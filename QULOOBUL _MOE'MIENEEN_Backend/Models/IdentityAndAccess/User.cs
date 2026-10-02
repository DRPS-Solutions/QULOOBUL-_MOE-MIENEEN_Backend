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
        /// <summary>Kept equal to Status == "Active" by Lifecycle.MoveUser.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>SM-03 state: Pending Activation, Active, Locked, Inactive or Anonymised.</summary>
        [MaxLength(30)] public string Status { get; set; } = "Active";
        /// <summary>Consecutive failed logins; the third locks the account.</summary>
        public int FailedLoginCount { get; set; }
        /// <summary>SHA-256 of the one-time set-password token emailed on activation or reset.</summary>
        [MaxLength(64)] public string? PasswordTokenHash { get; set; }
        public DateTime? PasswordTokenExpires { get; set; }
        /// <summary>Start of the POPIA retention clock that gates anonymise().</summary>
        public DateTime? DeactivatedAt { get; set; }
        /// <summary>Set when an admin issues a temporary password; the user must replace it before using the app.</summary>
        public bool MustChangePassword { get; set; }

        public Role? Role { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
