using Microsoft.AspNetCore.Identity;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Data
{
    /// <summary>
    /// Reference data and the first admin account. Wired to EF's UseSeeding hook in Program.cs, so it
    /// runs straight after `dotnet ef database update` (the CD migration step). Each block only fills
    /// an empty table, so re-running it is harmless.
    /// </summary>
    public static class DbSeeder
    {
        // Lower HierarchyLevel = more authority (TaskAssignmentService: assign down or sideways, never up).
        private static readonly (string Name, int Level, string[] Granted)[] RoleSeed =
        [
            (Roles.Admin, 1, [Permissions.ManageUsers, Permissions.ReviewRequests, Permissions.GenerateReports, Permissions.ManageFiles, Permissions.ReopenTask]),
            (Roles.SecondaryAdmin, 2, [Permissions.ReviewRequests, Permissions.GenerateReports, Permissions.ManageFiles, Permissions.ReopenTask]),
            (Roles.Imaamat, 2, []),
            (Roles.FacilityManager, 3, []),
            (Roles.Staff, 4, []),
        ];
//----------------------------------------------------------------------------------------------------------------------//
        private static readonly string[] NotificationTypeSeed =
        [
            "TaskAssigned", "TaskCancelled", "DeadlineApproaching", "ProjectAssigned", "EventInvite",
            "RequestSubmitted", "RequestReviewed", "RequestCompleted", "RequestEscalated", "UserLocked", "PasswordResetRequested", "Announcement",
        ];
//----------------------------------------------------------------------------------------------------------------------//
        public static void Seed(AppDbContext db, IConfiguration config)
        {
            if (!db.Roles.Any())
                db.Roles.AddRange(RoleSeed.Select(r => new Role { RoleName = r.Name, HierarchyLevel = r.Level }));
            db.SaveChanges();

            // Grants are topped up rather than seeded once, so a permission added later (ReopenTask)
            // reaches databases that were seeded before it existed.
            var permissions = db.Permissions.ToDictionary(p => p.PermissionName);
            foreach (var name in RoleSeed.SelectMany(r => r.Granted).Distinct().Where(n => !permissions.ContainsKey(n)))
                db.Permissions.Add(permissions[name] = new Permission { PermissionName = name });
            foreach (var (name, _, granted) in RoleSeed)
            {
                var role = db.Roles.SingleOrDefault(r => r.RoleName == name);
                if (role is null) continue;
                foreach (var p in granted.Where(p => !db.RolePermissions.Any(rp =>
                             rp.RoleId == role.RoleId && rp.Permission!.PermissionName == p)))
                    db.RolePermissions.Add(new RolePermission { Role = role, Permission = permissions[p] });
            }

            if (!db.RequestCategories.Any())
                db.RequestCategories.AddRange(new[] { "Maintenance", "Equipment", "Supplies", "Other" }
                    .Select(n => new RequestCategory { CategoryName = n }));

            if (!db.FileCategories.Any())
                db.FileCategories.AddRange(new[] { "Task Attachment", "Meeting Minutes", "Monthly Report", "Other" }
                    .Select(n => new FileCategory { CategoryName = n }));

            // NotificationService creates types lazily; seeding the known ones up front lets a user
            // switch a type off (UC-08 preferences) before it has ever fired. Topped up, like grants.
            foreach (var name in NotificationTypeSeed.Where(n => !db.NotificationTypes.Any(t => t.TypeName == n)))
                db.NotificationTypes.Add(new NotificationType { TypeName = name });

            db.SaveChanges();

            // The first admin comes from config (user-secrets locally, pipeline secrets in CD), never from code.
            var email = config["Seed:AdminEmail"];
            var password = config["Seed:AdminPassword"];
            if (!db.Users.Any() && !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                var admin = new User
                {
                    RoleId = db.Roles.Single(r => r.RoleName == Roles.Admin).RoleId,
                    FirstName = "System",
                    Surname = "Admin",
                    Email = email,
                };
                admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password);
                db.Users.Add(admin);
                db.SaveChanges();
            }

            // Local testing only: one pre-activated user per non-admin role, so every role's views can be
            // signed into without the emailed set-password link. Opt-in via user-secrets `Seed:DevPassword`;
            // it is never set in Azure, so nothing is created there.
            var devPassword = config["Seed:DevPassword"];
            if (!string.IsNullOrWhiteSpace(devPassword))
                foreach (var role in db.Roles.Where(r => r.RoleName != Roles.Admin).ToList())
                {
                    var devEmail = $"{role.RoleName.Replace(" ", "").ToLowerInvariant()}@dev.local";
                    if (db.Users.Any(u => u.Email == devEmail)) continue;
                    var dev = new User { RoleId = role.RoleId, FirstName = role.RoleName, Surname = "Dev", Email = devEmail };
                    dev.PasswordHash = new PasswordHasher<User>().HashPassword(dev, devPassword);
                    db.Users.Add(dev);
                }
            db.SaveChanges();

            // The actor for clock-driven transitions in MaintenanceJobs. Every history and audit row
            // needs a resolvable user FK, and an escalation has no human behind it. Inactive and with
            // no usable password hash, so it can neither log in nor be assigned work.
            if (!db.Users.Any(u => u.Email == SystemAccountEmail))
            {
                db.Users.Add(new User
                {
                    RoleId = db.Roles.Single(r => r.RoleName == Roles.Staff).RoleId,
                    FirstName = "System",
                    Surname = "Automation",
                    Email = SystemAccountEmail,
                    IsActive = false,
                    Status = Lifecycle.UserInactive,
                });
                db.SaveChanges();
            }

            // Every task needs a project; standalone daily tasks go in this one, owned by an admin.
            var adminId = db.Users.Where(u => u.Role!.RoleName == Roles.Admin)
                .OrderBy(u => u.UserId).Select(u => (int?)u.UserId).FirstOrDefault();
            if (adminId is not null && !db.Projects.Any(p => p.ProjectName == DailyTasksProject))
            {
                db.Projects.Add(new Project
                {
                    OwnerId = adminId.Value,
                    ProjectName = DailyTasksProject,
                    Status = "Active",
                    Description = "Standalone day-to-day tasks not tied to an event or project.",
                });
                db.SaveChanges();
            }
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//