using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Hubs;
using PropertySystem.Models;
using System.IO;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Owner")]
    public class OwnerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<NotificationHub> _hubContext;

        public OwnerController(AppDbContext context, IWebHostEnvironment env, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _env = env;
            _hubContext = hubContext;
        }

        private Owner? GetCurrentOwner()
        {
            int userId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            return _context.Owners.FirstOrDefault(o => o.UserId == userId);
        }

        public IActionResult Index()
        {
            var owner = GetCurrentOwner(); if (owner == null) return Content("<h3 style='padding:20px; color:red;'>账号异常</h3>", "text/html", System.Text.Encoding.UTF8);
            ViewBag.Announcements = _context.Announcements.OrderByDescending(a => a.IsTop).ThenByDescending(a => a.PublishTime).Take(5).ToList();
            var myMessages = _context.Messages.Where(m => m.OwnerId == owner.Id).OrderByDescending(m => m.CreateTime).ToList();
            ViewBag.UnreadCount = myMessages.Count(m => !m.IsRead); ViewBag.MyMessages = myMessages;
            return View();
        }

        [HttpPost]
        public IActionResult ReadMessage(int msgId)
        {
            var msg = _context.Messages.Find(msgId); if (msg != null && msg.OwnerId == GetCurrentOwner()?.Id) { msg.IsRead = true; _context.SaveChanges(); }
            return Ok();
        }

        public IActionResult Services()
        {
            var owner = GetCurrentOwner(); if (owner == null) return Content("账号异常", "text/html", System.Text.Encoding.UTF8);
            ViewBag.Repairs = _context.RepairRequests.Where(r => r.OwnerId == owner.Id).OrderByDescending(r => r.CreateTime).ToList();
            ViewBag.Complaints = _context.Complaints.Where(c => c.OwnerId == owner.Id).OrderByDescending(c => c.CreateTime).ToList(); return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitRepair(RepairRequest req)
        {
            var owner = GetCurrentOwner(); if (owner == null) return RedirectToAction("Services");
            req.OwnerId = owner.Id; req.Status = "待接单"; req.CreateTime = DateTime.Now;
            if (req.BeforeImageFile != null)
            {
                string folder = Path.Combine(_env.WebRootPath, "uploads/repairs"); Directory.CreateDirectory(folder);
                string uniqueName = Guid.NewGuid().ToString() + "_" + req.BeforeImageFile.FileName;
                using (var stream = new FileStream(Path.Combine(folder, uniqueName), FileMode.Create)) { await req.BeforeImageFile.CopyToAsync(stream); }
                req.BeforeImage = "/uploads/repairs/" + uniqueName;
            }
            _context.RepairRequests.Add(req); await _context.SaveChangesAsync(); TempData["ServiceSuccess"] = "报修工单已提交！"; // 🚀 先发射信号
            if (_hubContext != null)
            {
                await _hubContext.Clients.Group("Role_Maintenance")
                                 .SendAsync("ReceiveNewOrder", "🛠️ 新单提醒！", $"业主报修：{req.RepairType}，请查收！");
            }

            TempData["ServiceSuccess"] = "报修工单已提交！";
            return RedirectToAction("Services"); // 最后 return

            // 🚀 信号发射：只要一报修，瞬间广播给全小区所有的维修工！
            await _hubContext.Clients.Group("Role_Maintenance")
                             .SendAsync("ReceiveNewOrder", "🛠️ 新单提醒！", $"业主报修：{req.RepairType}，请立即前往抢单大厅查收！");

        }

        [HttpPost]
        public IActionResult SubmitComplaint(string title, string content)
        {
            var owner = GetCurrentOwner();
            if (owner != null && !string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(content))
            {
                _context.Complaints.Add(new Complaint { OwnerId = owner.Id, Title = title, Content = content, Status = "待处理", CreateTime = DateTime.Now });
                _context.SaveChanges(); TempData["ServiceSuccess"] = "投诉建议已提交！";
            }
            return RedirectToAction("Services");
        }

        [HttpPost]
        public IActionResult EvaluateRepair(int repairId, int rating, string evaluation)
        {
            var owner = GetCurrentOwner(); var req = _context.RepairRequests.Find(repairId);
            if (owner != null && req != null && req.OwnerId == owner.Id && req.Status == "待确认")
            {
                req.Rating = rating; req.Evaluation = evaluation; req.ConfirmTime = DateTime.Now; req.Status = "已完成"; _context.SaveChanges();
            }
            return RedirectToAction("Services");
        }

        public IActionResult RepairDetail(int id)
        {
            var owner = GetCurrentOwner(); if (owner == null) return RedirectToAction("Index");
            var req = _context.RepairRequests.Include(r => r.Worker).FirstOrDefault(r => r.Id == id && r.OwnerId == owner.Id);
            return req == null ? NotFound("找不到该工单。") : View(req);
        }

        public IActionResult Bills()
        {
            var owner = GetCurrentOwner(); if (owner == null) return Content("账号异常", "text/html");
            var myBills = _context.Bills.Where(b => b.OwnerId == owner.Id).OrderByDescending(b => b.CreateTime).ToList();
            ViewBag.UnpaidBills = myBills.Where(b => !b.IsPaid).ToList(); ViewBag.PaidBills = myBills.Where(b => b.IsPaid).ToList(); return View();
        }

        [HttpPost]
        public IActionResult MockPay(int billId)
        {
            var bill = _context.Bills.Find(billId); var owner = GetCurrentOwner();
            if (owner != null && bill != null && bill.OwnerId == owner.Id && !bill.IsPaid)
            {
                bill.IsPaid = true; _context.SaveChanges(); TempData["PaySuccess"] = "支付成功！感谢您的配合。";
            }
            return RedirectToAction("Bills");
        }

        public IActionResult Profile() { return View(GetCurrentOwner()); }

        [HttpGet] public IActionResult EditProfile() { return View(GetCurrentOwner()); }

        [HttpPost]
        public IActionResult EditProfile(Owner model)
        {
            var owner = GetCurrentOwner(); if (owner == null) return RedirectToAction("Index");

            if (ModelState.IsValid)
            {
                owner.Name = model.Name; owner.Gender = model.Gender; owner.Age = model.Age;
                owner.Email = model.Email; owner.CarPlate = model.CarPlate;
                var user = _context.Users.Find(owner.UserId); if (user != null) user.FullName = model.Name;
                _context.SaveChanges(); TempData["ProfileSuccess"] = "个人资料修改成功！"; return RedirectToAction("Profile");
            }
            return View(model);
        }

        [HttpPost]
        public IActionResult ClaimAsset(string code)
        {
            if (string.IsNullOrEmpty(code)) return RedirectToAction("Profile");
            code = code.ToUpper().Trim(); var owner = GetCurrentOwner(); if (owner == null) return RedirectToAction("Profile");
            if (code.StartsWith("HOUSE-"))
            {
                var house = _context.Houses.FirstOrDefault(h => h.CertCode == code);
                if (house != null)
                {
                    house.OwnerId = owner.Id; house.CertCode = null; var ownerHouses = _context.Houses.Where(h => h.OwnerId == owner.Id).ToList();
                    string roomStr = string.Join(", ", ownerHouses.Select(h => $"{h.BuildingNo}栋{h.UnitNo}单元{h.RoomNo}"));
                    if (roomStr.Length > 50) roomStr = roomStr.Substring(0, 47) + "..."; owner.RoomNo = roomStr; _context.SaveChanges(); TempData["AuthMsg"] = "房产认证成功！";
                }
                else TempData["AuthError"] = "凭证码无效或已被使用";
            }
            else if (code.StartsWith("PARK-"))
            {
                var parking = _context.ParkingSpaces.FirstOrDefault(p => p.CertCode == code);
                if (parking != null)
                {
                    int houseCount = _context.Houses.Count(h => h.OwnerId == owner.Id); int currentParkings = _context.ParkingSpaces.Count(p => p.OwnerId == owner.Id);
                    if (houseCount == 0) TempData["AuthError"] = "请先认证房产，才能绑定车位！";
                    else if (currentParkings >= houseCount * 2) TempData["AuthError"] = $"超出限制！";
                    else
                    {
                        parking.OwnerId = owner.Id; parking.Status = "已绑定"; parking.CertCode = null; var ownerParkings = _context.ParkingSpaces.Where(p => p.OwnerId == owner.Id).ToList();
                        owner.ParkingNo = string.Join(", ", ownerParkings.Select(p => p.ParkingNo)); _context.SaveChanges(); TempData["AuthMsg"] = "车位认证成功！";
                    }
                }
                else TempData["AuthError"] = "凭证码无效或已被使用";
            }
            return RedirectToAction("Profile");
        }
    }
}
