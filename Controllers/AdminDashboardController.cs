using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly AppDbContext _context;
        public AdminDashboardController(AppDbContext context) { _context = context; }

        public IActionResult Index()
        {
            var now = DateTime.Now;

            // 1. 资产与入住指标
            int totalHouses = _context.Houses.Count();
            int occupiedHouses = _context.Houses.Count(h => h.OwnerId != null);
            ViewBag.TotalHouses = totalHouses;
            ViewBag.OccupancyRate = totalHouses > 0 ? (occupiedHouses * 100.0 / totalHouses).ToString("0") : "0";

            ViewBag.TotalParkings = _context.ParkingSpaces.Count();
            ViewBag.SoldParkings = _context.ParkingSpaces.Count(p => p.OwnerId != null);

            // 2. 财务营收指标 (本月)
            string curMonth = now.ToString("yyyy-MM");
            var curBills = _context.Bills.Where(b => b.BillingMonth == curMonth).ToList();

            // 声明并计算 expected 和 collected
            decimal expected = curBills.Sum(b => b.Amount);
            decimal collected = curBills.Where(b => b.IsPaid).Sum(b => b.Amount);

            //赋值给 ViewBag
            ViewBag.FinanceExpected = expected;
            ViewBag.FinanceCollected = collected;
            ViewBag.TotalUnpaid = expected - collected; // 此时 expected 已经有值了，绝不会报错
            ViewBag.CollectionRate = expected > 0 ? (collected / expected * 100).ToString("0.0") : "0";

            // 3. 物联设备指标
            var eqs = _context.Equipments.ToList();
            ViewBag.EqTotal = eqs.Count;
            ViewBag.EqError = eqs.Count(e => e.Status == "故障报修");
            ViewBag.EqUrgent = eqs.Count(e => e.IsMaintenanceUrgent && e.Status != "故障报修");

            // 4. 工单与客服指标 (今日)
            ViewBag.RepairPending = _context.RepairRequests.Count(r => r.Status == "待接单");
            ViewBag.ComplaintPending = _context.Complaints.Count(c => c.Status == "待处理");

            // 5. 安防与员工指标 (今日)
            ViewBag.TodayVisitors = _context.Visitors.Count(v => v.VisitTime.Date == now.Date);
            ViewBag.StaffCount = _context.Users.Count(u => u.Role != "Owner" && u.Role != "Admin");

            return View();
        }
    }
}
