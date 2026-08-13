using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit
{
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
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//