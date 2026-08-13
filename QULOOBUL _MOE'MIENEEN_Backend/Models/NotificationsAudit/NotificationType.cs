using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit
{
    [Table("Notification_Type")]
    public class NotificationType
    {
        [Key] public int TypeId { get; set; }
        [MaxLength(100)] public string TypeName { get; set; } = String.Empty;
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//