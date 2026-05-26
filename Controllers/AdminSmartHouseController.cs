using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertySystem.Data;
using PropertySystem.Models;

namespace PropertySystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminSmartHouseController : Controller
    {
        private readonly AppDbContext _context;

        public AdminSmartHouseController(AppDbContext context)
        {
            _context = context;
        }

        // 1. 小区沙盘总览
        public IActionResult Index()
        {
            // 按楼栋号分组统计
            var buildingStats = _context.Houses
                .GroupBy(h => h.BuildingNo)
                .Select(g => new
                {
                    BuildingNo = g.Key,
                    TotalRooms = g.Count(),
                    VacantRooms = g.Count(h => h.OwnerId == null),
                    OccupiedRooms = g.Count(h => h.OwnerId != null)
                })
                .OrderBy(b => b.BuildingNo)
                .ToList();

            // 转换为 ViewBag 传给前端动态使用
            ViewBag.BuildingStats = buildingStats;
            return View();
        }

        // 2. 一键批量建楼页面
        [HttpGet]
        public IActionResult BatchCreate() => View();

    
        [HttpPost]
        // 3. 一键批量建楼 
        [HttpPost]
        public IActionResult BatchCreate(int buildingNo, int totalFloors) // 删除了 hasUnderground 参数
        {
            if (_context.Houses.Any(h => h.BuildingNo == buildingNo))
            {
                TempData["Error"] = $"第 {buildingNo} 栋楼已经存在，无法重复生成！请先删除或使用逐条录入。";
                return RedirectToAction("BatchCreate");
            }

            var newHouses = new List<House>();
            string[] units = { "A", "B" };

            // 循环生成地上楼层
            for (int f = 1; f <= totalFloors; f++)
            {
                foreach (var unit in units)
                {
                    for (int r = 1; r <= 4; r++)
                    {
                        string roomNo = $"{f}{(r < 10 ? "0" + r : r.ToString())}";
                        newHouses.Add(CreateHouseTemplate(buildingNo, unit, f.ToString(), roomNo));
                    }
                }
            }

            _context.Houses.AddRange(newHouses);
            _context.SaveChanges();

            TempData["Success"] = $"🎉 第 {buildingNo} 栋纯住宅楼构建完成！共生成 {newHouses.Count} 套房产。";
            return RedirectToAction("Index");
        }


        // 生成标准的房间模板
        private House CreateHouseTemplate(int bNo, string unit, string floor, string roomNo)
        {
            return new House
            {
                BuildingNo = bNo,
                UnitNo = unit,
                Floor = floor,
                RoomNo = roomNo,
                Layout = "三室一厅",
                Area = 120.0m,
                Elevators = 2,
                Cameras = 3,
                Stairs = 2,
                FireEquipments = 2
            };
        }

        // 4. 楼栋销控矩阵图 (Digital Twin)
        public IActionResult VisualBoard(int buildingNo)
        {
            var houses = _context.Houses.Include(h => h.Owner)
                                        .Where(h => h.BuildingNo == buildingNo)
                                        .ToList();
            if (!houses.Any()) return NotFound("找不到该楼栋数据");
            ViewBag.AllOwners = _context.Owners.OrderBy(o => o.Name).ToList();

            ViewBag.BuildingNo = buildingNo;
            return View(houses);
        }



        // 5. 传统列表视图
        public IActionResult RoomList(int? searchBuilding, string searchUnit, string searchRoom, int pageNumber = 1)
        {
            var houses = _context.Houses.Include(h => h.Owner).AsQueryable();

            if (searchBuilding.HasValue) houses = houses.Where(h => h.BuildingNo == searchBuilding);
            if (!string.IsNullOrEmpty(searchUnit)) houses = houses.Where(h => h.UnitNo.Contains(searchUnit));
            if (!string.IsNullOrEmpty(searchRoom)) houses = houses.Where(h => h.RoomNo.Contains(searchRoom));

            ViewBag.SearchBuilding = searchBuilding; ViewBag.SearchUnit = searchUnit; ViewBag.SearchRoom = searchRoom;

            // 排序后进行分页切片
            var sortedList = houses.OrderBy(h => h.BuildingNo).ThenBy(h => h.Floor).ToList();
            return View(PaginatedList<House>.Create(sortedList, pageNumber, 20));
        }


        // 6. 手动录入单套房屋 
        [HttpGet]
        public IActionResult CreateSingle() => View(new House());

        [HttpPost]
        public IActionResult CreateSingle(House house)
        {
            if (ModelState.IsValid)
            {
                _context.Houses.Add(house);
                _context.SaveChanges();
                return RedirectToAction("RoomList"); // 保存后回到列表页
            }
            return View(house);
        }

        // 7. 编辑房屋 
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var house = _context.Houses.Find(id);
            if (house == null) return NotFound();
            return View(house);
        }

        [HttpPost]
        public IActionResult Edit(int id, House house)
        {
            if (id != house.Id) return BadRequest();

            if (ModelState.IsValid)
            {
          
                var existing = _context.Houses.AsNoTracking().FirstOrDefault(h => h.Id == id);
                house.CertCode = existing?.CertCode;
                house.OwnerId = existing?.OwnerId;

                _context.Houses.Update(house);
                _context.SaveChanges();
                // 编辑完成后，智能判断：返回之前的页面（列表或沙盘）
                return RedirectToAction("RoomList");
            }
            return View(house);
        }

        // 8. 删除房屋
        public IActionResult Delete(int id)
        {
            var house = _context.Houses.Find(id);
            if (house != null)
            {
                _context.Houses.Remove(house);
                _context.SaveChanges();
            }
            return RedirectToAction("RoomList");
        }
        // 爆破整栋大楼
        [HttpPost]
        public IActionResult DeleteBuilding(int buildingNo)
        {
            // 查出这栋楼的所有房子
            var houses = _context.Houses.Where(h => h.BuildingNo == buildingNo).ToList();

       
            if (houses.Any(h => h.OwnerId != null))
            {
                TempData["Error"] = $"🚨 爆破中止！第 {buildingNo} 栋已有业主入住，严禁整栋删除！请先将该楼栋所有业主解绑。";
                return RedirectToAction("Index");
            }

            _context.Houses.RemoveRange(houses); // 批量删除集合
            _context.SaveChanges();

            TempData["Success"] = $"💣 轰！第 {buildingNo} 栋大楼已被成功拆除，共清理 {houses.Count} 条数据。";
            return RedirectToAction("Index");
        }

        //  列表勾选批量删除
        [HttpPost]
        public IActionResult BatchDeleteRooms(int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0)
            {
                TempData["Error"] = "请至少勾选一条需要删除的数据！";
                return RedirectToAction("RoomList");
            }

            
            var housesToDelete = _context.Houses
                .Where(h => selectedIds.Contains(h.Id) && h.OwnerId == null)
                .ToList();

            int count = housesToDelete.Count;
            if (count > 0)
            {
                _context.Houses.RemoveRange(housesToDelete);
                _context.SaveChanges();
                TempData["Success"] = $"🗑️ 批量清理成功！共删除了 {count} 套空置房屋。(已自动忽略您勾选的已售出房屋)";
            }
            else
            {
                TempData["Error"] = "删除失败！您勾选的房屋可能已被售出。";
            }

            return RedirectToAction("RoomList");
        }
        //  5. 沙盘图上快速绑定
        [HttpPost]
        public IActionResult QuickBind(int houseId, int ownerId, int buildingNo)
        {
            var house = _context.Houses.Find(houseId);
            var owner = _context.Owners.Find(ownerId);

            if (house != null && owner != null && house.OwnerId == null)
            {
                house.OwnerId = owner.Id;
                house.CertCode = null; // 既然管理员手动绑定了，就销毁凭证码
                _context.SaveChanges();

                // 同步更新业主表里的“房间号”文字展示
                var ownerHouses = _context.Houses.Where(h => h.OwnerId == owner.Id).ToList();
                string roomStr = string.Join(", ", ownerHouses.Select(h => $"{h.BuildingNo}栋{h.UnitNo}单元{h.RoomNo}"));

              
                if (roomStr.Length > 50)
                {
                    roomStr = roomStr.Substring(0, 47) + "..."; // 超过就截断并加上省略号
                }

                owner.RoomNo = roomStr;
                _context.SaveChanges();

                TempData["Success"] = $"🎉 成功将 {house.RoomNo} 绑定给业主 {owner.Name}！";
            }
            else
            {
                TempData["Error"] = "绑定失败！该房屋可能已被其他业主买走，或业主档案存在异常。";
            }

            // 绑定完直接跳回刚才看的那栋楼的沙盘
            return RedirectToAction("VisualBoard", new { buildingNo = buildingNo });
        }


    }
}
