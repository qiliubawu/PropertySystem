using System.ComponentModel.DataAnnotations;

namespace PropertySystem.Models
{
    public class Announcement
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "请输入公告标题")]
        [MaxLength(100, ErrorMessage = "标题最长不能超过100个字符")]
        [Display(Name = "公告标题")]
        public string Title { get; set; }

        [Required(ErrorMessage = "请输入公告内容")]
        [Display(Name = "公告正文")]
        public string Content { get; set; }

        [Display(Name = "发布时间")]
        public DateTime PublishTime { get; set; } = DateTime.Now;

        [Display(Name = "发布人")]
        public string? Publisher { get; set; } // 后台代码自动填充

        [Display(Name = "是否置顶/紧急")]
        public bool IsTop { get; set; } = false;
        [Display(Name = "公告分类")]
        public string? Type { get; set; } = "普通通知";

    }
}
