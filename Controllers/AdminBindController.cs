using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminBindController : Controller
    {
        private readonly AppDbContext _context;
        public AdminBindController(AppDbContext context) { _context = context; }

        //绑定
        public IActionResult Index()
        {
            // 获取未绑定的房屋和车位
            ViewBag.UnboundHouses = _context.Houses.Where(h => h.OwnerId == null).ToList();
            ViewBag.UnboundParkings = _context.ParkingSpaces.Where(p => p.Status == "未绑定").ToList();
            return View();
        }

        // 生成房产凭证码
        public IActionResult GenerateHouseCode(int id)
        {
            var house = _context.Houses.Find(id);
            if (house != null)
            {
                house.CertCode = "HOUSE-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        //生成车位凭证码
        public IActionResult GenerateParkingCode(int id)
        {
            var parking = _context.ParkingSpaces.Find(id);
            if (parking != null)
            {
                parking.CertCode = "PARK-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}
