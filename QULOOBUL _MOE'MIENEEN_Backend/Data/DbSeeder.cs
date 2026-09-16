using Microsoft.AspNetCore.Identity;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
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
            (Roles.Admin, 1, [Permissions.ManageUsers, Permissions.ReviewRequests, Permissions.GenerateReports, Permissions.ManageFiles]),
            (Roles.SecondaryAdmin, 2, [Permissions.ReviewRequests, Permissions.GenerateReports, Permissions.ManageFiles]),
            (Roles.Imaamat, 2, []),
            (Roles.FacilityManager, 3, []),
            (Roles.Staff, 4, []),
        ];

        public static void Seed(AppDbContext db, IConfiguration config)
        {
            if (!db.Roles.Any())
            {
                // Admin is granted every permission, so this is the full set.
                var permissions = RoleSeed.SelectMany(r => r.Granted).Distinct()
                    .ToDictionary(name => name, name => new Permission { PermissionName = name });
                db.Permissions.AddRange(permissions.Values);

                foreach (var (name, level, granted) in RoleSeed)
                {
                    var role = new Role { RoleName = name, HierarchyLevel = level };
                    db.Roles.Add(role);
                    db.RolePermissions.AddRange(
                        granted.Select(p => new RolePermission { Role = role, Permission = permissions[p] }));
                }
            }

            if (!db.RequestCategories.Any())
                db.RequestCategories.AddRange(new[] { "Maintenance", "Equipment", "Supplies", "Other" }
                    .Select(n => new RequestCategory { CategoryName = n }));

            if (!db.FileCategories.Any())
                db.FileCategories.AddRange(new[] { "Task Attachment", "Meeting Minutes", "Monthly Report", "Other" }
                    .Select(n => new FileCategory { CategoryName = n }));

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