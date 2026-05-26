using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminAnnouncementController : Controller
    {
        private readonly AppDbContext _context;

        public AdminAnnouncementController(AppDbContext context)
        {
            _context = context;
        }

        // 1. 公告列表 
        // 1. 新闻流列表 
        public IActionResult Index(string searchTitle, string searchType, int pageNumber = 1)
        {
            var query = _context.Announcements.AsQueryable();

            if (!string.IsNullOrEmpty(searchTitle)) query = query.Where(a => a.Title.Contains(searchTitle));
            if (!string.IsNullOrEmpty(searchType)) query = query.Where(a => a.Type == searchType);

            ViewBag.SearchTitle = searchTitle; ViewBag.SearchType = searchType;

            var sortedList = query.OrderByDescending(a => a.IsTop).ThenByDescending(a => a.PublishTime).ToList();
            return View(PaginatedList<Announcement>.Create(sortedList, pageNumber, 20));
        }



        // 2. 发布新公告
        [HttpGet]
        public IActionResult Create() => View(new Announcement());

        [HttpPost]
        public IActionResult Create(Announcement model)
        {
            if (ModelState.IsValid)
            {
                // 自动获取当前登录管理员的名字作为发布人
                model.Publisher = User.Identity.Name ?? "系统管理员";
                model.PublishTime = DateTime.Now;

                _context.Announcements.Add(model);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // 3. 编辑公告
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var item = _context.Announcements.Find(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        public IActionResult Edit(int id, Announcement model)
        {
            if (id != model.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                var existingItem = _context.Announcements.Find(id);
                if (existingItem != null)
                {
                    existingItem.Title = model.Title;
                    existingItem.Content = model.Content;
                    existingItem.IsTop = model.IsTop;
                    // 发布人和时间保持不变

                    _context.SaveChanges();
                    return RedirectToAction("Index");
                }
            }
            return View(model);
        }

        // 4. 删除公告
        public IActionResult Delete(int id)
        {
            var item = _context.Announcements.Find(id);
            if (item != null)
            {
                _context.Announcements.Remove(item);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}
