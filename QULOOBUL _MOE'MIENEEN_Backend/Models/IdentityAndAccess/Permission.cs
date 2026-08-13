using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess
{
    [Table("Permission")]
    public class Permission
    {
        [Key] public int PermissionId { get; set; }
        [MaxLength(100)] public string PermissionName { get; set; } = String.Empty;
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//