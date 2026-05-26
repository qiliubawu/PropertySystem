using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin,Supervisor")] // 管理员和主管可看
    public class AdminEquipmentController : Controller
    {
        private readonly AppDbContext _context;

        public AdminEquipmentController(AppDbContext context)
        {
            _context = context;
        }

        //设备台账大厅
        public IActionResult Index(string searchName, string searchCategory, string searchStatus, int pageNumber = 1)
        {
            var query = _context.Equipments.AsQueryable();

            //大盘统计
            var allData = query.ToList();
            ViewBag.TotalCount = allData.Count;
            ViewBag.ErrorCount = allData.Count(e => e.Status == "故障报修");
            ViewBag.UrgentCount = allData.Count(e => e.IsMaintenanceUrgent);
            ViewBag.NormalCount = allData.Count(e => e.Status == "正常运行" && !e.IsMaintenanceUrgent);

            // 搜索过滤
            if (!string.IsNullOrEmpty(searchName)) query = query.Where(e => e.Name.Contains(searchName) || e.EquipmentNo.Contains(searchName));
            if (!string.IsNullOrEmpty(searchCategory)) query = query.Where(e => e.Category == searchCategory);
            if (!string.IsNullOrEmpty(searchStatus)) query = query.Where(e => e.Status == searchStatus);

            ViewBag.SearchName = searchName; ViewBag.SearchCategory = searchCategory; ViewBag.SearchStatus = searchStatus;

            // 排序并执行分页
            var sortedList = query.ToList().OrderByDescending(e => e.IsMaintenanceUrgent).ThenBy(e => e.NextMaintenanceDate).ToList();
            return View(PaginatedList<Equipment>.Create(sortedList, pageNumber, 20));
        }

        // 2. 蓝图部署引擎

        [HttpGet]
        public IActionResult BlueprintDeploy()
        {
            ViewBag.BuildingCount = _context.Houses.Select(h => h.BuildingNo).Distinct().Count();
            return View();
        }

        [HttpPost]
        public IActionResult BlueprintDeploy(DateTime nextMaintenanceDate)
        {
            var buildings = _context.Houses.Select(h => h.BuildingNo).Distinct().OrderBy(b => b).ToList();

            if (!buildings.Any())
            {
                TempData["Error"] = "小区目前是一片空地，请先去【智能沙盘】建楼！";
                return RedirectToAction("BlueprintDeploy");
            }

            var newEqs = new List<Equipment>();

            foreach (var b in buildings)
            {
                string bStr = $"B{b}";
                newEqs.Add(CreateEq($"ELEV-{bStr}-01", $"第{b}栋 客运电梯", "特种设备", $"{b}栋 电梯井", nextMaintenanceDate));
                newEqs.Add(CreateEq($"ELEV-{bStr}-02", $"第{b}栋 消防/货运电梯", "特种设备", $"{b}栋 电梯井", nextMaintenanceDate));
                newEqs.Add(CreateEq($"CHG-{bStr}-01", $"智能电瓶车充电桩", "公共设施", $"{b}栋 楼前停放区", nextMaintenanceDate));

                string[] trashTypes = { "厨余垃圾桶", "其他垃圾桶", "可回收物桶", "有害垃圾桶" };
                for (int i = 0; i < 4; i++)
                {
                    newEqs.Add(CreateEq($"TRSH-{bStr}-0{i + 1}", trashTypes[i], "公共设施", $"{b}栋 一楼大堂外", nextMaintenanceDate));
                }

                var floors = _context.Houses.Where(h => h.BuildingNo == b).Select(h => h.Floor).Distinct().ToList();
                foreach (var f in floors)
                {
                    newEqs.Add(CreateEq($"FIRE-{bStr}-F{f}", $"消防栓及灭火器箱", "消防安防", $"{b}栋 {f}层 楼道", nextMaintenanceDate));
                    newEqs.Add(CreateEq($"CAM-{bStr}-F{f}", $"全景高清监控探头", "消防安防", $"{b}栋 {f}层 电梯厅", nextMaintenanceDate));
                }
                newEqs.Add(CreateEq($"FIRE-{bStr}-B1", $"地下车库消防总控箱", "消防安防", $"{b}栋 地下B1层", nextMaintenanceDate));
            }

            newEqs.Add(CreateEq("ENT-CEN-01", "大型儿童滑梯游乐组", "公共设施", "小区中央广场", nextMaintenanceDate));
            newEqs.Add(CreateEq("FIT-CEN-01", "全民健身器材组合", "公共设施", "小区中央广场", nextMaintenanceDate));
            newEqs.Add(CreateEq("PUMP-01", "小区二次供水主水泵", "水电气暖", "地下核心设备房", nextMaintenanceDate));
            newEqs.Add(CreateEq("GATE-01", "东门车辆识别道闸", "消防安防", "小区东大门", nextMaintenanceDate));
            newEqs.Add(CreateEq("GATE-02", "人脸识别门禁主机", "消防安防", "小区东大门", nextMaintenanceDate));

            foreach (var eq in newEqs)
            {
                eq.EquipmentNo = eq.EquipmentNo + "-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
            }

            _context.Equipments.AddRange(newEqs);
            _context.SaveChanges();

            TempData["Success"] = $"🏗️ 蓝图部署成功！共铺设了 {newEqs.Count} 个智能设备。";
            return RedirectToAction("Index");
        }

        // 私有辅助方法
        private Equipment CreateEq(string no, string name, string category, string location, DateTime nextMaint)
        {
            return new Equipment
            {
                EquipmentNo = no,
                Name = name,
                Category = category,
                Location = location,
                Status = "正常运行",
                PurchaseDate = DateTime.Today,
                NextMaintenanceDate = nextMaint,
                Supplier = "标准化蓝图统一部署"
            };
        }

        // 3. 一键清空
        [HttpPost]
        public IActionResult ClearAll()
        {
            var all = _context.Equipments.ToList();
            _context.Equipments.RemoveRange(all);
            _context.SaveChanges();
            TempData["Success"] = "全部设备已清空！";
            return RedirectToAction("Index");
        }

        // 4. 一键维保续期 
        public IActionResult RenewMaintenance(int id)
        {
            var equip = _context.Equipments.Find(id);
            if (equip != null)
            {
                equip.LastMaintenanceDate = DateTime.Today;
                equip.NextMaintenanceDate = DateTime.Today.AddDays(365);
                equip.Status = "正常运行";
                _context.SaveChanges();
                TempData["Success"] = $"[{equip.Name}] 维保已核销！下次期限已自动延后1年。";
            }
            return RedirectToAction("Index");
        }

        //  5. 单条设备创建
        [HttpGet] public IActionResult Create() => View(new Equipment());

        [HttpPost]
        public IActionResult Create(Equipment model)
        {
            if (_context.Equipments.Any(e => e.EquipmentNo == model.EquipmentNo)) ModelState.AddModelError("EquipmentNo", "编号已存在");
            if (ModelState.IsValid) { _context.Equipments.Add(model); _context.SaveChanges(); return RedirectToAction("Index"); }
            return View(model);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var item = _context.Equipments.Find(id);
            return item == null ? NotFound() : View(item);
        }

        [HttpPost]
        public IActionResult Edit(int id, Equipment model)
        {
            if (id != model.Id) return BadRequest();
            if (ModelState.IsValid) { _context.Equipments.Update(model); _context.SaveChanges(); return RedirectToAction("Index"); }
            return View(model);
        }

        public IActionResult Delete(int id)
        {
            var item = _context.Equipments.Find(id);
            if (item != null) { _context.Equipments.Remove(item); _context.SaveChanges(); }
            return RedirectToAction("Index");
        }

        //  6. 公开接口 
        [AllowAnonymous]
        public IActionResult PublicDetail(string eqNo)
        {
            var equip = _context.Equipments.FirstOrDefault(e => e.EquipmentNo == eqNo);
            if (equip == null) return Content("找不到该设备数据！");
            return View(equip);
        }

    }
}
