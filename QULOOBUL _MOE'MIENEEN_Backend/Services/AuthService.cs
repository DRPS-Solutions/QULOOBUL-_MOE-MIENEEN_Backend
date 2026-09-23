using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Auth;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-01: validate credentials, issue a JWT carrying role + permission claims.
    /// Owns SM-03's login-driven transitions: Active → Locked and Pending Activation → Active.</summary>
    public class AuthService(
        AppDbContext db, JwtTokenFactory tokens, AuditService audit, NotificationService notify,
        EmailService email, IConfiguration config, ILogger<AuthService> log)
    {
        private static readonly PasswordHasher<User> Hasher = new();

        public const string FailedAction = "auth.login.failed";
        public const int MaxFailedLogins = 3;
        public static readonly TimeSpan PasswordTokenLifetime = TimeSpan.FromDays(3);
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

            // SM-03 note: pending, locked and deactivated are all "not active" but are told apart at login.
            if (!user.IsActive)
            {
                await RecordFailureAsync(user.UserId);
                throw new UnauthorizedAccessException(user.Status switch
                {
                    Lifecycle.UserPending => "This account has not been activated. Use the link in your activation email.",
                    Lifecycle.UserLocked => "This account is locked after too many failed sign-ins. Ask an admin to reset your password.",
                    _ => "This account is deactivated.",
                });
            }

            if (Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password)
                == PasswordVerificationResult.Failed)
            {
                // SD-01, alt [valid = false]; SM-03 Authenticate [3 consecutive failures] → Locked.
                user.FailedLoginCount++;
                audit.Record(user.UserId, FailedAction, nameof(User), user.UserId);
                if (user.FailedLoginCount >= MaxFailedLogins) await LockAsync(user);
                else await db.SaveChangesAsync();
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            var permissions = await (
                from rp in db.RolePermissions
                join p in db.Permissions on rp.PermissionId equals p.PermissionId
                where rp.RoleId == user.RoleId
                select p.PermissionName).ToListAsync();

            var roleName = user.Role?.RoleName ?? "Staff";
            var (token, expires) = tokens.Create(user, roleName, permissions);

            user.FailedLoginCount = 0; // "consecutive": a success resets the count
            await audit.RecordAndSaveAsync(user.UserId, "auth.login", nameof(User), user.UserId);
            return new AuthResponse(token, expires, user.UserId,
                $"{user.FirstName} {user.Surname}", roleName, permissions);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 setPassword [password meets policy]: redeem an emailed one-time token.
        /// Activates a pending account; for an active one it completes an admin reset.</summary>
        public async Task SetPasswordAsync(SetPasswordRequest req)
        {
            if (!req.Password.Any(char.IsLetter) || !req.Password.Any(char.IsDigit))
                throw new ArgumentException("Password must be at least 8 characters with a letter and a digit.");

            var hash = HashToken(req.Token);
            var user = await db.Users.FirstOrDefaultAsync(u => u.PasswordTokenHash == hash)
                ?? throw new UnauthorizedAccessException("This link is invalid or has already been used.");
            if (user.PasswordTokenExpires < DateTime.UtcNow)
                throw new UnauthorizedAccessException("This link has expired. Ask an admin to send a new one.");

            if (user.Status == Lifecycle.UserPending) Lifecycle.MoveUser(user, Lifecycle.UserActive);
            else if (user.Status != Lifecycle.UserActive)
                throw new InvalidOperationException("This account cannot set a password in its current state.");

            user.PasswordHash = Hash(user, req.Password);
            user.PasswordTokenHash = null;
            user.PasswordTokenExpires = null;
            user.FailedLoginCount = 0;
            await audit.RecordAndSaveAsync(user.UserId, "user.password.set", nameof(User), user.UserId);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Put a fresh one-time set-password token on <paramref name="user"/> (caller saves) and
        /// return the plaintext for the email. Only its hash is stored.</summary>
        public static string NewPasswordToken(User user)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.PasswordTokenHash = HashToken(token);
            user.PasswordTokenExpires = DateTime.UtcNow + PasswordTokenLifetime;
            return token;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Email the set-password link. Call after the token has been saved.</summary>
        public Task SendPasswordLinkAsync(User user, string token, bool activation)
        {
            var link = $"{config["App:BaseUrl"]?.TrimEnd('/')}/set-password?token={token}";
            return activation
                ? email.SendAccountEmailAsync(user.Email, "Activate your account",
                    $"An account was created for you. Set your password to activate it: {link}")
                : email.SendAccountEmailAsync(user.Email, "Your password was reset",
                    $"An admin reset your password. Choose a new one here: {link}");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Used by password set / reset — hash for storage.</summary>
        public string Hash(User user, string plain) => Hasher.HashPassword(user, plain);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 Active → Locked "/ write AuditLog, notify admin".</summary>
        private async Task LockAsync(User user)
        {
            Lifecycle.MoveUser(user, Lifecycle.UserLocked);
            audit.Record(user.UserId, "user.locked", nameof(User), user.UserId);

            var admins = await (
                from u in db.Users
                join rp in db.RolePermissions on u.RoleId equals rp.RoleId
                join p in db.Permissions on rp.PermissionId equals p.PermissionId
                where u.IsActive && p.PermissionName == Permissions.ManageUsers
                select u.UserId).ToListAsync();
            var notices = new List<Notification>();
            foreach (var adminId in admins)
                notices.Add(await notify.QueueAsync(adminId, "UserLocked",
                    $"{user.FirstName} {user.Surname}'s account was locked after {MaxFailedLogins} failed sign-ins.",
                    nameof(User), user.UserId));

            await db.SaveChangesAsync();
            foreach (var n in notices)
            {
                await notify.PushAsync(n.UserId, "userLocked", new { userId = user.UserId });
                await email.SendAsync(n, "A user account was locked");
            }
        }
//----------------------------------------------------------------------------------------------------------------------//
        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
//----------------------------------------------------------------------------------------------------------------------//
        private Task RecordFailureAsync(int userId) =>
            audit.RecordAndSaveAsync(userId, FailedAction, nameof(User), userId);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
