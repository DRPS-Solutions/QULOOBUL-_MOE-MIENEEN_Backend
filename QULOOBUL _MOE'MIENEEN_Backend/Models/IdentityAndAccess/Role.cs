using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess
{
    [Table("Role")]
    public class Role
    {
        [Key] public int RoleId { get; set; }
        [MaxLength(100)] public string RoleName { get; set; } = String.Empty;

        /// <summary>Lower number = higher authority. Tasks may be assigned down, never up.</summary>
        public int HierarchyLevel { get; set; }
    }
}
