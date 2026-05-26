using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;

namespace PropertySystem.Controllers
{
    // 只要属于这 5 种内部角色的其中之一，就能进入大厅
    [Authorize(Roles = "Admin,Security,Cleaner,Maintenance,FireSafety")]
    public class StaffAnnouncementController : Controller
    {
        private readonly AppDbContext _context;

        public StaffAnnouncementController(AppDbContext context) { _context = context; }

        public IActionResult Index()
        {
            // 获取所有公告，按置顶和时间排序
            var announcements = _context.Announcements
                                        .OrderByDescending(a => a.IsTop)
                                        .ThenByDescending(a => a.PublishTime)
                                        .ToList();

            // 根据当前登录的角色，传给前端，用来判断该加载哪个母版页的导航栏
            ViewBag.CurrentRole = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;

            return View(announcements);
        }
    }
}
