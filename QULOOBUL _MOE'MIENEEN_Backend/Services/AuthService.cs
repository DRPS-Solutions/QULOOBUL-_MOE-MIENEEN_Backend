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
    /// Owns SM-03's login-driven transitions: Active → Locked and Pending Activation → Active.
    /// Reference: OWASP Foundation, "OWASP Top 10:2025." Accessed: Aug. 08, 2026. [Online]. Available: https://owasp.org/Top10/2025/
    /// Reference: M. Jones, J. Bradley, and N. Sakimura, "JSON Web Token (JWT)," Internet Engineering Task Force (IETF), RFC 7519, May 2015. Accessed: Aug. 08, 2026. [Online]. Available: https://www.rfc-editor.org/info/rfc7519
    /// </summary>
    public class AuthService(
        AppDbContext db, JwtTokenFactory tokens, AuditService audit, NotificationService notify,
        EmailService email, IConfiguration config, ILogger<AuthService> log)
    {
        // Salted PBKDF2 hashes; the plaintext password is never stored.
        // Reference: Microsoft, "PasswordHasher<TUser> Class." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1
        private static readonly PasswordHasher<User> Hasher = new();

        public const string FailedAction = "auth.login.failed";
        public const int MaxFailedLogins = 5;
        public static readonly TimeSpan PasswordTokenLifetime = TimeSpan.FromDays(3);
        public static readonly TimeSpan LockoutPeriod = TimeSpan.FromMinutes(15);

        /// <summary>Audit action for a new password; a token issued before the latest one is refused (Program.cs).</summary>
        public const string PasswordSetAction = "user.password.set";

        /// <summary>
        /// Every sign-in failure says this, whether the email is unknown, the password wrong, or the account
        /// pending, locked or deactivated: telling them apart would let anyone probe which emails have accounts
        /// and what state they are in. It explains the lock so a locked-out user is not left guessing.
        /// </summary>
        public static readonly string LoginFailed =
            $"Invalid email or password. {MaxFailedLogins} failed attempts lock an account for {LockoutPeriod.TotalMinutes:0} minutes; use Forgot password to ask an admin for help.";

        // Checked against when the email is unknown, so that case costs as much time as a real attempt.
        private static readonly string DummyHash = Hasher.HashPassword(new User(), "not-a-real-password");
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<AuthResponse> LoginAsync(LoginRequest req)
        {
            var user = await db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == req.Email);

            if (user is null)
            {
                // SD-01 writes LOGIN_FAILED against a user id; an unknown address has none, and
                // Audit_Log's FK must resolve to a real person. Logged instead of audited.
                Hasher.VerifyHashedPassword(new User(), DummyHash, req.Password);
                log.LogWarning("Login attempt for unknown email address");
                throw new UnauthorizedAccessException(LoginFailed);
            }

            // Anyone who knows an email can lock that account with failed sign-ins, so the lock lifts itself;
            // otherwise the sole admin could be locked out for good.
            if (user.Status == Lifecycle.UserLocked && await LockedAtAsync(user.UserId) is { } at
                && DateTime.UtcNow - at >= LockoutPeriod)
            {
                Lifecycle.MoveUser(user, Lifecycle.UserActive);
                user.FailedLoginCount = 0;
                audit.Record(user.UserId, "user.unlocked", nameof(User), user.UserId);
            }

            // Verified even for an inactive account (and never acted on), so timing does not reveal its state.
            var passwordOk = Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password)
                != PasswordVerificationResult.Failed;

            // SM-03 note: pending, locked and deactivated are all "not active"; the caller is told nothing more.
            if (!user.IsActive)
            {
                await RecordFailureAsync(user.UserId);
                throw new UnauthorizedAccessException(LoginFailed);
            }

            if (!passwordOk)
            {
                // SD-01, alt [valid = false]; SM-03 Authenticate [5 consecutive failures] → Locked.
                user.FailedLoginCount++;
                audit.Record(user.UserId, FailedAction, nameof(User), user.UserId);
                if (user.FailedLoginCount >= MaxFailedLogins) await LockAsync(user);
                else await db.SaveChangesAsync();
                throw new UnauthorizedAccessException(LoginFailed);
            }

            var response = await IssueAsync(user);
            user.FailedLoginCount = 0; // "consecutive": a success resets the count
            await audit.RecordAndSaveAsync(user.UserId, "auth.login", nameof(User), user.UserId);
            return response;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>The signed token and the user details a client needs, for sign-in and for a password change.</summary>
        private async Task<AuthResponse> IssueAsync(User user)
        {
            var permissions = await (
                from rp in db.RolePermissions
                join p in db.Permissions on rp.PermissionId equals p.PermissionId
                where rp.RoleId == user.RoleId
                select p.PermissionName).ToListAsync();

            var roleName = user.Role?.RoleName ?? "Staff";
            var (token, expires) = tokens.Create(user, roleName, permissions);
            return new AuthResponse(token, expires, user.UserId,
                $"{user.FirstName} {user.Surname}", roleName, permissions, user.MustChangePassword);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 setPassword [password meets policy]: redeem an emailed one-time token.
        /// Activates a pending account; for an active one it completes an admin reset.</summary>
        public async Task SetPasswordAsync(SetPasswordRequest req)
        {
            EnsurePolicy(req.Password);

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
            user.MustChangePassword = false;
            await audit.RecordAndSaveAsync(user.UserId, PasswordSetAction, nameof(User), user.UserId);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// A signed-in user swaps their current (possibly temporary) password for one of their own. Every token
        /// issued before now stops working (Program.cs), so a stolen session ends here; the caller gets a fresh
        /// token to carry on with.
        /// </summary>
        public async Task<AuthResponse> ChangePasswordAsync(int userId, ChangePasswordRequest req)
        {
            EnsurePolicy(req.NewPassword);
            var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new KeyNotFoundException($"User {userId} not found.");
            if (Hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
                throw new InvalidOperationException("Your current password is incorrect.");
            if (req.NewPassword == req.CurrentPassword)
                throw new InvalidOperationException("Choose a password different from your current one.");

            user.PasswordHash = Hash(user, req.NewPassword);
            user.MustChangePassword = false;
            user.PasswordTokenHash = null;
            user.PasswordTokenExpires = null;
            await audit.RecordAndSaveAsync(userId, PasswordSetAction, nameof(User), userId);
            return await IssueAsync(user);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>A one-off password an admin can read out: 6 letters + 4 digits so it meets the policy,
        /// with no look-alike characters (l/1, o/0).
        /// Reference: Microsoft, "RandomNumberGenerator Class." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator
        /// </summary>
        public static string NewTemporaryPassword() =>
            RandomNumberGenerator.GetString("abcdefghjkmnpqrstuvwxyz", 6) + RandomNumberGenerator.GetString("23456789", 4);

        private static void EnsurePolicy(string password)
        {
            if (password.Length < 8 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
                throw new ArgumentException("Password must be at least 8 characters with a letter and a digit.");
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
        public Task SendPasswordLinkAsync(User user, string token, bool activation, bool selfService = false)
        {
            var link = $"{config["App:BaseUrl"]?.TrimEnd('/')}/set-password?token={token}";
            return activation
                ? email.SendAccountEmailAsync(user.Email, "Activate your account",
                    $"An account was created for you. Set your password to activate it: {link}")
                : selfService
                ? email.SendAccountEmailAsync(user.Email, "Change your password",
                    $"You asked to change your password. Choose a new one here: {link} If this wasn't you, ignore this email.")
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

            await NotifyAdminsAsync("UserLocked",
                $"{user.FirstName} {user.Surname}'s account was locked after {MaxFailedLogins} failed sign-ins.",
                user.UserId, "A user account was locked");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>The login page's "Forgot password?" form: tells the admins who is asking, so they can
        /// issue a temporary password. Answers the same whether or not the email matches an account.</summary>
        public async Task ForgotPasswordAsync(ForgotPasswordRequest req)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            var typed = $"{req.FirstName.Trim()} {req.Surname.Trim()}";
            string message;
            if (user is null)
                message = $"{typed} ({req.Email}) asked for a password reset, but no account uses that email.";
            else
            {
                // The account's name, not the typed one: anyone can type any name next to someone's email.
                var actual = $"{user.FirstName} {user.Surname}";
                message = $"{actual} ({user.Email}) asked for a password reset. Use Reset Password on their account in User Management.";
                if (!string.Equals(typed, actual, StringComparison.OrdinalIgnoreCase))
                    message += $" Note: the request gave the name \"{typed}\", which doesn't match this account. Check it's really them before resetting.";
            }
            await NotifyAdminsAsync("PasswordResetRequested", message, user?.UserId ?? 0, "Password reset requested");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Notify (in-app, live push, email) every active user who can manage users. Saves the Unit of Work.</summary>
        private async Task NotifyAdminsAsync(string typeName, string message, int userId, string subject)
        {
            var admins = await (
                from u in db.Users
                join rp in db.RolePermissions on u.RoleId equals rp.RoleId
                join p in db.Permissions on rp.PermissionId equals p.PermissionId
                where u.IsActive && p.PermissionName == Permissions.ManageUsers
                select u.UserId).ToListAsync();
            var notices = new List<Notification>();
            foreach (var adminId in admins)
                notices.Add(await notify.QueueAsync(adminId, typeName, message, nameof(User), userId));

            await db.SaveChangesAsync();
            foreach (var n in notices)
            {
                // "notification" is what both clients listen for to refresh their bell.
                await notify.PushAsync(n.UserId, "notification", new { userId });
                await email.SendAsync(n, subject);
            }
        }
//----------------------------------------------------------------------------------------------------------------------//
        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>When the account was last locked: the audit trail already holds it, so no extra column.</summary>
        private Task<DateTime?> LockedAtAsync(int userId) =>
            db.AuditLogs.Where(a => a.UserId == userId && a.Action == "user.locked")
                .MaxAsync(a => (DateTime?)a.Timestamp);
//----------------------------------------------------------------------------------------------------------------------//
        private Task RecordFailureAsync(int userId) =>
            audit.RecordAndSaveAsync(userId, FailedAction, nameof(User), userId);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
