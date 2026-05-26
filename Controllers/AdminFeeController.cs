using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;
using ClosedXML.Excel;
using System.IO;
using Microsoft.AspNetCore.SignalR;
using PropertySystem.Hubs;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminFeeController : Controller
    {
        private readonly AppDbContext _context;
        public AdminFeeController(AppDbContext context) { _context = context; }

        private decimal GetPrice(string key, decimal defaultPrice)
        {
            var config = _context.SystemConfigs.FirstOrDefault(c => c.ConfigKey == key);
            if (config != null && decimal.TryParse(config.ConfigValue, out decimal price)) return price;
            return defaultPrice;
        }

        // 1. 财务大盘
        public IActionResult Index(string searchMonth, string searchType, string searchStatus, int pageNumber = 1)
        {
            var bills = _context.Bills.Include(b => b.Owner).AsQueryable();

            if (!string.IsNullOrEmpty(searchMonth)) bills = bills.Where(b => b.BillingMonth == searchMonth);
            if (!string.IsNullOrEmpty(searchType)) bills = bills.Where(b => b.FeeType == searchType);
            if (!string.IsNullOrEmpty(searchStatus))
            {
                bool isPaid = searchStatus == "已缴费"; bills = bills.Where(b => b.IsPaid == isPaid);
            }

            var allFiltered = bills.ToList();

            decimal lateRate = GetPrice("Fee_Late_Rate", 0.003m);
            foreach (var b in allFiltered)
            {
                if (!b.IsPaid && (DateTime.Now - b.CreateTime).TotalDays > 30)
                {
                    int overdueDays = (int)(DateTime.Now - b.CreateTime).TotalDays - 30;
                    b.LateFee = Math.Round(b.Amount * lateRate * overdueDays, 2);
                }
            }

            decimal expected = allFiltered.Sum(b => b.Amount + b.LateFee);
            decimal collected = allFiltered.Where(b => b.IsPaid).Sum(b => b.Amount);
            ViewBag.FinanceExpected = expected;
            ViewBag.FinanceCollected = collected;
            ViewBag.TotalUnpaid = expected - collected;
            ViewBag.CollectionRate = expected > 0 ? (collected / expected * 100).ToString("0.0") : "0.0";

            ViewBag.SearchMonth = searchMonth; ViewBag.SearchType = searchType; ViewBag.SearchStatus = searchStatus;
            ViewBag.CurrentFilter = $"?searchMonth={searchMonth}&searchType={searchType}&searchStatus={searchStatus}";

            var sortedList = allFiltered.OrderByDescending(b => b.CreateTime).ToList();
            return View(PaginatedList<Bill>.Create(sortedList, pageNumber, 20));
        }

        // 2. 批量发账
        [HttpPost]
        public IActionResult BatchGenerate(string targetMonth, string feeType)
        {
            var owners = _context.Owners.ToList();
            int successCount = 0; int skipCount = 0;

            decimal priceProp = GetPrice("Fee_Property", 2.5m);
            decimal priceOwnedPark = GetPrice("Fee_Parking_Owned", 80m);
            decimal priceRentedPark = GetPrice("Fee_Parking_Rented", 300m);
            decimal pricePublic = GetPrice("Fee_Public_Utility", 0.5m);
            decimal priceTrash = GetPrice("Fee_Trash", 15m);

            foreach (var owner in owners)
            {
                if (_context.Bills.Any(b => b.OwnerId == owner.Id && b.BillingMonth == targetMonth && b.FeeType == feeType)) { skipCount++; continue; }

                decimal amount = 0; string remark = ""; bool canGenerate = false;

                if (feeType == "住宅物业费")
                {
                    var houses = _context.Houses.Where(h => h.OwnerId == owner.Id).ToList();
                    if (houses.Any()) { amount = houses.Sum(h => h.Area) * priceProp; remark = $"住宅总面积 {houses.Sum(h => h.Area)}㎡ × {priceProp}元"; canGenerate = true; }
                }
                else if (feeType == "公摊水电能耗费")
                {
                    var houses = _context.Houses.Where(h => h.OwnerId == owner.Id).ToList();
                    if (houses.Any()) { amount = houses.Sum(h => h.Area) * pricePublic; remark = $"住宅总面积 {houses.Sum(h => h.Area)}㎡ × {pricePublic}元(公摊基数)"; canGenerate = true; }
                }
                else if (feeType == "生活垃圾处理费")
                {
                    var houses = _context.Houses.Where(h => h.OwnerId == owner.Id).ToList();
                    if (houses.Any()) { amount = houses.Count * priceTrash; remark = $"名下住宅 {houses.Count}套 × 户均固定收费 {priceTrash}元"; canGenerate = true; }
                }
                else if (feeType == "产权车位管理费")
                {
                    var parkings = _context.ParkingSpaces.Where(p => p.OwnerId == owner.Id && p.Type.Contains("产权")).ToList();
                    if (parkings.Any()) { amount = parkings.Count * priceOwnedPark; remark = $"产权车位 {parkings.Count}个 × {priceOwnedPark}元"; canGenerate = true; }
                }
                else if (feeType == "租赁车位使用费")
                {
                    var parkings = _context.ParkingSpaces.Where(p => p.OwnerId == owner.Id && p.Type.Contains("租赁")).ToList();
                    if (parkings.Any()) { amount = parkings.Count * priceRentedPark; remark = $"租赁车位 {parkings.Count}个 × {priceRentedPark}元"; canGenerate = true; }
                }

                if (canGenerate && amount > 0)
                {
                    _context.Bills.Add(new Bill { OwnerId = owner.Id, FeeType = feeType, BillingMonth = targetMonth, Amount = amount, Remark = remark, IsPaid = false, CreateTime = DateTime.Now });
                    successCount++;
                }
            }
            _context.SaveChanges(); TempData["Success"] = $"批量发账完成！生成 {successCount} 份。"; return RedirectToAction("Index");
        }

        // 3. 单条手工补录
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Owners = _context.Owners.ToList();
            return View(new Bill { BillingMonth = DateTime.Now.ToString("yyyy-MM") });
        }

        [HttpPost]
        public IActionResult Create(Bill bill)
        {
            if (bill.Amount <= 0) ModelState.AddModelError("Amount", "账单金额必须大于 0 元！");

            if (ModelState.IsValid)
            {
                var owner = _context.Owners.Find(bill.OwnerId);
                if (owner == null) return BadRequest();
                if (_context.Bills.Any(b => b.OwnerId == owner.Id && b.BillingMonth == bill.BillingMonth && b.FeeType == bill.FeeType))
                { TempData["Error"] = "该业主本月已存在同类账单，请勿重复录入！"; return RedirectToAction("Create"); }

                bill.IsPaid = false; bill.CreateTime = DateTime.Now;
                _context.Bills.Add(bill); _context.SaveChanges();
                TempData["Success"] = $"手工账单录入成功！向 {owner.Name} 催收 {bill.Amount} 元。"; return RedirectToAction("Index");
            }
            ViewBag.Owners = _context.Owners.ToList(); return View(bill);
        }

        
        public IActionResult MarkAsPaid(int id) { var bill = _context.Bills.Find(id); if (bill != null) { bill.IsPaid = true; _context.SaveChanges(); } return RedirectToAction("Index"); }
        public IActionResult PrintReceipt(int id) { var bill = _context.Bills.Include(b => b.Owner).FirstOrDefault(b => b.Id == id); if (bill == null) return NotFound(); return View(bill); }

        [HttpGet]
        public IActionResult ExportExcel(string searchMonth, string searchType, string searchStatus)
        {
            var bills = _context.Bills.Include(b => b.Owner).AsQueryable();
            if (!string.IsNullOrEmpty(searchMonth)) bills = bills.Where(b => b.BillingMonth == searchMonth);
            if (!string.IsNullOrEmpty(searchType)) bills = bills.Where(b => b.FeeType == searchType);
            if (!string.IsNullOrEmpty(searchStatus)) { bool isPaid = searchStatus == "已缴费"; bills = bills.Where(b => b.IsPaid == isPaid); }
            var exportData = bills.OrderByDescending(b => b.CreateTime).ToList();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("财务账单明细");
                var currentRow = 1;
                worksheet.Cell(currentRow, 1).Value = "账单单号"; worksheet.Cell(currentRow, 2).Value = "业主姓名"; worksheet.Cell(currentRow, 3).Value = "联系电话";
                worksheet.Cell(currentRow, 4).Value = "费用类型"; worksheet.Cell(currentRow, 5).Value = "账期"; worksheet.Cell(currentRow, 6).Value = "本金金额(元)";
                worksheet.Cell(currentRow, 7).Value = "状态"; worksheet.Cell(currentRow, 8).Value = "生成时间";
                worksheet.Range("A1:H1").Style.Font.Bold = true; worksheet.Range("A1:H1").Style.Fill.BackgroundColor = XLColor.LightGray;

                foreach (var item in exportData)
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = item.Id.ToString("D8"); worksheet.Cell(currentRow, 2).Value = item.Owner.Name; worksheet.Cell(currentRow, 3).Value = item.Owner.Phone;
                    worksheet.Cell(currentRow, 4).Value = item.FeeType; worksheet.Cell(currentRow, 5).Value = item.BillingMonth; worksheet.Cell(currentRow, 6).Value = item.Amount;
                    worksheet.Cell(currentRow, 7).Value = item.IsPaid ? "已缴费" : "欠费"; worksheet.Cell(currentRow, 8).Value = item.CreateTime.ToString("yyyy-MM-dd HH:mm");
                }
                worksheet.Columns().AdjustToContents();
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream); var content = stream.ToArray(); return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"智慧社区财务报表_{DateTime.Now.ToString("yyyyMMdd")}.xlsx");
                }
            }
        }

        [HttpGet] public IActionResult FeeConfig() => View(_context.SystemConfigs.ToList());
        [HttpPost]
        public IActionResult UpdateConfig(int id, string configValue)
        {
            var config = _context.SystemConfigs.Find(id); if (config != null) { config.ConfigValue = configValue; _context.SaveChanges(); TempData["Success"] = "收费标准更新成功！"; }
            return RedirectToAction("FeeConfig");
        }

        public IActionResult ArrearsPenetration()
        {
            var unpaidBills = _context.Bills.Include(b => b.Owner).Where(b => !b.IsPaid).ToList();
            ViewBag.PenetrationData = unpaidBills.GroupBy(b => b.Owner).Select(g => new { Owner = g.Key, TotalArrears = g.Sum(b => b.Amount), BillCount = g.Count(), Detail = string.Join(", ", g.Select(b => b.BillingMonth + b.FeeType)) }).OrderByDescending(x => x.TotalArrears).ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendDunning(int ownerId, decimal amount, [FromServices] IHubContext<NotificationHub> hubContext)
        {
            var owner = _context.Owners.Find(ownerId);
            if (owner != null)
            {
                var msg = new Message { OwnerId = owner.Id, Title = "🚨 欠费催缴函", Content = $"您当前累计欠费 {amount} 元，请尽快缴纳！", Type = "催缴通知", CreateTime = DateTime.Now };
                _context.Messages.Add(msg); await _context.SaveChangesAsync();
                if (owner.UserId.HasValue) { await hubContext.Clients.Group($"User_{owner.UserId.Value}").SendAsync("ReceiveMessage", msg.Title, msg.Content); }
                TempData["Success"] = "催缴单已秒推至业主手机！";
            }
            return RedirectToAction("ArrearsPenetration");
        }
    }
}
