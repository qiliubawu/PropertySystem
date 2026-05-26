using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class Visitor
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "请输入姓名")]
        [Display(Name = "访客姓名")]
        public string Name { get; set; }

        [Required(ErrorMessage = "请选择性别")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "请输入年龄")]
        public int Age { get; set; }

        [Required(ErrorMessage = "请输入手机号")]
        [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "手机号格式不正确")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "请输入来访目的")]
        public string Purpose { get; set; }

        // ================= 新增字段 =================
        [Display(Name = "车牌号(选填)")]
        public string? CarPlate { get; set; }

        [Required(ErrorMessage = "请选择预计到达时间")]
        public DateTime StartTime { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "请选择预计离开时间")]
        public DateTime EndTime { get; set; } = DateTime.Now.AddHours(2); // 默认有效2小时
        // ==========================================

        public DateTime VisitTime { get; set; } = DateTime.Now; // 表单提交时间
        public DateTime? LeaveTime { get; set; } // 保安手动提前签出的时间

        // 数据库里存的基础状态 (只有"访问中"或"已人工签出")
        public string Status { get; set; } = "访问中";

        // ⭐ 核心黑科技：不存入数据库，每次读取时根据当前时间自动计算状态！
        [NotMapped]
        public string DynamicStatus
        {
            get
            {
                if (Status == "已人工签出") return "已人工签出";

                var now = DateTime.Now;
                if (now < StartTime) return "未生效 (未到时间)";
                if (now >= StartTime && now <= EndTime) return "访问中 (通行有效)";
                return "已过期 (自动注销)"; // 时间超过了 EndTime，自动失效
            }
        }
    }
}
