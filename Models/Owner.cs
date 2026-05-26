using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class Owner
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "请输入业主姓名")]
        [Display(Name = "姓名")]
        public string Name { get; set; }

        [Required(ErrorMessage = "请选择性别")]
        [Display(Name = "性别")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "请输入年龄")]
        [Range(1, 150, ErrorMessage = "年龄必须在 1-150 之间")]
        [Display(Name = "年龄")]
        public int Age { get; set; }

        [Required(ErrorMessage = "请输入手机号")]
        [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "请输入正确的11位手机号码")]
        [Display(Name = "手机号(将作为登录账号)")]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        [Display(Name = "邮箱号")]
        public string? Email { get; set; }

        [Display(Name = "房间号(待绑定)")]
        public string? RoomNo { get; set; }

        [Display(Name = "车位号(待绑定)")]
        public string? ParkingNo { get; set; }

        [Required(ErrorMessage = "请输入住户人数")]
        [Range(1, 20, ErrorMessage = "住户人数至少为1人")]
        [Display(Name = "住户人数")]
        public int ResidentCount { get; set; } = 1;

        [Display(Name = "车牌号码(选填)")]
        public string? CarPlate { get; set; }

        // 👇 就是之前覆盖时不小心丢了这一行，现在补回来了！
        [Display(Name = "画像标签")]
        public string? Tags { get; set; }

        // 登录密码 (纯接收前端输入，不存入本表，只限制长度至少6位)
        [NotMapped]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "密码长度至少为 6 位字符")]
        [Display(Name = "登录密码")]
        public string? LoginPassword { get; set; }

        public int? UserId { get; set; }
    }
}
