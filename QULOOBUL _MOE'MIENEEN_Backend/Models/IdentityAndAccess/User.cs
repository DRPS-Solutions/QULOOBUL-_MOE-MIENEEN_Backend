using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess
{
    [Table("User")]
    public class User
    {
        [Key] public int UserId { get; set; }
        public int RoleId { get; set; }
        [MaxLength(100)] public string FirstName { get; set; } = String.Empty;
        [MaxLength(100)] public string Surname { get; set; } = String.Empty;
        [MaxLength(256)] public string Email { get; set; } = String.Empty;
        [MaxLength(20)] public string CellNo { get; set; } = String.Empty;
        [MaxLength(256)] public string PasswordHash { get; set; } = String.Empty;
        public bool IsActive { get; set; } = true;

        public Role? Role { get; set; }
    }
}
