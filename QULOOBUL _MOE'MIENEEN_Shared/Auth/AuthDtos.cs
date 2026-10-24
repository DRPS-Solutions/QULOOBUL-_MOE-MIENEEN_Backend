using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace QULOOBUL__MOE_MIENEEN_Shared.Auth
{
    /// <summary>
    /// Request/response contracts for UC-01. The data annotations are validated by [ApiController]
    /// before an action runs, and by the clients' forms before a request is sent.
    /// Reference: Microsoft, "Model validation in ASP.NET Core MVC and Razor Pages." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation
    /// </summary>
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
        /// <summary>The mobile app asks for a refresh token so it stays signed in; the web client does not.</summary>
        public bool IssueRefreshToken { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>RefreshToken is set only when the request asked for one (and never while a temporary password is pending).</summary>
    public record AuthResponse(
        string Token,
        DateTime ExpiresAtUtc,
        int UserId,
        string FullName,
        string Role,
        IReadOnlyList<string> Permissions,
        bool MustChangePassword = false,
        string? RefreshToken = null,
        DateTime? RefreshExpiresAtUtc = null);
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>Swaps a refresh token for a new access token and a new refresh token (the old one is spent);
    /// also the body of sign-out, which revokes it.</summary>
    public record RefreshRequest
    {
        [Required, MaxLength(100)]
        public string RefreshToken { get; init; } = string.Empty;
    }
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
        /// <summary>As on LoginRequest: the mobile app finishing a temporary-password sign-in asks for one.</summary>
        public bool IssueRefreshToken { get; init; }
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>Shown once to the admin, who passes it on to the user in person or by phone.</summary>
    public record TemporaryPasswordDto(string TemporaryPassword);

}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//