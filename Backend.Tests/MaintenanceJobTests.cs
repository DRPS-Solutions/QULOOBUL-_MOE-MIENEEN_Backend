using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using Xunit;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace Backend.Tests;

// The clock-driven transitions. Each one has to be idempotent: the sweep runs hourly, and a
// repeat tick must not escalate twice, nag twice, or generate the same duty twice.
public class MaintenanceJobTests
{
    private static readonly DateTime Now = new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_request_with_no_action_for_48_hours_escalates_exactly_once()
    {
        var (db, notify) = await NewAsync();
        var requester = await AddUserAsync(db, "staff@test", Roles.Staff);
        var manager = await AddUserAsync(db, "fm@test", Roles.FacilityManager);

        db.ResourceRequests.Add(new ResourceRequest
        {
            RequestedBy = requester,
            CategoryId = 1,
            Status = Lifecycle.RequestStart,
            DateSubmitted = Now.AddHours(-49),
        });
        await db.SaveChangesAsync();

        var first = await MaintenanceJobs.EscalateStaleRequestsAsync(db, notify, Now);
        var second = await MaintenanceJobs.EscalateStaleRequestsAsync(db, notify, Now);

        Assert.Equal(2, first.Count);               // the facility manager and the requester
        Assert.Empty(second);                       // no longer Pending, so nothing to escalate
        Assert.Equal(Lifecycle.RequestEscalated, db.ResourceRequests.Single().Status);
        Assert.Single(db.RequestStatusHistory);
        Assert.Contains(manager, first.Select(d => d.Notification.UserId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_request_inside_the_48_hour_window_is_left_alone()
    {
        var (db, notify) = await NewAsync();
        var requester = await AddUserAsync(db, "staff@test", Roles.Staff);
        db.ResourceRequests.Add(new ResourceRequest
        {
            RequestedBy = requester,
            CategoryId = 1,
            Status = Lifecycle.RequestStart,
            DateSubmitted = Now.AddHours(-47),
        });
        await db.SaveChangesAsync();

        Assert.Empty(await MaintenanceJobs.EscalateStaleRequestsAsync(db, notify, Now));
        Assert.Equal(Lifecycle.RequestStart, db.ResourceRequests.Single().Status);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_deadline_reminder_is_sent_once_per_assignee()
    {
        var (db, notify) = await NewAsync();
        var assignee = await AddUserAsync(db, "staff@test", Roles.Staff);
        var project = await AddDailyProjectAsync(db, assignee);

        var task = new ProjectTask
        {
            ProjectId = project,
            CreatedBy = assignee,
            Title = "Kitchen duty",
            Status = "In Progress",
            Priority = "Normal",
            Deadline = Now.AddHours(6),
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        db.TaskAssignments.Add(new TaskAssignment { TaskId = task.TaskId, UserId = assignee });
        await db.SaveChangesAsync();

        Assert.Single(await MaintenanceJobs.SendDeadlineRemindersAsync(db, notify, Now));
        Assert.Empty(await MaintenanceJobs.SendDeadlineRemindersAsync(db, notify, Now));
        Assert.Equal(1, await db.Notifications.CountAsync());
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_task_outside_the_reminder_horizon_is_not_announced()
    {
        var (db, notify) = await NewAsync();
        var assignee = await AddUserAsync(db, "staff@test", Roles.Staff);
        var project = await AddDailyProjectAsync(db, assignee);

        var task = new ProjectTask
        {
            ProjectId = project,
            CreatedBy = assignee,
            Title = "Next week",
            Status = "In Progress",
            Priority = "Normal",
            Deadline = Now.AddDays(5),
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        db.TaskAssignments.Add(new TaskAssignment { TaskId = task.TaskId, UserId = assignee });
        await db.SaveChangesAsync();

        Assert.Empty(await MaintenanceJobs.SendDeadlineRemindersAsync(db, notify, Now));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_weekly_template_generates_one_task_on_its_day_and_not_twice()
    {
        var (db, notify) = await NewAsync();
        var staff = await AddUserAsync(db, "staff@test", Roles.Staff);
        await AddDailyProjectAsync(db, staff);

        db.TaskSchedules.Add(new TaskSchedule
        {
            CreatedBy = staff,
            AssignedTo = staff,
            Title = "Weekly kitchen duty",
            DayOfWeek = Now.DayOfWeek.ToString(),
            IsActive = true,
        });
        await db.SaveChangesAsync();

        Assert.Single(await MaintenanceJobs.GenerateScheduledTasksAsync(db, notify, Now));
        Assert.Empty(await MaintenanceJobs.GenerateScheduledTasksAsync(db, notify, Now));

        var generated = await db.Tasks.SingleAsync();
        Assert.Equal("Weekly kitchen duty", generated.Title);
        Assert.Equal(Lifecycle.TaskStart, generated.Status);
        Assert.Single(db.TaskAssignments);
        Assert.Single(db.TaskStatusHistory);       // SM-01 records the opening state too
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_template_for_another_day_generates_nothing()
    {
        var (db, notify) = await NewAsync();
        var staff = await AddUserAsync(db, "staff@test", Roles.Staff);
        await AddDailyProjectAsync(db, staff);

        db.TaskSchedules.Add(new TaskSchedule
        {
            CreatedBy = staff,
            AssignedTo = staff,
            Title = "Some other day",
            DayOfWeek = Now.AddDays(1).DayOfWeek.ToString(),
            IsActive = true,
        });
        await db.SaveChangesAsync();

        Assert.Empty(await MaintenanceJobs.GenerateScheduledTasksAsync(db, notify, Now));
        Assert.Equal(0, await db.Tasks.CountAsync());
    }
//----------------------------------------------------------------------------------------------------------------------//
    // Every sweep needs the seeded System account to own the history row it writes.
    private static async Task<(AppDbContext Db, NotificationService Notify)> NewAsync()
    {
        // Reference: Microsoft (2026) Testing without your production database system - EF Core. Available at: https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database (Accessed: 4 October 2026).
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.AddRange(
            new Role { RoleName = Roles.Staff, HierarchyLevel = 4 },
            new Role { RoleName = Roles.FacilityManager, HierarchyLevel = 3 });
        await db.SaveChangesAsync();
        await AddUserAsync(db, SystemAccountEmail, Roles.Staff);
        return (db, new NotificationService(db, null!));
    }
//----------------------------------------------------------------------------------------------------------------------//
    private static async Task<int> AddUserAsync(AppDbContext db, string email, string roleName)
    {
        var user = new User { RoleId = db.Roles.Single(r => r.RoleName == roleName).RoleId, Email = email };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.UserId;
    }
//----------------------------------------------------------------------------------------------------------------------//
    private static async Task<int> AddDailyProjectAsync(AppDbContext db, int ownerId)
    {
        var project = new Project { OwnerId = ownerId, ProjectName = DailyTasksProject, Status = "Active" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.ProjectId;
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//