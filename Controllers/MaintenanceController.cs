using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Maintenance")] // 仅维修工可进
    public class MaintenanceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public MaintenanceController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context; _env = env;
        }

        private int GetCurrentUserId() => int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

        //  抢单大厅 
        public IActionResult Index()
        {
            // 查出所有“待接单”的工单
            var publicOrders = _context.RepairRequests.Include(r => r.Owner)
                                       .Where(r => r.Status == "待接单")
                                       .OrderBy(r => r.CreateTime).ToList();
            return View(publicOrders);
        }

        // 抢单动作
        public IActionResult GrabOrder(int id)
        {
            var req = _context.RepairRequests.Find(id);
            // 必须还是待接单状态才能抢
            if (req != null && req.Status == "待接单")
            {
                req.WorkerId = GetCurrentUserId(); // 绑定给我
                req.Status = "维修中";
                req.AcceptTime = DateTime.Now;
                _context.SaveChanges();
                TempData["Msg"] = "🎉 抢单成功！请尽快联系业主并前往现场。";
            }
            return RedirectToAction("MyTasks"); // 抢到后跳到我的任务页
        }

        //我的任务工作台 
        public IActionResult MyTasks()
        {
            int myId = GetCurrentUserId();
            var myOrders = _context.RepairRequests.Include(r => r.Owner)
                                   .Where(r => r.WorkerId == myId && (r.Status == "维修中" || r.Status == "待确认"))
                                   .OrderByDescending(r => r.AcceptTime).ToList();

            // 获取仓库里还有货的材料，发给前端下拉框
            ViewBag.AvailableMaterials = _context.Materials.Where(m => m.Stock > 0).ToList();

            return View(myOrders);
        }


        // 完工拍照上传
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> FinishTask(int id, string notes, IFormFile imageFile, int[] materialIds, int[] quantities)
        {
            var req = _context.RepairRequests.Find(id);
            if (req != null && req.Status == "维修中" && req.WorkerId == GetCurrentUserId())
            {
                // 1. 处理照片 
                if (imageFile != null)
                {
                    string folder = Path.Combine(_env.WebRootPath, "uploads/repairs"); Directory.CreateDirectory(folder);
                    string fileName = Guid.NewGuid().ToString() + "_finish_" + imageFile.FileName;
                    using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create)) { await imageFile.CopyToAsync(stream); }
                    req.AfterImage = "/uploads/repairs/" + fileName;
                }

                // 2. 核心进销存逻辑：扣减库存并核算这单的物料总成本
                decimal totalCost = 0;
                if (materialIds != null && quantities != null && materialIds.Length == quantities.Length)
                {
                    for (int i = 0; i < materialIds.Length; i++)
                    {
                        int mId = materialIds[i]; int qty = quantities[i];
                        if (qty > 0)
                        {
                            var material = _context.Materials.Find(mId);
                            // 检查库存是否够扣
                            if (material != null && material.Stock >= qty)
                            {
                                material.Stock -= qty; // 扣减库存
                                decimal cost = material.UnitPrice * qty;
                                totalCost += cost;

                                // 记录明细
                                _context.RepairMaterials.Add(new RepairMaterial
                                {
                                    RepairRequestId = req.Id,
                                    MaterialId = mId,
                                    Quantity = qty,
                                    Cost = cost
                                });
                            }
                        }
                    }
                }

                req.MaterialCost = totalCost; // 将算好的成本写入工单
                req.RepairNotes = notes;
                req.Status = "待确认";
                req.FinishTime = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["Msg"] = $"✅ 维修结案！本次消耗物料成本: ¥{totalCost}，库存已自动扣减。";
            }
            return RedirectToAction("MyTasks");
        }


        //Tab 3: 个人业绩看板
        public IActionResult Performance()
        {
            int myId = GetCurrentUserId();
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            // 查出本月我接过的所有单子
            var myMonthOrders = _context.RepairRequests
                .Where(r => r.WorkerId == myId && r.AcceptTime.HasValue && r.AcceptTime.Value.Month == currentMonth && r.AcceptTime.Value.Year == currentYear)
                .ToList();

            // 1. 本月接单总数
            ViewBag.TotalOrders = myMonthOrders.Count;

            // 2. 好评率 (评分为 4 或 5 星的算好评)
            var evaluatedOrders = myMonthOrders.Where(r => r.Rating.HasValue).ToList();
            if (evaluatedOrders.Any())
            {
                int goodReviews = evaluatedOrders.Count(r => r.Rating >= 4);
                ViewBag.GoodRate = ((double)goodReviews / evaluatedOrders.Count * 100).ToString("0.0") + "%";
            }
            else { ViewBag.GoodRate = "暂无评价"; }

            // 3. 超时率 (接单到完工超过 24 小时算超时)
            var finishedOrders = myMonthOrders.Where(r => r.FinishTime.HasValue).ToList();
            if (finishedOrders.Any())
            {
                int timeoutCount = finishedOrders.Count(r => (r.FinishTime.Value - r.AcceptTime.Value).TotalHours > 24);
                ViewBag.TimeoutRate = ((double)timeoutCount / finishedOrders.Count * 100).ToString("0.0") + "%";
            }
            else { ViewBag.TimeoutRate = "0.0%"; }

            ViewBag.WorkerName = _context.Users.Find(myId)?.FullName;
            return View(evaluatedOrders); // 把历史评价列表传过去展示
        }
    }
}
