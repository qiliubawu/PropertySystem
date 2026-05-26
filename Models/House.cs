using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace PropertySystem.Models
{
    public class House
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "请输入楼栋号")]
        [Range(1, 25, ErrorMessage = "楼栋号必须在 1 到 25 之间")]
        [Display(Name = "楼栋")]
        public int BuildingNo { get; set; }

        [Required(ErrorMessage = "请输入单元号")]
        [RegularExpression("^[AB]$", ErrorMessage = "单元号只能是大写的 A 或 B")]
        [Display(Name = "单元号")]
        public string UnitNo { get; set; }

        [Required(ErrorMessage = "请输入楼层")]
        [RegularExpression("^([1-9]|[1-2][0-9]|30)$", ErrorMessage = "楼层只能是 1-30 的数字")]
        [Display(Name = "楼层")]
        public string Floor { get; set; }


        [Required(ErrorMessage = "请输入房间号")]
        [Display(Name = "房间号(每层4户)")]
        public string RoomNo { get; set; }

        // 以下为固定值属性
        [Display(Name = "户型")]
        public string Layout { get; set; } = "三室一厅";

        [Display(Name = "面积(㎡)")]
        public decimal Area { get; set; } = 120.0m;

        [Display(Name = "电梯数")]
        public int Elevators { get; set; } = 2;

        [Display(Name = "监控数")]
        public int Cameras { get; set; } = 3;

        [Display(Name = "楼梯数")]
        public int Stairs { get; set; } = 2;

        [Display(Name = "消防设备")]
        public int FireEquipments { get; set; } = 2;
        // 放在 House.cs 和 ParkingSpace.cs 的最下面
        [Display(Name = "专属凭证码")]
        public string? CertCode { get; set; }

        public int? OwnerId { get; set; }

        // 👇 加上这两行“导航属性”，建立表与表之间的桥梁
        [ForeignKey("OwnerId")]
        public virtual Owner? Owner { get; set; }
    }
}


