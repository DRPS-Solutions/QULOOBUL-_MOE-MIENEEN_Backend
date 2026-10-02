using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace QULOOBUL__MOE_MIENEEN_Shared.Auth
{
    internal class AuthDtos
    {
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record LoginRequest
    {
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; init; } = string.Empty;
        [Required, MaxLength(128)]
        public string Password { get; init; } = string.Empty;
    }
//----------------------------------------------------------------------------------------------------------------------//
    public record AuthResponse(
        string Token,
        DateTime ExpiresAtUtc,
        int UserId,
        string FullName,
        string Role,
        IReadOnlyList<string> Permissions,
        bool MustChangePassword = false);
//----------------------------------------------------------------------------------------------------------------------//
    public record PasswordResetRequest
    {
        [Required, EmailAddress] 
        public string Email { get; init; } = string.Empty;
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>"Forgot password?": who is asking, so the admin knows which account to reset.</summary>
    public record ForgotPasswordRequest
    {
        [Required, MaxLength(100)]
        public string FirstName { get; init; } = string.Empty;
        [Required, MaxLength(100)]
        public string Surname { get; init; } = string.Empty;
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; init; } = string.Empty;
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>SM-03 setPassword: redeems the emailed activation or reset token.</summary>
    public record SetPasswordRequest
    {
        [Required, MaxLength(100)]
        public string Token { get; init; } = string.Empty;
        [Required, MinLength(8), MaxLength(128)]
        public string Password { get; init; } = string.Empty;
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>A signed-in user replaces their password (required after an admin's temporary one).</summary>
    public record ChangePasswordRequest
    {
        [Required, MaxLength(128)]
        public string CurrentPassword { get; init; } = string.Empty;
        [Required, MinLength(8), MaxLength(128)]
        public string NewPassword { get; init; } = string.Empty;
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>Shown once to the admin, who passes it on to the user in person or by phone.</summary>
    public record TemporaryPasswordDto(string TemporaryPassword);

}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//