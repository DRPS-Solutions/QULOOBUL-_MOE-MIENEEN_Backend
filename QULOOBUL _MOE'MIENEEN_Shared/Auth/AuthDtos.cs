using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace QULOOBUL__MOE_MIENEEN_Shared.Auth
{
    internal class AuthDtos
    {
    }
    public record LoginRequest
    {
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; init; } = string.Empty;
        [Required, MaxLength(128)]
        public string Password { get; init; } = string.Empty;
    }

    public record AuthResponse(
        string Token,
        DateTime ExpiresAtUtc,
        int UserId,
        string FullName,
        string Role,
        IReadOnlyList<string> Permissions);

    public record PasswordResetRequest
    {
        [Required, EmailAddress] 
        public string Email { get; init; } = string.Empty;
    }


}
