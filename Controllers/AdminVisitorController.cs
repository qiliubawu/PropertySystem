using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    // 这里允许 Admin(管理员) 或者 Security(保安) 访问
    [Authorize(Roles = "Admin,Security")]
    public class AdminVisitorController : Controller
    {
        private readonly AppDbContext _context;

        public AdminVisitorController(AppDbContext context)
        {
            _context = context;
        }

        // 1. 访客列表
        public IActionResult Index(int pageNumber = 1)
        {
            var sortedList = _context.Visitors.OrderByDescending(v => v.VisitTime).ToList();
            return View(PaginatedList<Visitor>.Create(sortedList, pageNumber, 20));
        }


        // 2. 标记访客已离开
        public IActionResult MarkLeave(int id)
        {
            var visitor = _context.Visitors.Find(id);
            if (visitor != null)
            {
                visitor.Status = "已人工签出"; // 强制覆盖底层状态
                visitor.LeaveTime = DateTime.Now;
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }


        // 3. 删除记录 
        public IActionResult Delete(int id)
        {
            var visitor = _context.Visitors.Find(id);
            if (visitor != null) { _context.Visitors.Remove(visitor); _context.SaveChanges(); }
            return RedirectToAction("Index");
        }
    }
}
