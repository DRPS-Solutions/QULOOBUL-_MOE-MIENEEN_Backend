using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit
{
    [Table("Report")]
    public class Report
    {
        [Key] public int ReportId { get; set; }
        public int GeneratedBy { get; set; }

        /// <summary>The user the report is about, when it is a per-person report.</summary>
        public int? SubjectUserId { get; set; }
        [MaxLength(50)] public string ReportType { get; set; } = String.Empty;
        public DateTime DateGenerated { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        [MaxLength(500)] public string FilePath { get; set; } = String.Empty;

        [ForeignKey(nameof(GeneratedBy))] public User? Generator { get; set; }
        [ForeignKey(nameof(SubjectUserId))] public User? Subject { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//