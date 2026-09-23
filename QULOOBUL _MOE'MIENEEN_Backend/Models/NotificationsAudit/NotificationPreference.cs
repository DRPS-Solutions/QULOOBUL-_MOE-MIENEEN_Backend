using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit
{
    /// <summary>
    /// UC-08 per-type opt-out (NotificationType: "allowing ... user-level preferences per type").
    /// No row means enabled, so a brand-new type reaches everyone until they switch it off.
    /// </summary>
    [Table("Notification_Preference")]
    public class NotificationPreference
    {
        [Key] public int PreferenceId { get; set; }
        public int UserId { get; set; }
        public int TypeId { get; set; }
        public bool IsEnabled { get; set; } = true;

        public User? User { get; set; }
        [ForeignKey(nameof(TypeId))] public NotificationType? Type { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
