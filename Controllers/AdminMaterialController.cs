using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminMaterialController : Controller
    {
        private readonly AppDbContext _context;
        public AdminMaterialController(AppDbContext context) { _context = context; }

        //  1. 现有库存列表
        public IActionResult Index(int pageNumber = 1)
        {
            var list = _context.Materials.OrderBy(m => m.SKU).ToList();
            return View(PaginatedList<Material>.Create(list, pageNumber, 20));
        }

        [HttpPost]
        public IActionResult AddStock(int id, int addCount)
        {
            var mat = _context.Materials.Find(id);
            if (mat != null && addCount > 0)
            {
                mat.Stock += addCount; _context.SaveChanges(); TempData["Success"] = $"[{mat.Name}] 成功入库 {addCount} {mat.Unit}！";
            }
            return RedirectToAction("Index");
        }

        //  2. 员工申报审批列表
        public IActionResult Requests(int pageNumber = 1)
        {
            var list = _context.MaterialRequests.Include(r => r.Staff).OrderByDescending(r => r.RequestTime).ToList();
            return View(PaginatedList<MaterialRequest>.Create(list, pageNumber, 20));
        }

        // 3. 审批并直接转化入库
        [HttpPost]
        public IActionResult ProcessRequest(int requestId, string action, string sku, decimal unitPrice, string unit, string reply)
        {
            var req = _context.MaterialRequests.Find(requestId);
            if (req == null || req.Status != "待处理") return RedirectToAction("Requests");

            req.ProcessTime = DateTime.Now;
            req.AdminReply = reply;

            if (action == "Approve")
            {
                // 查找该 SKU 是否已在库存中
                var existingMaterial = _context.Materials.FirstOrDefault(m => m.SKU == sku);
                if (existingMaterial != null)
                {
                    // 存在则追加库存
                    existingMaterial.Stock += req.Quantity;
                }
                else
                {
                    // 不存在则新建一种物料
                    _context.Materials.Add(new Material
                    {
                        SKU = sku,
                        Name = req.ItemName,
                        Stock = req.Quantity,
                        UnitPrice = unitPrice,
                        Unit = unit
                    });
                }
                req.Status = "已入库";
                TempData["Success"] = $"✅ 申请已批准！成功为仓库增加 {req.Quantity} 个 {req.ItemName}。";
            }
            else
            {
                req.Status = "已驳回";
                TempData["Success"] = "❌ 申请已驳回！";
            }

            _context.SaveChanges();
            return RedirectToAction("Requests");
        }
    }
}
