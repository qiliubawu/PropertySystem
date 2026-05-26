using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;
using System.IO;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "FireSafety")]
    public class FireSafetyController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public FireSafetyController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context; _env = env;
        }

        private int GetCurrentUserId() => int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

        //  警控与维保大屏 
        public IActionResult Index()
        {
            var fireEqs = _context.Equipments.Where(e => e.Category == "消防安防").ToList();

            ViewBag.ErrorEqs = fireEqs.Where(e => e.Status == "故障报修").ToList();
            ViewBag.UrgentEqs = fireEqs.Where(e => e.Status == "正常运行" && e.IsMaintenanceUrgent).ToList();
            ViewBag.NormalCount = fireEqs.Count(e => e.Status == "正常运行" && !e.IsMaintenanceUrgent);

            return View();
        }

        [HttpPost]
        public IActionResult CheckAndRenew(int eqId, string checkResult)
        {
            var equip = _context.Equipments.Find(eqId);
            if (equip != null && equip.Category == "消防安防")
            {
                if (checkResult == "正常")
                {
                    equip.LastMaintenanceDate = DateTime.Today; equip.NextMaintenanceDate = DateTime.Today.AddMonths(6);
                    equip.Status = "正常运行"; TempData["Msg"] = $"✅ [{equip.Name}] 巡检合格！已更新电子防伪检查标。";
                }
                else
                {
                    equip.Status = "故障报修"; TempData["Msg"] = $"🚨 [{equip.Name}] 已标记为故障！请尽快联系工程部更换。";
                }
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // 每日消防防火巡检任务
        [HttpGet]
        public IActionResult DailyPatrol()
        {
            // 1. 动态生成任务：去设备表里把所有消防安防设备抓出来，每天要求去打一次卡！
            if (!_context.CleanerTasks.Any(t => t.TaskDate == DateTime.Today && t.TaskType == "消防"))
            {
                var fireEquipments = _context.Equipments.Where(e => e.Category == "消防安防").ToList();
                var newTasks = new List<CleanerTask>();

                foreach (var eq in fireEquipments)
                {
                    newTasks.Add(new CleanerTask
                    {
                        Location = $"{eq.Location} - {eq.Name}", // 把具体是哪个设备写清楚
                        TaskDate = DateTime.Today,
                        TaskType = "消防",
                        Period = "防火巡查日检" // 这个时段名称随便起，主要是用来分组
                    });
                }
                if (newTasks.Any()) { _context.CleanerTasks.AddRange(newTasks); _context.SaveChanges(); }
            }

            // 获取今天的消防打卡列表
            var todayTasks = _context.CleanerTasks.Where(t => t.TaskDate == DateTime.Today && t.TaskType == "消防").OrderBy(t => t.Id).ToList();
            return View(todayTasks);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitPatrolScan(CleanerTask model, string scanResult)
        {
            var task = _context.CleanerTasks.Find(model.Id);
            if (task != null && task.Status == "待打卡")
            {
                // 必须现场拍照证明设备状态
                if (model.ImageFile == null || model.ImageFile.Length == 0)
                {
                    TempData["ErrorMsg"] = "🚨 违规拦截：防火巡检必须拍摄现场消防器材的照片留底防伪，禁止文字虚假打卡！";
                    return RedirectToAction("DailyPatrol");
                }

                task.CleanerId = GetCurrentUserId();
                task.ScanTime = DateTime.Now;
                task.AbnormalNotes = model.AbnormalNotes;

                string folder = Path.Combine(_env.WebRootPath, "uploads/firesafety");
                Directory.CreateDirectory(folder);
                string fileName = Guid.NewGuid().ToString() + "_fire_" + model.ImageFile.FileName;
                using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }
                task.AbnormalImage = "/uploads/firesafety/" + fileName;

                if (scanResult == "安全")
                {
                    task.Status = "正常完成"; TempData["Msg"] = "🛡️ 消防器材压力/外观检查正常，已存档！";
                }
                else
                {
                    task.Status = "异常上报"; TempData["Msg"] = "🚨 消防隐患(如通道堵塞/器材失效)已上报！";
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("DailyPatrol");
        }

        //我的档案
        public IActionResult Profile() => View(_context.Users.Find(GetCurrentUserId()));
    }
}
