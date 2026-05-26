using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertySystem.Models
{
    public class CleanerTask
    {
        public int Id { get; set; }
        public int? CleanerId { get; set; }

        public string Location { get; set; }

        [DataType(DataType.Date)]
        public DateTime TaskDate { get; set; } = DateTime.Today;

        public string Status { get; set; } = "待打卡"; // 待打卡, 正常完成, 异常上报
        public DateTime? ScanTime { get; set; }

        public string? AbnormalNotes { get; set; }
        public string? AbnormalImage { get; set; }

        [ForeignKey("CleanerId")]
        public virtual User? Cleaner { get; set; }

        [NotMapped]
        public IFormFile? ImageFile { get; set; } // 用于接收手机拍照
        public string? TaskType { get; set; } = "保洁"; // "保洁" 或 "保安"
        [Display(Name = "巡更时段")]
        public string? Period { get; set; } = "全天";

    }
}
