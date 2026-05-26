using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;
using System.IO;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Security")]
    public class SecurityController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SecurityController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context; _env = env;
        }

        private int GetCurrentUserId() => int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

        // Tab 1: 安防巡更 
        public IActionResult Index()
        {
            // 1. 核心生成算法：每天第一次进来时，自动铺设早、中、晚三套巡逻任务网
            if (!_context.CleanerTasks.Any(t => t.TaskDate == DateTime.Today && t.TaskType == "保安"))
            {
                string[] locations = { "小区正大门岗亭", "地下车库B2层深处死角", "物业中控室配电房", "小区中央广场游乐区" };
                string[] periods = { "早班 (06:00-12:00)", "中班 (12:00-18:00)", "晚班 (18:00-06:00)" };

                var newTasks = new List<CleanerTask>();
                foreach (var p in periods)
                {
                    foreach (var loc in locations)
                    {
                        newTasks.Add(new CleanerTask { Location = loc, TaskDate = DateTime.Today, TaskType = "保安", Period = p });
                    }
                }
                _context.CleanerTasks.AddRange(newTasks);
                _context.SaveChanges();
            }

            // 2. 将今日的保安任务按“时间段”分组传给前端展示
            var todayTasks = _context.CleanerTasks.Where(t => t.TaskDate == DateTime.Today && t.TaskType == "保安")
                                     .OrderBy(t => t.Id).ToList();
            ViewBag.GroupedTasks = todayTasks.GroupBy(t => t.Period).ToList();

            return View();
        }

        // 提交巡更打卡 
        [HttpPost]
        public async Task<IActionResult> SubmitScan(CleanerTask model, string scanResult)
        {
            var task = _context.CleanerTasks.Find(model.Id);
            if (task != null && task.Status == "待打卡")
            {
                //如果没有传照片，直接拦截并驳回
                if (model.ImageFile == null || model.ImageFile.Length == 0)
                {
                    TempData["ErrorMsg"] = "🚨 违规操作拦截：巡更打卡必须拍摄并上传现场照片留底防伪！";
                    return RedirectToAction("Index");
                }

                task.CleanerId = GetCurrentUserId();
                task.ScanTime = DateTime.Now;
                task.AbnormalNotes = model.AbnormalNotes;

                // 保存必须上传的现场照片
                string folder = Path.Combine(_env.WebRootPath, "uploads/security");
                Directory.CreateDirectory(folder);
                string fileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }
                task.AbnormalImage = "/uploads/security/" + fileName;

                // 判断状态：是安全签到，还是隐患上报
                if (scanResult == "安全")
                {
                    task.Status = "正常完成";
                    TempData["Msg"] = "🛡️ 巡更点位打卡安全，照片已录入云端！";
                }
                else
                {
                    task.Status = "异常上报";
                    TempData["Msg"] = "🚨 安全隐患已带图上报至指挥中心！";
                }

                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        // ... (底下的 Handover 交接班 和 Profile 我的档案方法保持不变，不要删) ...
        [HttpGet]
        public IActionResult Handover()
        {
            int myId = GetCurrentUserId(); var history = _context.ShiftHandovers.Where(s => s.FromCleanerId == myId && s.HandoverType == "保安").OrderByDescending(s => s.HandoverTime).ToList();
            ViewBag.History = history; return View(new ShiftHandover());
        }
        [HttpPost]
        public IActionResult Handover(ShiftHandover model)
        {
            model.FromCleanerId = GetCurrentUserId(); model.HandoverTime = DateTime.Now; model.HandoverType = "保安";
            if (ModelState.IsValid) { _context.ShiftHandovers.Add(model); _context.SaveChanges(); TempData["Msg"] = "交接班完成！"; return RedirectToAction("Handover"); }
            return Handover();
        }
        public IActionResult Profile() => View(_context.Users.Find(GetCurrentUserId()));
    }
}
