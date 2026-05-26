using System.ComponentModel.DataAnnotations;

namespace PropertySystem.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "请输入登录账号")]
        [Display(Name = "登录账号(工号/手机)")]
        public string Username { get; set; }

        [Required(ErrorMessage = "请输入登录密码")]
        [Display(Name = "登录密码")]
        public string Password { get; set; }

        [Required(ErrorMessage = "请输入员工姓名")]
        [Display(Name = "员工姓名")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "请选择岗位角色")]
        [Display(Name = "系统角色")]
        public string Role { get; set; }
    }
}
