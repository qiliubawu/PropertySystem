using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminParkingController : Controller
    {
        private readonly AppDbContext _context;

        public AdminParkingController(AppDbContext context) { _context = context; }

        // 1.列表
        public IActionResult Index(string searchNo, string searchStatus, int pageNumber = 1)
        {
            var parkings = _context.ParkingSpaces.Include(p => p.Owner).AsQueryable();

            if (!string.IsNullOrEmpty(searchNo)) parkings = parkings.Where(p => p.ParkingNo.Contains(searchNo));
            if (!string.IsNullOrEmpty(searchStatus)) parkings = parkings.Where(p => p.Status == searchStatus);

            ViewBag.SearchNo = searchNo; ViewBag.SearchStatus = searchStatus;

            var sortedList = parkings.OrderBy(p => p.Location).ThenBy(p => p.ParkingNo).ToList();
            return View(PaginatedList<ParkingSpace>.Create(sortedList, pageNumber, 20));
        }


        // 2.可视化：智能车库地图
        public IActionResult VisualBoard()
        {
            var parkings = _context.ParkingSpaces.Include(p => p.Owner).ToList();
            // 按区域分组传给前端 (如：地下A区，地下B区)
            ViewBag.GroupedParkings = parkings.GroupBy(p => p.Location).ToList();
            return View();
        }

        // 基于规划图纸的车位一键布点 
        [HttpGet]
        public IActionResult BatchCreate()
        {
            // 给前端带去当前小区建了几栋楼的数据
            ViewBag.BuildingCount = _context.Houses.Select(h => h.BuildingNo).Distinct().Count();
            return View();
        }

        [HttpPost]
        public IActionResult BatchCreate(bool generateUnderground, bool generateGround)
        {
            var newParkings = new List<ParkingSpace>();

            // 1. 生成地下大型标准停车场
            if (generateUnderground)
            {
                string[] zones = { "A", "B", "C" };
                foreach (var zone in zones)
                {
                    for (int i = 1; i <= 50; i++) // 每区50个
                    {
                        string finalNo = $"UG-{zone}{i:D3}"; // 如 UG-A001
                        if (!_context.ParkingSpaces.Any(p => p.ParkingNo == finalNo))
                        {
                            newParkings.Add(new ParkingSpace
                            {
                                ParkingNo = finalNo,
                                Location = $"地下核心车库 {zone}区",
                                Type = "产权车位",
                                Area = 12.5m,
                                Status = "未绑定"
                            });
                        }
                    }
                }
            }

            // 2. 生成地上访客/临时车位 
            if (generateGround)
            {
                var buildings = _context.Houses.Select(h => h.BuildingNo).Distinct().ToList();
                if (!buildings.Any())
                {
                    TempData["Error"] = "系统检测到当前尚未建楼，无法生成地上楼前车位！请先去沙盘建楼。";
                    return RedirectToAction("BatchCreate");
                }

                foreach (var b in buildings)
                {
                    for (int i = 1; i <= 10; i++)
                    {
                        string finalNo = $"GR-B{b}-{i:D2}"; // 如 GR-B1-01
                        if (!_context.ParkingSpaces.Any(p => p.ParkingNo == finalNo))
                        {
                            newParkings.Add(new ParkingSpace
                            {
                                ParkingNo = finalNo,
                                Location = $"第 {b} 栋 楼前露天区",
                                Type = "临时/租赁车位",
                                Area = 10.0m,
                                Status = "未绑定"
                            });
                        }
                    }
                }
            }

            if (newParkings.Any())
            {
                _context.ParkingSpaces.AddRange(newParkings);
                _context.SaveChanges();
                TempData["Success"] = $"🚗 智能布点成功！系统已自动生成 {newParkings.Count} 个标准停车位。";
            }
            else
            {
                TempData["Error"] = "所选区域的车位均已存在，系统自动跳过生成，避免重复。";
            }

            return RedirectToAction("VisualBoard");
        }

        // 一键清空车位 =
        [HttpPost]
        public IActionResult ClearAll()
        {
            // 只能清空未绑定的车位，保护业主资产
            var unboundParkings = _context.ParkingSpaces.Where(p => p.Status == "未绑定").ToList();
            _context.ParkingSpaces.RemoveRange(unboundParkings);
            _context.SaveChanges();
            TempData["Success"] = $"已强行清空 {unboundParkings.Count} 个未出售的空置车位！";
            return RedirectToAction("Index");
        }


      
        [HttpGet] public IActionResult Create() => View(new ParkingSpace());
        [HttpPost]
        public IActionResult Create(ParkingSpace parking)
        {
            if (_context.ParkingSpaces.Any(p => p.ParkingNo == parking.ParkingNo)) ModelState.AddModelError("ParkingNo", "编号已存在");
            if (ModelState.IsValid) { _context.ParkingSpaces.Add(parking); _context.SaveChanges(); return RedirectToAction("Index"); }
            return View(parking);
        }
        [HttpGet] public IActionResult Edit(int id) { var p = _context.ParkingSpaces.Find(id); if (p == null) return NotFound(); return View(p); }
        [HttpPost]
        public IActionResult Edit(int id, ParkingSpace parking)
        {
            if (id != parking.Id) return BadRequest();
            if (ModelState.IsValid)
            {
                var existing = _context.ParkingSpaces.AsNoTracking().FirstOrDefault(p => p.Id == id);
                parking.Status = existing.Status; parking.OwnerId = existing.OwnerId; parking.CertCode = existing.CertCode;
                _context.ParkingSpaces.Update(parking); _context.SaveChanges(); return RedirectToAction("Index");
            }
            return View(parking);
        }
        public IActionResult Delete(int id)
        {
            var p = _context.ParkingSpaces.Find(id);
            if (p != null && p.Status == "未绑定") { _context.ParkingSpaces.Remove(p); _context.SaveChanges(); }
            return RedirectToAction("Index");
        }
    }
}
