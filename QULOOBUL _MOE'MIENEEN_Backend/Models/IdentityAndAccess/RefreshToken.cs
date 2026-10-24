using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess
{
    /// <summary>
    /// A mobile sign-in that outlives the 60-minute access token. Single use: each refresh spends the row and
    /// issues a new one, and presenting a spent row again is treated as theft and revokes them all.
    /// </summary>
    [Table("Refresh_Token")]
    public class RefreshToken
    {
        [Key] public int RefreshTokenId { get; set; }
        public int UserId { get; set; }
        /// <summary>SHA-256 of the token; the plaintext is only ever on the phone.</summary>
        [MaxLength(64)] public string TokenHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        /// <summary>When it was swapped for a new one. Kept until it expires so a replay can be spotted.</summary>
        public DateTime? UsedAt { get; set; }

        public User? User { get; set; }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
