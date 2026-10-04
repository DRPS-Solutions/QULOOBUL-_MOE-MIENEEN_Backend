using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Hubs;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using Xunit;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace Backend.Tests;

// The SM guards that need data, so they live in the services rather than the Lifecycle tables.
// None of these paths push over SignalR, so the hub is null; email has no API key, so it no-ops.
public class StateMachineGuardTests
{
    [Fact]
    public async Task Three_consecutive_failed_logins_lock_the_account()
    {
        using var db = NewDb();
        var user = new User { Role = new Role { RoleName = Roles.Staff }, Email = "alice@test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var auth = Auth(db);

        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                auth.LoginAsync(new LoginRequest { Email = "alice@test", Password = "wrong" }));

        Assert.Equal(Lifecycle.UserLocked, user.Status);
        Assert.False(user.IsActive);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_task_cannot_start_without_an_assignee_and_reopening_needs_ReopenTask()
    {
        using var db = NewDb();
        var staff = new User { Role = new Role { RoleName = Roles.Staff, HierarchyLevel = 4 }, Email = "s@test" };
        var todo = new ProjectTask { Project = new Project { Owner = staff }, Creator = staff, Status = "To Do" };
        var done = new ProjectTask { Project = todo.Project, Creator = staff, Status = "Done" };
        db.AddRange(staff, todo, done);
        await db.SaveChangesAsync();
        var tasks = Tasks(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tasks.ChangeStatusAsync(todo.TaskId, "In Progress", staff.UserId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tasks.ChangeStatusAsync(done.TaskId, "In Progress", staff.UserId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>   // cancel(admin)
            tasks.ChangeStatusAsync(todo.TaskId, Lifecycle.TaskCancelled, staff.UserId));

        db.TaskAssignments.Add(new TaskAssignment { TaskId = todo.TaskId, UserId = staff.UserId });
        await db.SaveChangesAsync();
        await tasks.ChangeStatusAsync(todo.TaskId, "In Progress", staff.UserId);
        Assert.Equal("In Progress", todo.Status);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Deactivating_a_user_hands_their_open_tasks_to_the_project_owner()
    {
        using var db = NewDb();
        var role = new Role { RoleName = Roles.Staff, HierarchyLevel = 4 };
        var owner = new User { Role = role, Email = "owner@test" };
        var leaver = new User { Role = role, Email = "leaver@test" };
        var project = new Project { Owner = owner };
        var open = new ProjectTask { Project = project, Creator = owner, Status = "In Progress" };
        var finished = new ProjectTask { Project = project, Creator = owner, Status = "Done" };
        db.AddRange(owner, leaver, open, finished,
            new TaskAssignment { Task = open, User = leaver },
            new TaskAssignment { Task = finished, User = leaver });
        await db.SaveChangesAsync();

        await Tasks(db).ReassignOpenTasksAsync(leaver.UserId, owner.UserId);
        await db.SaveChangesAsync();

        Assert.Equal([owner.UserId], db.TaskAssignments.Where(a => a.TaskId == open.TaskId).Select(a => a.UserId));
        Assert.Equal([leaver.UserId], db.TaskAssignments.Where(a => a.TaskId == finished.TaskId).Select(a => a.UserId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Deactivating_a_user_hands_their_recurring_schedules_to_the_creator_or_the_admin()
    {
        using var db = NewDb();
        var role = new Role { RoleName = Roles.Staff, HierarchyLevel = 4 };
        var admin = new User { Role = role, Email = "admin@test", IsActive = true };
        var manager = new User { Role = role, Email = "manager@test", IsActive = true };
        var leaver = new User { Role = role, Email = "leaver@test", IsActive = true };
        db.AddRange(admin, manager, leaver);
        await db.SaveChangesAsync();
        var byManager = new TaskSchedule { CreatedBy = manager.UserId, AssignedTo = leaver.UserId, Title = "Sweep", DayOfWeek = "Friday" };
        var bySelf = new TaskSchedule { CreatedBy = leaver.UserId, AssignedTo = leaver.UserId, Title = "Lock up", DayOfWeek = "Friday" };
        db.AddRange(byManager, bySelf);
        await db.SaveChangesAsync();

        await Tasks(db).ReassignOpenTasksAsync(leaver.UserId, admin.UserId);
        await db.SaveChangesAsync();

        Assert.Equal(manager.UserId, byManager.AssignedTo);
        Assert.Equal(admin.UserId, bySelf.AssignedTo);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Theory]
    [InlineData(29, true)]
    [InlineData(31, false)]
    public async Task A_declined_request_can_be_resubmitted_only_within_30_days(int daysAgo, bool allowed)
    {
        using var db = NewDb();
        var requester = new User { Role = new Role { RoleName = Roles.Staff }, Email = "alice@test" };
        var request = new ResourceRequest
        {
            Requester = requester, Category = new RequestCategory { CategoryName = "Other" }, Status = "Declined",
        };
        db.AddRange(request, new RequestStatusHistory
        {
            Request = request, NewStatus = "Declined", ChangedBy = 1, TimeCreated = DateTime.UtcNow.AddDays(-daysAgo),
        });
        await db.SaveChangesAsync();
        var requests = new RequestReviewService(db, new AuditService(db), new NotificationService(db, null!), null!);

        var resubmit = () => requests.ResubmitAsync(request.RequestId, requester.UserId, null);
        if (allowed)
        {
            await resubmit();
            Assert.Equal(Lifecycle.RequestStart, request.Status);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(resubmit);
    }
//----------------------------------------------------------------------------------------------------------------------//
    // R5: the facility manager who approved an escalated request closes it; another manager may not.
    [Fact]
    public async Task Only_the_approving_facility_manager_may_complete_an_escalated_request()
    {
        using var db = NewDb();
        var managerRole = new Role { RoleName = Roles.FacilityManager, HierarchyLevel = 3 };
        var approver = new User { Role = managerRole, Email = "fm1@test" };
        var other = new User { Role = managerRole, Email = "fm2@test" };
        var requester = new User { Role = new Role { RoleName = Roles.Staff }, Email = "alice@test" };
        db.AddRange(approver, other, requester);
        await db.SaveChangesAsync();
        var request = new ResourceRequest
        {
            Requester = requester, Category = new RequestCategory { CategoryName = "Other" },
            Status = "Approved", ReviewedBy = approver.UserId,
        };
        db.Add(request);
        await db.SaveChangesAsync();
        var requests = new RequestReviewService(db, new AuditService(db), new NotificationService(db, null!),
            Email(db, new ConfigurationBuilder().Build()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => requests.CompleteAsync(request.RequestId, other.UserId));
        // The SignalR push after the commit has no hub here; the status is saved before it runs.
        var approved = await Record.ExceptionAsync(() => requests.CompleteAsync(request.RequestId, approver.UserId));
        Assert.IsNotType<UnauthorizedAccessException>(approved);
        Assert.Equal("Completed", (await db.ResourceRequests.AsNoTracking().SingleAsync()).Status);
    }
//----------------------------------------------------------------------------------------------------------------------//
    // QA v3: staff with a task on the event but not in "Staff Helping" got no ProjectCancelled notification.
    [Fact]
    public async Task Cancelling_an_event_notifies_members_and_task_assignees_but_not_the_canceller()
    {
        using var db = NewDb();
        var role = new Role { RoleName = Roles.Staff, HierarchyLevel = 4 };
        User owner = new() { Role = role, Email = "o@test", IsActive = true },
             member = new() { Role = role, Email = "m@test", IsActive = true },
             assignee = new() { Role = role, Email = "a@test", IsActive = true };
        var project = new Project { Owner = owner, ProjectName = "Fete", Status = "Active" };
        var task = new ProjectTask { Project = project, Creator = owner, Status = "To Do" };
        db.AddRange(owner, member, assignee, project, task,
            new ProjectMember { Project = project, User = member },
            new TaskAssignment { Task = task, User = assignee });
        await db.SaveChangesAsync();

        var notify = new NotificationService(db, NoopHub());
        var projects = new ProjectService(db, new AuditService(db), notify, new AccessGuard(db),
            new TaskAssignmentService(db, new AuditService(db), notify, Email(db, new ConfigurationBuilder().Build()), new AccessGuard(db)));
        await projects.CancelAsync(project.ProjectId, "rain", owner.UserId);

        var told = await db.Notifications.Where(n => n.Type!.TypeName == "ProjectCancelled").Select(n => n.UserId).ToListAsync();
        Assert.Equal(new[] { assignee.UserId, member.UserId }.Order(), told.Order());
        Assert.Equal(Lifecycle.TaskCancelled, task.Status);
    }
//----------------------------------------------------------------------------------------------------------------------//
    /// <summary>A hub whose every send is a completed no-op, for paths that push after committing.</summary>
    private static IHubContext<NotificationHub> NoopHub() => (IHubContext<NotificationHub>)NoopProxy.For(typeof(IHubContext<NotificationHub>));

    public class NoopProxy : DispatchProxy
    {
        public static object For(Type t) => DispatchProxy.Create(t, typeof(NoopProxy));
        protected override object? Invoke(MethodInfo? m, object?[]? args) =>
            m!.ReturnType == typeof(Task) ? Task.CompletedTask : m.ReturnType.IsInterface ? For(m.ReturnType) : null;
    }
//----------------------------------------------------------------------------------------------------------------------//
    private static AuthService Auth(AppDbContext db)
    {
        var config = new ConfigurationBuilder().Build();
        var audit = new AuditService(db);
        return new AuthService(db, null!, audit, new NotificationService(db, null!), Email(db, config),
            config, NullLogger<AuthService>.Instance);
    }
//----------------------------------------------------------------------------------------------------------------------//
    private static TaskAssignmentService Tasks(AppDbContext db) =>
        new(db, new AuditService(db), new NotificationService(db, null!),
            Email(db, new ConfigurationBuilder().Build()), new AccessGuard(db));
//----------------------------------------------------------------------------------------------------------------------//
    private static EmailService Email(AppDbContext db, IConfiguration config) =>
        new(config, db, NullLogger<EmailService>.Instance);
//----------------------------------------------------------------------------------------------------------------------//
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//