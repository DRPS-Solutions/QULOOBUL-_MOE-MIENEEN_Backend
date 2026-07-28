using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models;

[Table("Request_Category")]
public class RequestCategory
{
    [Key] public int CategoryId { get; set; }
    [MaxLength(100)] public string CategoryName { get; set; } = "";
}

[Table("Resource_Request")]
public class ResourceRequest
{
    [Key] public int RequestId { get; set; }
    public int RequestedBy { get; set; }
    public int CategoryId { get; set; }
    public int? ReviewedBy { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "";
    [MaxLength(20)] public string Priority { get; set; } = "";
    public DateTime DateSubmitted { get; set; }

    [ForeignKey(nameof(RequestedBy))] public User? Requester { get; set; }
    [ForeignKey(nameof(CategoryId))] public RequestCategory? Category { get; set; }
    [ForeignKey(nameof(ReviewedBy))] public User? Reviewer { get; set; }
}

[Table("Request_Status_History")]
public class RequestStatusHistory
{
    [Key] public int HistoryId { get; set; }
    public int RequestId { get; set; }
    [MaxLength(30)] public string NewStatus { get; set; } = "";
    public int ChangedBy { get; set; }
    public DateTime TimeCreated { get; set; }

    [ForeignKey(nameof(RequestId))] public ResourceRequest? Request { get; set; }
    [ForeignKey(nameof(ChangedBy))] public User? ChangedByUser { get; set; }
}

[Table("Notification_Type")]
public class NotificationType
{
    [Key] public int TypeId { get; set; }
    [MaxLength(100)] public string TypeName { get; set; } = "";
}

[Table("Notification")]
public class Notification
{
    [Key] public int NotificationId { get; set; }
    public int UserId { get; set; }
    public int TypeId { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }

    // Polymorphic target — null for notifications not about a specific record.
    public int? EntityId { get; set; }
    [MaxLength(100)] public string? EntityType { get; set; }

    public User? User { get; set; }
    [ForeignKey(nameof(TypeId))] public NotificationType? Type { get; set; }
}

[Table("Audit_Log")]
public class AuditLog
{
    [Key] public int AuditId { get; set; }
    public int UserId { get; set; }
    [MaxLength(100)] public string Action { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public int EntityId { get; set; }
    [MaxLength(100)] public string EntityType { get; set; } = "";

    public User? User { get; set; }
}

[Table("Report")]
public class Report
{
    [Key] public int ReportId { get; set; }
    public int GeneratedBy { get; set; }

    /// <summary>The user the report is about, when it is a per-person report.</summary>
    public int? SubjectUserId { get; set; }
    [MaxLength(50)] public string ReportType { get; set; } = "";
    public DateTime DateGenerated { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    [MaxLength(500)] public string FilePath { get; set; } = "";

    [ForeignKey(nameof(GeneratedBy))] public User? Generator { get; set; }
    [ForeignKey(nameof(SubjectUserId))] public User? Subject { get; set; }
}
