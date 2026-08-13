using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models;

[Table("Notification_Type")]
public class NotificationType
{
    [Key] public int TypeId { get; set; }
    [MaxLength(100)] public string TypeName { get; set; } = String.Empty;
}

[Table("Notification")]
public class Notification
{
    [Key] public int NotificationId { get; set; }
    public int UserId { get; set; }
    public int TypeId { get; set; }
    public string Message { get; set; } = String.Empty;
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
    [MaxLength(100)] public string Action { get; set; } = String.Empty;
    public DateTime Timestamp { get; set; }
    public int EntityId { get; set; }
    [MaxLength(100)] public string EntityType { get; set; } = String.Empty;

    public User? User { get; set; }
}

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
