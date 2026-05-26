using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;
using PropertySystem.Models;
using System.Security.Claims;
using System.IO;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Security,Cleaner,FireSafety,Maintenance")]
    public class StaffSharedController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public StaffSharedController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context; _env = env;
        }

        // 1. 公共报修接口
        [HttpPost]
        public async Task<IActionResult> SubmitPublicRepair(string repairType, string description, IFormFile? imageFile)
        {
            var publicOwner = _context.Owners.FirstOrDefault(o => o.Name == "💡 物业公共区域");
            if (publicOwner == null)
            {
                publicOwner = new Owner { Name = "💡 物业公共区域", Phone = "400-000-0000", Gender = "男", Age = 1, ResidentCount = 1 };
                _context.Owners.Add(publicOwner); _context.SaveChanges();
            }

            string staffName = User.Identity.Name ?? "未知员工";
            string role = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "";
            string roleName = role == "Security" ? "保安" : (role == "Cleaner" ? "保洁" : (role == "Maintenance" ? "维修工" : "消防员"));

            var req = new RepairRequest
            {
                OwnerId = publicOwner.Id,
                RepairType = repairType,
                Description = $"【内部{roleName} {staffName} 提单】 " + description,
                Status = "待接单",
                CreateTime = DateTime.Now
            };

            if (imageFile != null)
            {
                string folder = Path.Combine(_env.WebRootPath, "uploads/repairs"); Directory.CreateDirectory(folder);
                string uniqueName = Guid.NewGuid().ToString() + "_staff_" + imageFile.FileName;
                using (var stream = new FileStream(Path.Combine(folder, uniqueName), FileMode.Create)) { await imageFile.CopyToAsync(stream); }
                req.BeforeImage = "/uploads/repairs/" + uniqueName;
            }

            _context.RepairRequests.Add(req); await _context.SaveChangesAsync();
            TempData["SharedMsg"] = "🛠️ 公共报修已发送！";
            return Redirect(Request.Headers["Referer"].ToString());
        }

        //  2. 物资需求申报接口
        [HttpPost]
        public IActionResult SubmitMaterialRequest(string itemName, int quantity, string reason)
        {
            int userId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var req = new MaterialRequest
            {
                StaffId = userId,
                ItemName = itemName,
                Quantity = quantity,
                Reason = reason,
                Status = "待处理",
                RequestTime = DateTime.Now
            };
            _context.MaterialRequests.Add(req);
            _context.SaveChanges();

            TempData["SharedMsg"] = "📦 物资申报已提交，请等待主管审批！";
            return Redirect(Request.Headers["Referer"].ToString());
        }
    }
}
