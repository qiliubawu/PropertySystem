using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class Bill
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "业主ID")]
        public int OwnerId { get; set; }

        [Required(ErrorMessage = "请选择费用类型")]
        [Display(Name = "费用类型")]
        public string FeeType { get; set; } // 物业费, 停车费, 水电费

        [Display(Name = "账单金额(元)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "请选择账单月份")]
        [Display(Name = "账单月份")]
        public string BillingMonth { get; set; }

        [Display(Name = "缴费状态")]
        public bool IsPaid { get; set; } = false;

        [Display(Name = "生成时间")]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        [Display(Name = "计算明细")]
        public string? Remark { get; set; }

        // 导航属性，方便在代码里直接调用业主的名字
        [ForeignKey("OwnerId")]
        public virtual Owner? Owner { get; set; }
        [NotMapped]
        public decimal LateFee { get; set; } = 0;

    }
}
