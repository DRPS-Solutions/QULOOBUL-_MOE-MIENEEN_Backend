using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using Xunit;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace Backend.Tests;

// The anti-IDOR NFR in one place: if these open up, altering an id in the URL reaches
// somebody else's work.
public class AccessGuardTests
{
    [Fact]
    public async Task A_stranger_cannot_read_a_task_on_a_project_they_are_not_on()
    {
        var f = await Fixture.CreateAsync();

        await f.Guard.EnsureTaskAsync(f.OwnerId, f.TaskId);       // owner: fine
        await f.Guard.EnsureTaskAsync(f.AssigneeId, f.TaskId);    // assignee: fine
        await f.Guard.EnsureTaskAsync(f.AdminId, f.TaskId);       // admin: fine

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Guard.EnsureTaskAsync(f.StrangerId, f.TaskId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_missing_task_is_a_404_not_a_403()
    {
        var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Guard.EnsureTaskAsync(f.StrangerId, 9999));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task The_board_hides_other_peoples_tasks_rather_than_refusing_the_whole_project()
    {
        var f = await Fixture.CreateAsync();
        var tasks = new TaskService(f.Db, null!, new AuditService(f.Db), null!, f.Guard);

        Assert.Single(await tasks.BoardAsync(f.ProjectId, f.AssigneeId));
        Assert.Empty(await tasks.BoardAsync(f.ProjectId, f.StrangerId));
        Assert.Single(await tasks.BoardAsync(f.ProjectId, f.AdminId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Only_the_owner_or_an_admin_may_change_the_member_roster()
    {
        var f = await Fixture.CreateAsync();

        await f.Guard.EnsureProjectOwnerAsync(f.OwnerId, f.ProjectId);
        await f.Guard.EnsureProjectOwnerAsync(f.AdminId, f.ProjectId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Guard.EnsureProjectOwnerAsync(f.AssigneeId, f.ProjectId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task ManageFiles_reaches_any_file_without_being_a_full_admin()
    {
        var f = await Fixture.CreateAsync();
        // Secondary Admin: ManageFiles but not ManageUsers (test plan F2).
        var secondary = new User
        {
            Role = new Role { RoleName = Roles.SecondaryAdmin, HierarchyLevel = 2 },
            Email = "secondary@test",
        };
        f.Db.Users.Add(secondary);
        f.Db.RolePermissions.Add(new RolePermission
        {
            Role = secondary.Role,
            Permission = new Permission { PermissionName = Permissions.ManageFiles },
        });
        var onTask = new FileUpload { UploadedBy = f.OwnerId, TaskId = f.TaskId, FileName = "a.pdf" };
        var unattached = new FileUpload { UploadedBy = f.OwnerId, FileName = "b.pdf" };
        f.Db.FileUploads.AddRange(onTask, unattached);
        await f.Db.SaveChangesAsync();

        await f.Guard.EnsureFileAsync(secondary.UserId, onTask.FileId);
        await f.Guard.EnsureFileAsync(secondary.UserId, unattached.FileId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => f.Guard.EnsureFileAsync(f.StrangerId, unattached.FileId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    // One project owned by "owner", one task on it assigned to "assignee". "stranger" is on
    // nothing; "admin" holds ManageUsers.
    private sealed record Fixture(
        AppDbContext Db, AccessGuard Guard,
        int OwnerId, int AssigneeId, int StrangerId, int AdminId, int ProjectId, int TaskId)
    {
        public static async Task<Fixture> CreateAsync()
        {
            // Reference: Microsoft (2026) Testing without your production database system - EF Core. Available at: https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database (Accessed: 4 October 2026).
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var staffRole = new Role { RoleName = Roles.Staff, HierarchyLevel = 4 };
            var adminRole = new Role { RoleName = Roles.Admin, HierarchyLevel = 1 };
            db.RolePermissions.Add(new RolePermission
            {
                Role = adminRole,
                Permission = new Permission { PermissionName = Permissions.ManageUsers },
            });
//----------------------------------------------------------------------------------------------------------------------//
            var owner = new User { Role = staffRole, Email = "owner@test" };
            var assignee = new User { Role = staffRole, Email = "assignee@test" };
            var stranger = new User { Role = staffRole, Email = "stranger@test" };
            var admin = new User { Role = adminRole, Email = "admin@test" };
            db.Users.AddRange(owner, assignee, stranger, admin);
            await db.SaveChangesAsync();

            var project = new Project { OwnerId = owner.UserId, ProjectName = "Roof", Status = "Active" };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var task = new ProjectTask
            {
                ProjectId = project.ProjectId,
                CreatedBy = owner.UserId,
                Title = "Fix the gutter",
                Status = "In Progress",
                Priority = "Normal",
                Deadline = DateTime.UtcNow.AddDays(3),
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();

            db.TaskAssignments.Add(new TaskAssignment { TaskId = task.TaskId, UserId = assignee.UserId });
            await db.SaveChangesAsync();

            return new Fixture(db, new AccessGuard(db),
                owner.UserId, assignee.UserId, stranger.UserId, admin.UserId, project.ProjectId, task.TaskId);
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//