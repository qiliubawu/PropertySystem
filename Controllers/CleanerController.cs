using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;
using System.IO;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Cleaner")]
    public class CleanerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CleanerController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context; _env = env;
        }

        private int GetCurrentUserId() => int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

        //智能任务生成与派发大厅
        public IActionResult Index()
        {
            // 获取小区真实的楼栋列表
            var buildings = _context.Houses.Select(h => h.BuildingNo).Distinct().ToList();
            if (!buildings.Any())
            {
                buildings = new List<int> { 1 }; // 防呆：如果还没建楼，虚拟一栋避免报错
            }

            //  1. 动态生成【每日早晚】垃圾桶清运任务
            if (!_context.CleanerTasks.Any(t => t.TaskDate == DateTime.Today && t.TaskType == "保洁" && t.Period.Contains("清运")))
            {
                var newDailyTasks = new List<CleanerTask>();
                foreach (var b in buildings)
                {
                    newDailyTasks.Add(new CleanerTask { Location = $"第 {b} 栋楼下分类垃圾桶", TaskDate = DateTime.Today, TaskType = "保洁", Period = "1_早班清运 (07:00-09:00)" });
                    newDailyTasks.Add(new CleanerTask { Location = $"第 {b} 栋楼下分类垃圾桶", TaskDate = DateTime.Today, TaskType = "保洁", Period = "2_晚班清运 (18:00-20:00)" });
                }
                _context.CleanerTasks.AddRange(newDailyTasks);
                _context.SaveChanges();
            }

            //  2. 动态生成【本周度】楼栋大扫除任务 (判断本周一到周日是否生成过)
            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime startOfWeek = today.AddDays(-1 * diff).Date; // 算出本周一
            DateTime endOfWeek = startOfWeek.AddDays(6).Date;     // 算出本周日

            if (!_context.CleanerTasks.Any(t => t.TaskType == "保洁" && t.Period == "3_本周全面大扫除" && t.TaskDate >= startOfWeek && t.TaskDate <= endOfWeek))
            {
                var newWeeklyTasks = new List<CleanerTask>();
                foreach (var b in buildings)
                {
                    // 将周任务的日期标记为周一
                    newWeeklyTasks.Add(new CleanerTask { Location = $"第 {b} 栋全楼层走廊与死角", TaskDate = startOfWeek, TaskType = "保洁", Period = "3_本周全面大扫除" });
                }
                _context.CleanerTasks.AddRange(newWeeklyTasks);
                _context.SaveChanges();
            }

            //  3. 提取今天要显示的任务：(今天的日任务 + 本周还没过期的周任务)
            var tasksToDisplay = _context.CleanerTasks
                .Where(t => t.TaskType == "保洁" &&
                     ((t.TaskDate == DateTime.Today && t.Period.Contains("清运")) ||
                      (t.Period == "3_本周全面大扫除" && t.TaskDate >= startOfWeek && t.TaskDate <= endOfWeek)))
                .OrderBy(t => t.Id).ToList();

            // 按周期类型分组传给前端
            ViewBag.GroupedTasks = tasksToDisplay.GroupBy(t => t.Period).OrderBy(g => g.Key).ToList();

            return View();
        }

        // 提交保洁打卡 (强制拍照核验)
        [HttpPost]
        public async Task<IActionResult> SubmitScan(CleanerTask model, string scanResult)
        {
            var task = _context.CleanerTasks.Find(model.Id);
            if (task != null && task.Status == "待打卡")
            {
                //保洁必须拍照上传垃圾桶清理后/走廊拖地后的照片，否则驳回！
                if (model.ImageFile == null || model.ImageFile.Length == 0)
                {
                    TempData["ErrorMsg"] = "🚨 违规拦截：作业完毕必须现场拍照留痕，禁止虚假打卡！";
                    return RedirectToAction("Index");
                }

                task.CleanerId = GetCurrentUserId();
                task.ScanTime = DateTime.Now;
                task.AbnormalNotes = model.AbnormalNotes;

                // 存照片
                string folder = Path.Combine(_env.WebRootPath, "uploads/cleaner");
                Directory.CreateDirectory(folder);
                string fileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }
                task.AbnormalImage = "/uploads/cleaner/" + fileName;

                // 判断是否是报修异常
                if (scanResult == "正常")
                {
                    task.Status = "正常完成"; TempData["Msg"] = "✅ 现场已清理干净，照片已存档！辛苦了！";
                }
                else
                {
                    task.Status = "异常上报"; TempData["Msg"] = "⚠️ 设施损坏异常已随图上报至调度中心！";
                }

                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        // 保留：交接班与档案
        [HttpGet]
        public IActionResult Handover()
        {
            var history = _context.ShiftHandovers.Where(s => s.FromCleanerId == GetCurrentUserId() && s.HandoverType == "保洁").OrderByDescending(s => s.HandoverTime).ToList();
            ViewBag.History = history; return View(new ShiftHandover());
        }
        [HttpPost]
        public IActionResult Handover(ShiftHandover model)
        {
            model.FromCleanerId = GetCurrentUserId(); model.HandoverTime = DateTime.Now; model.HandoverType = "保洁";
            if (ModelState.IsValid) { _context.ShiftHandovers.Add(model); _context.SaveChanges(); TempData["Msg"] = "交接班完成！"; return RedirectToAction("Handover"); }
            return Handover();
        }
        public IActionResult Profile() => View(_context.Users.Find(GetCurrentUserId()));
    }
}
