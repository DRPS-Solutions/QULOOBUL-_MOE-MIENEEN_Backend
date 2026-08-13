using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit
{
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
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//