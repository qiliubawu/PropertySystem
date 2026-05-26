using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;
using PropertySystem.Models;
using System.Security.Claims;

namespace PropertySystem.Controllers
{
    // 所有人都能访问，但在代码内部做身份分流
    public class VisitorController : Controller
    {
        private readonly AppDbContext _context;

        public VisitorController(AppDbContext context)
        {
            _context = context;
        }

        // 1. 扫码统一入口：智能身份路由
        [HttpGet]
        public IActionResult Register()
        {
            // 🌟 核心优化：如果扫码的手机当前已经登录了系统，直接分流！
            if (User.Identity.IsAuthenticated)
            {
                var role = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

                // 如果是业主，出示业主绿码
                if (role == "Owner") return RedirectToAction("OwnerPass");

                // 如果是物业员工/管理员，出示员工蓝码
                if (role == "Admin" || role == "Security" || role == "Maintenance" || role == "Cleaner")
                    return RedirectToAction("StaffPass");
            }

            // 都没有登录，说明是纯外来访客，展示普通的登记表单
            return View(new Visitor());
        }

        // 2. 访客提交表单 (保持不变)
        [HttpPost]
        public IActionResult Register(Visitor model)
        {
            if (model.EndTime <= model.StartTime)
                ModelState.AddModelError("EndTime", "离开时间必须晚于进入时间！");

            if (ModelState.IsValid)
            {
                model.VisitTime = DateTime.Now;
                model.Status = "访问中";
                _context.Visitors.Add(model);
                _context.SaveChanges();
                return RedirectToAction("Success");
            }
            return View(model);
        }

        // 3. 访客登记成功提示 (保持不变)
        public IActionResult Success() => Content("<h2 style='text-align:center; color:green; margin-top:50px;'>✅ 登记成功！欢迎进入小区。</h2><p style='text-align:center'>请向保安出示此页面</p>", "text/html", System.Text.Encoding.UTF8);

        // ================= 新增：专属电子通行证模块 =================

        // 4. 业主专属通行证
        public IActionResult OwnerPass()
        {
            int userId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var owner = _context.Owners.FirstOrDefault(o => o.UserId == userId);

            if (owner == null) return Content("业主档案异常");
            return View(owner); // 把业主数据传给通行证页面
        }

        // 5. 内部员工专属通行证
        public IActionResult StaffPass()
        {
            ViewBag.StaffName = User.Identity.Name;
            ViewBag.StaffRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            return View();
        }
    }
}
