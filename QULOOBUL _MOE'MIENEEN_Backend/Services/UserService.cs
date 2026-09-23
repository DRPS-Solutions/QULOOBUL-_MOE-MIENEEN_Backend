using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-06/UC-07 user management and the admin-driven half of SM-03. Every state change
    /// goes through Lifecycle.MoveUser; the login-driven half lives in AuthService.</summary>
    public class UserService(
        AppDbContext db, AuthService auth, AuditService audit, AccessGuard guard,
        TaskAssignmentService tasks, NotificationService notify, EmailService email)
    {
        /// <summary>
        /// How long a deactivated account's personal details are kept before anonymise() may run.
        /// ponytail: the architecture doc names no POPIA retention period; one year is a placeholder
        /// until the NPO sets its records policy.
        /// </summary>
        public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(365);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 createAccount [email is unique] / send activation email, write AuditLog.
        /// The account starts Pending Activation with no password; the user sets their own.</summary>
        public async Task<UserDto> CreateAsync(CreateUserRequest req, int actorId)
        {
            var email = req.Email.Trim();
            if (await db.Users.AnyAsync(u => u.Email == email))
                throw new InvalidOperationException("An account with that email already exists.");
            var role = await db.Roles.FindAsync(req.RoleId)
                ?? throw new InvalidOperationException($"Unknown role {req.RoleId}.");

            var user = new User
            {
                RoleId = role.RoleId,
                FirstName = req.FirstName.Trim(),
                Surname = req.Surname.Trim(),
                Email = email,
                CellNo = req.CellNo.Trim(),
                Status = Lifecycle.UserPending,
                IsActive = false,
            };
            var token = AuthService.NewPasswordToken(user);
            db.Users.Add(user);
            await db.SaveChangesAsync();                 // assigns user.UserId
            await audit.RecordAndSaveAsync(actorId, "user.create", nameof(User), user.UserId);
            await auth.SendPasswordLinkAsync(user, token, activation: true);

            return ToDto(user, role.RoleName);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 updateProfile(details) / write AuditLog: an Active self-transition. A user edits
        /// their own details; changing a role is an admin's call.</summary>
        public async Task<UserDto> UpdateProfileAsync(int userId, UpdateUserRequest req, int actorId)
        {
            var isAdmin = await guard.IsAdminAsync(actorId);
            if (userId != actorId && !isAdmin)
                throw new UnauthorizedAccessException("You can only edit your own profile.");
            if (req.RoleId is not null && !isAdmin)
                throw new UnauthorizedAccessException("Only an admin can change a role.");

            var user = await Load(userId);
            if (user.Status != Lifecycle.UserActive)
                throw new InvalidOperationException($"A {user.Status.ToLowerInvariant()} account cannot be edited.");

            if (req.FirstName is { } f) user.FirstName = f.Trim();
            if (req.Surname is { } s) user.Surname = s.Trim();
            if (req.CellNo is { } c) user.CellNo = c.Trim();
            if (req.RoleId is { } r)
                user.RoleId = (await db.Roles.FindAsync(r) ?? throw new InvalidOperationException($"Unknown role {r}.")).RoleId;

            audit.Record(actorId, "user.update", nameof(User), userId);
            await db.SaveChangesAsync();
            return ToDto(user, await db.Roles.Where(x => x.RoleId == user.RoleId).Select(x => x.RoleName).FirstAsync());
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 deactivate() / revoke sessions, reassign open tasks. Sessions end because the
        /// JWT check in Program.cs refuses tokens of users who are no longer active.</summary>
        public async Task DeactivateAsync(int userId, int actorId)
        {
            if (userId == actorId) throw new InvalidOperationException("You cannot deactivate your own account.");
            var user = await Load(userId);
            Lifecycle.MoveUser(user, Lifecycle.UserInactive);
            user.DeactivatedAt = DateTime.UtcNow;
            user.PasswordTokenHash = null;
            user.PasswordTokenExpires = null;

            var notices = await tasks.ReassignOpenTasksAsync(userId, actorId);
            audit.Record(actorId, "user.deactivate", nameof(User), userId);
            await db.SaveChangesAsync();

            foreach (var n in notices)
            {
                await notify.PushAsync(n.UserId, "taskAssigned", new { taskId = n.EntityId });
                await email.SendAsync(n, "A task was reassigned to you");
            }
            if (notices.Count > 0) await notify.BroadcastAsync("boardChanged", new { });
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 reactivate(admin) [MANAGE_USERS] / write AuditLog.</summary>
        public async Task ReactivateAsync(int userId, int actorId)
        {
            var user = await Load(userId);
            Lifecycle.MoveUser(user, Lifecycle.UserActive);
            user.DeactivatedAt = null;
            user.FailedLoginCount = 0;
            await audit.RecordAndSaveAsync(actorId, "user.reactivate", nameof(User), userId);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// SM-03 resetPassword(admin) [MANAGE_USERS] / clear failure count, write AuditLog. Unlocks a
        /// Locked account (or resets an Active one): the old password stops working and the user picks
        /// a new one from the emailed link. On a Pending account it re-sends the activation email.
        /// </summary>
        public async Task ResetPasswordAsync(int userId, int actorId)
        {
            var user = await Load(userId);
            var activation = user.Status == Lifecycle.UserPending;
            if (!activation)
            {
                Lifecycle.MoveUser(user, Lifecycle.UserActive);
                user.PasswordHash = string.Empty;
            }
            user.FailedLoginCount = 0;
            var token = AuthService.NewPasswordToken(user);
            await audit.RecordAndSaveAsync(actorId, "user.password.reset", nameof(User), userId);
            await auth.SendPasswordLinkAsync(user, token, activation);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-03 anonymise() [POPIA retention period elapsed] / scrub personal fields, keep AuditLog
        /// references. The row stays, so every FK into it (history, minutes, audit) still resolves.</summary>
        public async Task AnonymiseAsync(int userId, int actorId)
        {
            var user = await Load(userId);
            if (user.Status != Lifecycle.UserInactive || user.DeactivatedAt is not { } since
                || DateTime.UtcNow - since < RetentionPeriod)
                throw new InvalidOperationException(
                    $"Only an account deactivated for at least {RetentionPeriod.TotalDays:0} days can be anonymised.");

            Lifecycle.MoveUser(user, Lifecycle.UserAnonymised);
            user.FirstName = "Anonymised";
            user.Surname = $"User {userId}";
            user.Email = $"anonymised-{userId}@invalid.local"; // Email is unique-indexed
            user.CellNo = string.Empty;
            user.PasswordHash = string.Empty;
            user.PasswordTokenHash = null;
            user.PasswordTokenExpires = null;
            await audit.RecordAndSaveAsync(actorId, "user.anonymise", nameof(User), userId);
        }
//----------------------------------------------------------------------------------------------------------------------//
        public Task<List<RoleDto>> RolesAsync() =>
            db.Roles.OrderBy(r => r.HierarchyLevel).ThenBy(r => r.RoleName)
                .Select(r => new RoleDto(r.RoleId, r.RoleName, r.HierarchyLevel)).ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        private async Task<User> Load(int userId)
        {
            var user = await db.Users.FindAsync(userId) ?? throw new KeyNotFoundException($"User {userId} not found.");
            if (user.Email == SystemAccountEmail)
                throw new InvalidOperationException("The system account cannot be changed.");
            return user;
        }

        private static UserDto ToDto(User u, string roleName) =>
            new(u.UserId, u.FirstName, u.Surname, u.Email, u.CellNo, roleName, u.IsActive, u.Status);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
