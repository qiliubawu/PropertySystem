using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http; // 必须引用这个才能处理 IFormFile

namespace PropertySystem.Models
{
    public class RepairRequest
    {
        public int Id { get; set; }

        public int OwnerId { get; set; }
        public int? WorkerId { get; set; }

        [Required(ErrorMessage = "请选择报修类型")]
        [Display(Name = "报修类型")]
        public string RepairType { get; set; }

        [Required(ErrorMessage = "请填写故障描述")]
        [Display(Name = "故障描述")]
        public string Description { get; set; }

        [Display(Name = "报修照片")]
        public string? BeforeImage { get; set; }

        public string Status { get; set; } = "待接单"; // 待接单, 维修中, 待确认, 已完成

        public DateTime CreateTime { get; set; } = DateTime.Now;
        public DateTime? AcceptTime { get; set; }

        public string? AfterImage { get; set; }
        public string? RepairNotes { get; set; }
        public DateTime? FinishTime { get; set; }

        public int? Rating { get; set; }
        public string? Evaluation { get; set; }
        public DateTime? ConfirmTime { get; set; }

        // ==========================================
        // 👇 就是缺了下面这个字段导致的报错！
        // ==========================================
        [Display(Name = "物料消耗成本")]
        public decimal? MaterialCost { get; set; } = 0;

        // 导航属性
        [ForeignKey("OwnerId")]
        public virtual Owner? Owner { get; set; }

        [ForeignKey("WorkerId")]
        public virtual User? Worker { get; set; }

        // 以下属性不映射到数据库，仅用于接收前端上传的图片文件
        [NotMapped]
        public IFormFile? BeforeImageFile { get; set; }

        [NotMapped]
        public IFormFile? AfterImageFile { get; set; }
    }
}
