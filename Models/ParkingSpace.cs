using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class ParkingSpace
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "请输入车位号")]
        [Display(Name = "车位编号")]
        public string ParkingNo { get; set; }

        [Required(ErrorMessage = "请输入车位位置")]
        [Display(Name = "所在位置")]
        public string Location { get; set; }

        [Display(Name = "面积(㎡)")]
        public decimal Area { get; set; } = 12.0m;

        [Display(Name = "车位类型")]
        public string Type { get; set; } = "产权车位";

        [Display(Name = "绑定状态")]
        public string Status { get; set; } = "未绑定";
        // 放在 House.cs 和 ParkingSpace.cs 的最下面
        [Display(Name = "专属凭证码")]
        public string? CertCode { get; set; }

        public int? OwnerId { get; set; }
        [ForeignKey("OwnerId")]
        public virtual Owner? Owner { get; set; }

    }
}
