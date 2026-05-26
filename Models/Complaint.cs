using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class Complaint
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }

        [Required(ErrorMessage = "请输入投诉/建议标题")]
        public string Title { get; set; }

        [Required(ErrorMessage = "请输入具体内容")]
        public string Content { get; set; }

        public DateTime CreateTime { get; set; } = DateTime.Now;
        public string Status { get; set; } = "待处理";
        public string? Reply { get; set; }

        [ForeignKey("OwnerId")]
        public virtual Owner? Owner { get; set; }
    }
}
