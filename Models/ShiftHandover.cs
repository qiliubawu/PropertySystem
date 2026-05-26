using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class ShiftHandover
    {
        public int Id { get; set; }
        public int FromCleanerId { get; set; }

        [Required(ErrorMessage = "请输入接班人姓名")]
        public string ToCleanerName { get; set; }

        public DateTime HandoverTime { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "请输入需要交接的事项")]
        public string LeftoverIssues { get; set; }

        [ForeignKey("FromCleanerId")]
        public virtual User? FromCleaner { get; set; }
        public string? HandoverType { get; set; } = "保洁"; // "保洁" 或 "保安"

    }
}
