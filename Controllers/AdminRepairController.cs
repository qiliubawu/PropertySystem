using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminRepairController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminRepairController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context; _env = env;
        }

        // 1. 报修调度指挥中心
        public IActionResult Index(string searchStatus, string searchType, int pageNumber = 1)
        {
            var requests = _context.RepairRequests.Include(r => r.Owner).Include(r => r.Worker).AsQueryable();

            if (!string.IsNullOrEmpty(searchStatus)) requests = requests.Where(r => r.Status == searchStatus);
            if (!string.IsNullOrEmpty(searchType)) requests = requests.Where(r => r.RepairType == searchType);

            ViewBag.PendingCount = _context.RepairRequests.Count(r => r.Status == "待接单");
            ViewBag.ProcessingCount = _context.RepairRequests.Count(r => r.Status == "维修中");
            ViewBag.ConfirmCount = _context.RepairRequests.Count(r => r.Status == "待确认");
            ViewBag.CompletedCount = _context.RepairRequests.Count(r => r.Status == "已完成");

            var completed = _context.RepairRequests.Where(r => r.Status == "已完成" && r.Rating.HasValue).ToList();
            ViewBag.AvgRating = completed.Any() ? completed.Average(r => r.Rating.Value).ToString("0.0") : "0.0";
            ViewBag.MaintenanceWorkers = _context.Users.Where(u => u.Role == "Maintenance").ToList();

            ViewBag.SearchStatus = searchStatus; ViewBag.SearchType = searchType;

            var sortedList = requests.OrderByDescending(r => r.CreateTime).ToList();
            return View(PaginatedList<RepairRequest>.Create(sortedList, pageNumber, 20));
        }

        // 2.指派维修工 
        [HttpPost]
        public IActionResult DispatchOrder(int requestId, int workerId)
        {
            var req = _context.RepairRequests.Find(requestId);
            if (req != null && req.Status == "待接单")
            {
                req.WorkerId = workerId;
                req.Status = "维修中";
                req.AcceptTime = DateTime.Now; // 记录派单/接单时间
                _context.SaveChanges();
                TempData["Success"] = "派单成功！工单已流转至维修工终端。";
            }
            return RedirectToAction("Index");
        }

        // 3. 工单周期追踪
        public IActionResult Detail(int id)
        {
            var req = _context.RepairRequests
                .Include(r => r.Owner)
                .Include(r => r.Worker)
                .FirstOrDefault(r => r.Id == id);

            if (req == null) return NotFound();
            return View(req);
        }

        [HttpGet] public IActionResult Create() { ViewBag.Owners = _context.Owners.ToList(); return View(new RepairRequest()); }

        [HttpPost]
        public async Task<IActionResult> Create(RepairRequest req)
        {
            if (ModelState.IsValid)
            {
                if (req.BeforeImageFile != null)
                {
                    string folder = Path.Combine(_env.WebRootPath, "uploads/repairs"); Directory.CreateDirectory(folder);
                    string uniqueName = Guid.NewGuid().ToString() + "_" + req.BeforeImageFile.FileName;
                    using (var stream = new FileStream(Path.Combine(folder, uniqueName), FileMode.Create)) await req.BeforeImageFile.CopyToAsync(stream);
                    req.BeforeImage = "/uploads/repairs/" + uniqueName;
                }
                req.Status = "待接单"; req.CreateTime = DateTime.Now;
                _context.RepairRequests.Add(req); await _context.SaveChangesAsync(); return RedirectToAction("Index");
            }
            ViewBag.Owners = _context.Owners.ToList(); return View(req);
        }

        public IActionResult AcceptOrder(int id)
        { /* 保留旧的抢单功能，也可以不用 */
            var req = _context.RepairRequests.Find(id);
            if (req != null && req.Status == "待接单")
            {
                req.Status = "维修中"; req.AcceptTime = DateTime.Now;
                req.WorkerId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value); _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        [HttpGet] public IActionResult Complete(int id) { return View(_context.RepairRequests.Include(r => r.Owner).FirstOrDefault(r => r.Id == id)); }

        [HttpPost]
        public async Task<IActionResult> Complete(int id, RepairRequest model)
        {
            var req = _context.RepairRequests.Find(id);
            if (req != null && req.Status == "维修中")
            {
                if (model.AfterImageFile != null)
                {
                    string folder = Path.Combine(_env.WebRootPath, "uploads/repairs"); Directory.CreateDirectory(folder);
                    string uniqueName = Guid.NewGuid().ToString() + "_after_" + model.AfterImageFile.FileName;
                    using (var stream = new FileStream(Path.Combine(folder, uniqueName), FileMode.Create)) await model.AfterImageFile.CopyToAsync(stream);
                    req.AfterImage = "/uploads/repairs/" + uniqueName;
                }
                req.RepairNotes = model.RepairNotes; req.Status = "待确认"; req.FinishTime = DateTime.Now; await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        [HttpGet] public IActionResult Evaluate(int id) { return View(_context.RepairRequests.Include(r => r.Owner).FirstOrDefault(r => r.Id == id)); }

        [HttpPost]
        public IActionResult Evaluate(int id, int rating, string evaluation)
        {
            var req = _context.RepairRequests.Find(id);
            if (req != null && req.Status == "待确认")
            {
                req.Rating = rating; req.Evaluation = evaluation; req.ConfirmTime = DateTime.Now; req.Status = "已完成"; _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }
        //投诉处理 
        public IActionResult Complaints()
        {
            var complaints = _context.Complaints.Include(c => c.Owner).OrderByDescending(c => c.CreateTime).ToList();
            return View(complaints);
        }

        [HttpPost]
        public IActionResult ReplyComplaint(int id, string replyContent)
        {
            var c = _context.Complaints.Include(x => x.Owner).FirstOrDefault(x => x.Id == id);
            if (c != null && c.Status == "待处理")
            {
                c.Reply = replyContent;
                c.Status = "已回复";

                // 主动推送给业主手机，触发“小红点”
                var msg = new Message
                {
                    OwnerId = c.OwnerId,
                    Title = "🔔 投诉反馈通知",
                    Content = $"您提交的投诉/建议【{c.Title}】有了新的回复结果，请前往服务大厅查看详情。",
                    Type = "系统通知",
                    CreateTime = DateTime.Now
                };
                _context.Messages.Add(msg);

                _context.SaveChanges();
                TempData["Success"] = "回复已下发至业主终端！";
            }
            return RedirectToAction("Complaints");
        }

    }
}
