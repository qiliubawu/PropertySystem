using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class MaterialRequest
    {
        public int Id { get; set; }
        public int StaffId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; } = "待处理";
        public DateTime RequestTime { get; set; } = DateTime.Now;
        public DateTime? ProcessTime { get; set; }
        public string? AdminReply { get; set; }

        [ForeignKey("StaffId")]
        public virtual User? Staff { get; set; }
    }
}
