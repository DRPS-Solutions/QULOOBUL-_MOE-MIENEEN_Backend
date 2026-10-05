namespace QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess
{
    /// <summary>A row of vw_UserDirectory: what colleagues may know about each other, and nothing
    /// that could authenticate as them (no hash, no set-password token).</summary>
    public class UserDirectoryEntry
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = String.Empty;
        public string Email { get; set; } = String.Empty;
        public string RoleName { get; set; } = String.Empty;
        public bool IsActive { get; set; }
        public string Status { get; set; } = String.Empty;
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
