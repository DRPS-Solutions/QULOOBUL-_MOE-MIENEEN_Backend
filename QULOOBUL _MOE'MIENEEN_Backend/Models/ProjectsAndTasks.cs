using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models;









[Table("Task_Comment")]
public class TaskComment
{
    [Key] public int CommentId { get; set; }
    public int TaskId { get; set; }
    public int UserId { get; set; }
    public string Comment { get; set; } = String.Empty;
    public DateTime TimeCreated { get; set; }

    [ForeignKey(nameof(TaskId))] public ProjectTask? Task { get; set; }
    public User? User { get; set; }
}

[Table("Task_Status_History")]
public class TaskStatusHistory
{
    [Key] public int HistoryId { get; set; }
    public int TaskId { get; set; }
    [MaxLength(30)] public string NewStatus { get; set; } = "";
    public int ChangedBy { get; set; }
    public DateTime TimeCreated { get; set; }

    [ForeignKey(nameof(TaskId))] public ProjectTask? Task { get; set; }
    [ForeignKey(nameof(ChangedBy))] public User? ChangedByUser { get; set; }
}

/// <summary>Recurring weekly task template — Saturday's tasks differ from Friday's.</summary>
[Table("Task_Schedule")]
public class TaskSchedule
{
    [Key] public int ScheduleId { get; set; }
    public int CreatedBy { get; set; }
    public int AssignedTo { get; set; }
    [MaxLength(200)] public string Title { get; set; } = String.Empty;
    [MaxLength(10)] public string DayOfWeek { get; set; } = String.Empty;
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(CreatedBy))] public User? Creator { get; set; }
    [ForeignKey(nameof(AssignedTo))] public User? Assignee { get; set; }
}

[Table("File_Category")]
public class FileCategory
{
    [Key] public int CategoryId { get; set; }
    [MaxLength(100)] public string CategoryName { get; set; } = String.Empty;
}

[Table("File_Upload")]
public class FileUpload
{
    [Key] public int FileId { get; set; }
    public int CategoryId { get; set; }
    public int UploadedBy { get; set; }
    public int? TaskId { get; set; }
    public int? MeetingId { get; set; }
    [MaxLength(260)] public string FileName { get; set; } = "";
    [MaxLength(100)] public string FileType { get; set; } = "";
    [MaxLength(500)] public string FilePath { get; set; } = "";

    /// <summary>Set for monthly reports filed by period rather than attached to a task.</summary>
    public DateTime? PeriodMonth { get; set; }

    [ForeignKey(nameof(CategoryId))] public FileCategory? Category { get; set; }
    [ForeignKey(nameof(UploadedBy))] public User? Uploader { get; set; }
    [ForeignKey(nameof(TaskId))] public ProjectTask? Task { get; set; }
    [ForeignKey(nameof(MeetingId))] public Meeting? Meeting { get; set; }
}
