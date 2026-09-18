using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Auth;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-01: validate credentials, issue a JWT carrying role + permission claims.</summary>
    public class AuthService(AppDbContext db, JwtTokenFactory tokens, AuditService audit, ILogger<AuthService> log)
    {
        private static readonly PasswordHasher<User> Hasher = new();

        public const string FailedAction = "auth.login.failed";
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<AuthResponse> LoginAsync(LoginRequest req)
        {
            var user = await db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == req.Email);

            if (user is null)
            {
                // SD-01 writes LOGIN_FAILED against a user id; an unknown address has none, and
                // Audit_Log's FK must resolve to a real person. Logged instead of audited.
                log.LogWarning("Login attempt for unknown email address");
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            if (!user.IsActive)
            {
                await RecordFailureAsync(user.UserId);
                throw new UnauthorizedAccessException("This account is deactivated.");
            }

            if (Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password)
                == PasswordVerificationResult.Failed)
            {
                // SD-01, alt [valid = false]. Also the input SM-03's lockout guard will count once
                // User carries a failure counter.
                await RecordFailureAsync(user.UserId);
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            var permissions = await (
                from rp in db.RolePermissions
                join p in db.Permissions on rp.PermissionId equals p.PermissionId
                where rp.RoleId == user.RoleId
                select p.PermissionName).ToListAsync();

            var roleName = user.Role?.RoleName ?? "Staff";
            var (token, expires) = tokens.Create(user, roleName, permissions);

            await audit.RecordAndSaveAsync(user.UserId, "auth.login", nameof(User), user.UserId);
            return new AuthResponse(token, expires, user.UserId,
                $"{user.FirstName} {user.Surname}", roleName, permissions);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Used by CreateUserRequest / password reset — hash for storage.</summary>
        public string Hash(User user, string plain) => Hasher.HashPassword(user, plain);
//----------------------------------------------------------------------------------------------------------------------//
        private Task RecordFailureAsync(int userId) =>
            audit.RecordAndSaveAsync(userId, FailedAction, nameof(User), userId);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//