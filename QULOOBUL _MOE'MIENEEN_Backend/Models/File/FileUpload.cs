using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.File
{
    [Table("File_Upload")]
    public class FileUpload
    {
        [Key] public int FileId { get; set; }
        public int CategoryId { get; set; }
        public int UploadedBy { get; set; }
        public int? TaskId { get; set; }
        public int? MeetingId { get; set; }
        [MaxLength(260)] public string FileName { get; set; } = String.Empty;
        [MaxLength(100)] public string FileType { get; set; } = String.Empty;
        [MaxLength(500)] public string FilePath { get; set; } = String.Empty;

        /// <summary>Set for monthly reports filed by period rather than attached to a task.</summary>
        public DateTime? PeriodMonth { get; set; }

        [ForeignKey(nameof(CategoryId))] public FileCategory? Category { get; set; }
        [ForeignKey(nameof(UploadedBy))] public User? Uploader { get; set; }
        [ForeignKey(nameof(TaskId))] public ProjectTask? Task { get; set; }
        [ForeignKey(nameof(MeetingId))] public Meeting? Meeting { get; set; }
    }
}
