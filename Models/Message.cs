using System.ComponentModel.DataAnnotations.Schema;
namespace PropertySystem.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreateTime { get; set; } = DateTime.Now;
        public string Type { get; set; } = "系统通知";

        [ForeignKey("OwnerId")] public virtual Owner? Owner { get; set; }
    }
}
