using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminOwnerController : Controller
    {
        private readonly AppDbContext _context;
        public AdminOwnerController(AppDbContext context) { _context = context; }

        public IActionResult Index(string searchName, string searchPhone, int pageNumber = 1)
        {
            var owners = _context.Owners.AsQueryable();
            if (!string.IsNullOrEmpty(searchName)) owners = owners.Where(o => o.Name.Contains(searchName));
            if (!string.IsNullOrEmpty(searchPhone)) owners = owners.Where(o => o.Phone.Contains(searchPhone));
            ViewBag.SearchName = searchName; ViewBag.SearchPhone = searchPhone;
            return View(PaginatedList<Owner>.Create(owners.OrderByDescending(o => o.Id).ToList(), pageNumber, 20));
        }

        [HttpGet] public IActionResult Create() => View(new Owner());

        [HttpPost]
        public IActionResult Create(Owner owner)
        {
            if (_context.Owners.Any(o => o.Phone == owner.Phone)) ModelState.AddModelError("Phone", "手机号已存在！");
            if (string.IsNullOrEmpty(owner.LoginPassword)) ModelState.AddModelError("LoginPassword", "请设置初始密码！");

            if (ModelState.IsValid)
            {
                owner.ResidentCount = 1;
                var newUser = new User { Username = owner.Phone, Password = owner.LoginPassword, FullName = owner.Name, Role = "Owner" };
                _context.Users.Add(newUser); _context.SaveChanges();
                owner.UserId = newUser.Id; _context.Owners.Add(owner); _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(owner);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var owner = _context.Owners.Find(id); return owner == null ? NotFound() : View(owner);
        }

        [HttpPost]
        [HttpPost]
        public IActionResult Edit(int id, Owner owner)
        {
            if (id != owner.Id) return BadRequest();
            if (_context.Users.Any(u => u.Username == owner.Phone && u.Id != owner.UserId)) ModelState.AddModelError("Phone", "账号冲突！");

        
            ModelState.Remove("ResidentCount");
            ModelState.Remove("LoginPassword");

            if (ModelState.IsValid)
            {
                owner.ResidentCount = 1; // 强制锁定为1人
                _context.Owners.Update(owner);

                var user = _context.Users.Find(owner.UserId);
                if (user != null) { user.Username = owner.Phone; user.FullName = owner.Name; }

                _context.SaveChanges();
                TempData["Success"] = "业主资料更新成功！";
                return RedirectToAction("Index");
            }
            return View(owner);
        }


        [HttpGet] public IActionResult ChangePassword(int id) { var owner = _context.Owners.Find(id); return owner == null ? NotFound() : View(owner); }
        [HttpPost]
        public IActionResult ChangePassword(int id, string newPassword)
        {
            var owner = _context.Owners.Find(id); if (owner == null || !owner.UserId.HasValue) return BadRequest();
            if (newPassword.Length < 6) { TempData["Error"] = "密码格式错误"; return RedirectToAction("ChangePassword", new { id = id }); }
            var user = _context.Users.Find(owner.UserId.Value);
            if (user != null) { user.Password = newPassword; _context.SaveChanges(); TempData["Success"] = "密码修改成功！"; }
            return RedirectToAction("Index");
        }

        public IActionResult Profile(int id)
        {
            var owner = _context.Owners.Find(id); if (owner == null) return NotFound();
            ViewBag.Houses = _context.Houses.Where(h => h.OwnerId == id).ToList(); ViewBag.Parkings = _context.ParkingSpaces.Where(p => p.OwnerId == id).ToList();
            ViewBag.RecentBills = _context.Bills.Where(b => b.OwnerId == id).OrderByDescending(b => b.CreateTime).Take(5).ToList();
            ViewBag.RecentRepairs = _context.RepairRequests.Where(r => r.OwnerId == id).OrderByDescending(r => r.CreateTime).Take(5).ToList();
            return View(owner);
        }

        [HttpGet]
        public IActionResult BindAsset(int id)
        {
            var owner = _context.Owners.Find(id); if (owner == null) return NotFound();
            ViewBag.BoundHouses = _context.Houses.Where(h => h.OwnerId == id).ToList(); ViewBag.BoundParkings = _context.ParkingSpaces.Where(p => p.OwnerId == id).ToList();
            ViewBag.AvailableHouses = _context.Houses.Where(h => h.OwnerId == null).ToList(); ViewBag.AvailableParkings = _context.ParkingSpaces.Where(p => p.Status == "未绑定").ToList();
            return View(owner);
        }

        [HttpPost]
        public IActionResult BindHouse(int ownerId, int houseId)
        {
            var house = _context.Houses.Find(houseId); if (house != null) { house.OwnerId = ownerId; house.CertCode = null; _context.SaveChanges(); UpdateOwnerAssetStrings(ownerId); }
            return RedirectToAction("BindAsset", new { id = ownerId });
        }

        [HttpPost]
        public IActionResult BindParking(int ownerId, int parkingId)
        {
            var parking = _context.ParkingSpaces.Find(parkingId);
            if (parking != null)
            {
                int houseCount = _context.Houses.Count(h => h.OwnerId == ownerId); int currentParkings = _context.ParkingSpaces.Count(p => p.OwnerId == ownerId);
                if (houseCount == 0) TempData["Error"] = "暂无房产，无法分配车位！";
                else if (currentParkings >= houseCount * 2) TempData["Error"] = $"最多只能绑定 {houseCount * 2} 个车位！";
                else { parking.OwnerId = ownerId; parking.Status = "已绑定"; parking.CertCode = null; _context.SaveChanges(); UpdateOwnerAssetStrings(ownerId); TempData["Success"] = "车位分配成功！"; }
            }
            return RedirectToAction("BindAsset", new { id = ownerId });
        }

        [HttpPost]
        public IActionResult UnbindAsset(int ownerId, int? houseId, int? parkingId)
        {
            if (houseId.HasValue) { var house = _context.Houses.Find(houseId.Value); if (house != null) house.OwnerId = null; }
            if (parkingId.HasValue) { var parking = _context.ParkingSpaces.Find(parkingId.Value); if (parking != null) { parking.OwnerId = null; parking.Status = "未绑定"; } }
            _context.SaveChanges(); UpdateOwnerAssetStrings(ownerId); return RedirectToAction("BindAsset", new { id = ownerId });
        }

        private void UpdateOwnerAssetStrings(int ownerId)
        {
            var owner = _context.Owners.Find(ownerId); var houses = _context.Houses.Where(h => h.OwnerId == ownerId).ToList(); var parkings = _context.ParkingSpaces.Where(p => p.OwnerId == ownerId).ToList();
            string roomStr = houses.Any() ? string.Join(", ", houses.Select(h => $"{h.BuildingNo}栋{h.UnitNo}单元{h.RoomNo}")) : null;
            if (roomStr != null && roomStr.Length > 50) roomStr = roomStr.Substring(0, 47) + "...";
            owner.RoomNo = roomStr; owner.ParkingNo = parkings.Any() ? string.Join(", ", parkings.Select(p => p.ParkingNo)) : null; _context.SaveChanges();
        }

        // ================= 🌟 极度安全的彻底销户逻辑 (已修复外键冲突) =================
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var owner = _context.Owners.Find(id);
            if (owner != null)
            {
                // 1. 解绑名下所有的房产 (退回公海空置状态)
                var houses = _context.Houses.Where(h => h.OwnerId == id).ToList();
                foreach (var h in houses) { h.OwnerId = null; h.CertCode = null; }

                // 2. 解绑名下所有的车位 (状态改回未绑定)
                var parkings = _context.ParkingSpaces.Where(p => p.OwnerId == id).ToList();
                foreach (var p in parkings) { p.OwnerId = null; p.Status = "未绑定"; p.CertCode = null; }

                // 3. 级联删除相关的业务数据 (清除历史记录)
                _context.Bills.RemoveRange(_context.Bills.Where(b => b.OwnerId == id));
                _context.RepairRequests.RemoveRange(_context.RepairRequests.Where(r => r.OwnerId == id));
                _context.Complaints.RemoveRange(_context.Complaints.Where(c => c.OwnerId == id));
                _context.Messages.RemoveRange(_context.Messages.Where(m => m.OwnerId == id));

                // 获取将要被删除的底层账号 ID
                int? userIdToDelete = owner.UserId;

                // 4. 先删除业主本人档案，切断外键引用
                _context.Owners.Remove(owner);
                _context.SaveChanges(); // 必须先保存，解除数据库的引用警报！

                // 5. 最后，安全删除底层的登录账号
                if (userIdToDelete.HasValue)
                {
                    var user = _context.Users.Find(userIdToDelete.Value);
                    if (user != null)
                    {
                        _context.Users.Remove(user);
                        _context.SaveChanges(); // 二次保存
                    }
                }

                TempData["Success"] = $"🗑️ 业主 [{owner.Name}] 及其所有关联数据已被彻底注销，名下资产已退回空置状态！";
            }
            return RedirectToAction("Index");
        }
        //  1：业主画像库 
        public IActionResult OwnerPortrait(string searchName)
        {
            var owners = _context.Owners.AsQueryable();
            if (!string.IsNullOrEmpty(searchName)) owners = owners.Where(o => o.Name.Contains(searchName));

            ViewBag.SearchName = searchName;
            return View(owners.Select(o => new {
                Owner = o,
                RepairCount = _context.RepairRequests.Count(r => r.OwnerId == o.Id),
                ComplaintCount = _context.Complaints.Count(c => c.OwnerId == o.Id)
            }).ToList());
        }

        [HttpPost]
        public IActionResult UpdateOwnerTags(int ownerId, string tags)
        {
            var owner = _context.Owners.Find(ownerId);
            if (owner != null) { owner.Tags = tags; _context.SaveChanges(); TempData["Success"] = "画像标签更新成功！"; }
            return RedirectToAction("OwnerPortrait");
        }

        //  2：智能群发
        [HttpGet]
        public IActionResult MassMessage()
        {
            ViewBag.TotalOwners = _context.Owners.Count();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> MassMessage(string title, string content, string msgType, [FromServices] Microsoft.AspNetCore.SignalR.IHubContext<PropertySystem.Hubs.NotificationHub> hubContext)
        {
            var owners = _context.Owners.ToList();
            foreach (var owner in owners)
            {
                _context.Messages.Add(new Message { OwnerId = owner.Id, Title = title, Content = content, Type = msgType, CreateTime = DateTime.Now });
            }
            await _context.SaveChangesAsync();

            // 实时推送给在线业主
            await hubContext.Clients.Group("Role_Owner").SendAsync("ReceiveMessage", "📢 " + title, content);
            TempData["Success"] = "群发轰炸成功！在线业主已收到秒推弹窗。";
            return RedirectToAction("MassMessage");
        }

    }

}
