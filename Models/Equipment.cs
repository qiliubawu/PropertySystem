using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class Equipment
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "请输入设备编号")]
        [Display(Name = "设备编号")]
        public string EquipmentNo { get; set; }

        [Required(ErrorMessage = "请输入设备名称")]
        [Display(Name = "设备名称")]
        public string Name { get; set; }

        [Required(ErrorMessage = "请选择设备类别")]
        [Display(Name = "类别")]
        public string Category { get; set; }

        [Required(ErrorMessage = "请输入安装位置")]
        [Display(Name = "安装位置")]
        public string Location { get; set; }

        [Display(Name = "运行状态")]
        public string Status { get; set; } = "正常运行";

        [Display(Name = "维保单位/联系人")]
        public string? Supplier { get; set; }

        [Display(Name = "采购日期")]
        [DataType(DataType.Date)]
        public DateTime PurchaseDate { get; set; } = DateTime.Today;

        [Display(Name = "上次保养日期")]
        [DataType(DataType.Date)]
        public DateTime? LastMaintenanceDate { get; set; }

        [Display(Name = "下次维保期限")]
        [DataType(DataType.Date)]
        public DateTime? NextMaintenanceDate { get; set; }

        // ====== 智能判断逻辑（不存入数据库） ======
        // 如果“下次保养日期”距离今天不到 30 天，或者已经过期，就返回 true（触发预警）
        [NotMapped]
        public bool IsMaintenanceUrgent
        {
            get
            {
                if (NextMaintenanceDate == null) return false;
                return (NextMaintenanceDate.Value - DateTime.Today).TotalDays <= 30;
            }
        }
    }
}
