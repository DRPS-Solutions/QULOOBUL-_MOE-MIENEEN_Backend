using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;


namespace QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration
{
    [Table("Meeting_Minutes")]
    public class MeetingMinutes
    {
        [Key] public int MinutesId { get; set; }
        public int MeetingId { get; set; }
        public int RecordedBy { get; set; }
        public DateTime TimeRecorded { get; set; }
        public string MinutesText { get; set; } = String.Empty;

        /// <summary>AI-generated minutes are a draft until a person approves them.</summary>
        public bool IsAiDraft { get; set; }
        public int? ApprovedBy { get; set; }

        public Meeting? Meeting { get; set; }
        [ForeignKey(nameof(RecordedBy))] public User? Recorder { get; set; }
        [ForeignKey(nameof(ApprovedBy))] public User? Approver { get; set; }
    }
}
