using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")] // 仅最高管理员可管理员工
    public class AdminStaffController : Controller
    {
        private readonly AppDbContext _context;

        public AdminStaffController(AppDbContext context) { _context = context; }

        // 1. 员工通讯录 (排除业主，只看内部员工)
        public IActionResult Index(string searchName, string searchRole, int pageNumber = 1)
        {
            var query = _context.Users.Where(u => u.Role != "Owner").AsQueryable();
            if (!string.IsNullOrEmpty(searchName)) query = query.Where(u => u.FullName.Contains(searchName) || u.Username.Contains(searchName));
            if (!string.IsNullOrEmpty(searchRole)) query = query.Where(u => u.Role == searchRole);

            ViewBag.SearchName = searchName; ViewBag.SearchRole = searchRole;

            var sortedList = query.OrderBy(u => u.Role).ToList();
            return View(PaginatedList<User>.Create(sortedList, pageNumber, 20));
        }


        // 2. 员工入职录入
        [HttpGet] public IActionResult Create() => View(new User());

        [HttpPost]
        public IActionResult Create(User user)
        {
            if (_context.Users.Any(u => u.Username == user.Username))
                ModelState.AddModelError("Username", "该登录账号已被占用！");

            // 严禁在此创建业主账号
            if (user.Role == "Owner") return BadRequest("非法操作");

            if (ModelState.IsValid)
            {
                _context.Users.Add(user);
                _context.SaveChanges();
                TempData["Success"] = $"🎉 成功为新员工 [{user.FullName}] 分配了 [{user.Role}] 权限账号！";
                return RedirectToAction("Index");
            }
            return View(user);
        }

        // 3. 员工信息/岗位调整
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null || user.Role == "Owner") return NotFound();
            return View(user);
        }

        [HttpPost]
        public IActionResult Edit(int id, User user)
        {
            if (id != user.Id) return BadRequest();

            if (_context.Users.Any(u => u.Username == user.Username && u.Id != id))
                ModelState.AddModelError("Username", "该账号名已被其他员工占用！");

            if (ModelState.IsValid)
            {
                var existing = _context.Users.Find(id);
                if (existing != null)
                {
                    existing.Username = user.Username;
                    existing.FullName = user.FullName;
                    existing.Password = user.Password;
                    existing.Role = user.Role;
                    _context.SaveChanges();
                    TempData["Success"] = $"员工 [{user.FullName}] 的档案与权限已更新！";
                }
                return RedirectToAction("Index");
            }
            return View(user);
        }

        // 4. 员工离职注销
        public IActionResult Delete(int id)
        {
            // 绝对不能删除当前正在登录的自己
            int currentUserId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value ?? "0");
            if (id == currentUserId)
            {
                TempData["Error"] = "⚠️ 安全警告：您不能注销当前正在使用的管理员账号！";
                return RedirectToAction("Index");
            }

            var user = _context.Users.Find(id);
            if (user != null && user.Role != "Owner")
            {
                //如果这个维修工名下有历史报修单，直接删会报错。
                bool hasRepairs = _context.RepairRequests.Any(r => r.WorkerId == id);
                if (hasRepairs)
                {
                    TempData["Error"] = $"❌ 删除失败：[{user.FullName}] 曾参与过报修工单处理。为保证历史追溯完整，系统禁止删除该账号。建议点击编辑将其密码修改，或在姓名后标注(已离职)。";
                    return RedirectToAction("Index");
                }

                _context.Users.Remove(user);
                _context.SaveChanges();
                TempData["Success"] = "✅ 账号已永久注销。";
            }
            return RedirectToAction("Index");
        }
        //员工 KPI 
        public IActionResult WorkOrderMonitor()
        {
            var rankings = _context.Users.Where(u => u.Role == "Maintenance").Select(u => new {
                WorkerName = u.FullName,
                TotalOrders = _context.RepairRequests.Count(r => r.WorkerId == u.Id),
                CompletedOrders = _context.RepairRequests.Count(r => r.WorkerId == u.Id && r.Status == "已完成"),
                GoodReviews = _context.RepairRequests.Count(r => r.WorkerId == u.Id && r.Rating >= 4),
                AvgRating = _context.RepairRequests.Where(r => r.WorkerId == u.Id && r.Rating.HasValue).Average(r => r.Rating) ?? 0
            }).OrderByDescending(x => x.AvgRating).ThenByDescending(x => x.TotalOrders).ToList();
            ViewBag.Rankings = rankings;
            return View();
        }

    }
}
