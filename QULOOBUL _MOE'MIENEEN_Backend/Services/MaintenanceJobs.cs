using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// The three transitions the architecture doc drives from a clock rather than a user action:
    /// SM-02's 48-hour escalation ("the trigger is treated as constantly firing ... in implementation
    /// this is a scheduled background job on the API"), SM-01's deadlineApproaching reminder, and
    /// Task_Schedule's documented job of generating a task from a recurring weekly template.
    ///
    /// One hourly sweep runs all three: none of them needs finer granularity, and one timer is one
    /// thing to reason about at 3am. Each sweep is idempotent, so a missed or repeated tick is harmless.
    ///
    /// The sweep methods are static and take their dependencies explicitly so they can be exercised
    /// against an in-memory context with an injected clock, without starting a host.
    /// </summary>
    public class MaintenanceJobs(IServiceScopeFactory scopes, ILogger<MaintenanceJobs> log) : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        public static readonly TimeSpan EscalateAfter = TimeSpan.FromHours(48);
        public static readonly TimeSpan ReminderHorizon = TimeSpan.FromHours(24);

        public const string EscalatedType = "RequestEscalated";
        public const string DeadlineType = "DeadlineApproaching";
        public const string ScheduledType = "TaskAssigned";
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>A committed notification plus how to deliver it live and by email.</summary>
        public record Dispatch(Notification Notification, string EventName, object Payload, string EmailSubject);
//----------------------------------------------------------------------------------------------------------------------//
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(Interval);
            try
            {
                do
                {
                    try
                    {
                        await SweepAsync(ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // A failed sweep must not kill the host: the next tick retries.
                        log.LogError(ex, "Maintenance sweep failed");
                    }
                }
                while (await timer.WaitForNextTickAsync(ct));
            }
            catch (OperationCanceledException) { /* host shutting down */ }
        }
//----------------------------------------------------------------------------------------------------------------------//
        private async Task SweepAsync(CancellationToken ct)
        {
            // BackgroundService is a singleton; the DbContext is request-scoped, so the sweep opens
            // its own scope (architecture doc: "DbContext is not thread-safe ... request scope is correct").
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notify = scope.ServiceProvider.GetRequiredService<NotificationService>();
            var email = scope.ServiceProvider.GetRequiredService<EmailService>();
            var now = DateTime.UtcNow;

            var dispatches = new List<Dispatch>();
            dispatches.AddRange(await EscalateStaleRequestsAsync(db, notify, now, ct));
            dispatches.AddRange(await SendDeadlineRemindersAsync(db, notify, now, ct));
            dispatches.AddRange(await GenerateScheduledTasksAsync(db, notify, now, ct));

            foreach (var d in dispatches)
            {
                await notify.PushAsync(d.Notification.UserId, d.EventName, d.Payload);
                await email.SendAsync(d.Notification, d.EmailSubject);
            }
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// SM-02 Pending → Escalated: "[no admin action within 48 hours] / route to facility manager, notify".
        /// </summary>
        public static async Task<List<Dispatch>> EscalateStaleRequestsAsync(
            AppDbContext db, NotificationService notify, DateTime now, CancellationToken ct = default)
        {
            var cutoff = now - EscalateAfter;
            var stale = await db.ResourceRequests
                .Where(r => r.Status == Lifecycle.RequestStart && r.DateSubmitted <= cutoff)
                .ToListAsync(ct);
            if (stale.Count == 0) return [];

            var systemId = await SystemUserIdAsync(db, ct);
            if (systemId is null) return [];

            var managers = await db.Users
                .Where(u => u.IsActive && u.Role!.RoleName == Roles.FacilityManager)
                .Select(u => u.UserId).ToListAsync(ct);

            var pending = new List<(Notification N, int RequestId)>();
            foreach (var request in stale)
            {
                Lifecycle.EnsureRequestMove(request.Status, Lifecycle.RequestEscalated);
                request.Status = Lifecycle.RequestEscalated;
                db.RequestStatusHistory.Add(new RequestStatusHistory
                {
                    RequestId = request.RequestId,
                    NewStatus = Lifecycle.RequestEscalated,
                    ChangedBy = systemId.Value,
                    TimeCreated = now,
                });

                var message = $"Resource request #{request.RequestId} had no admin action for 48 hours "
                            + "and has been escalated to the facility manager.";
                foreach (var userId in managers.Append(request.RequestedBy).Distinct())
                    pending.Add((await notify.QueueAsync(
                        userId, EscalatedType, message, nameof(ResourceRequest), request.RequestId), request.RequestId));
            }

            await db.SaveChangesAsync(ct);
            return pending.Select(p => new Dispatch(
                p.N, "requestEscalated", new { requestId = p.RequestId }, "A resource request was escalated")).ToList();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// SM-01's In Progress self-transition: "deadlineApproaching [reminder not yet sent] /
        /// sendDeadlineNotification()". The already-sent notification row is the "reminder not yet
        /// sent" guard, so no extra column is needed to stay idempotent.
        /// </summary>
        public static async Task<List<Dispatch>> SendDeadlineRemindersAsync(
            AppDbContext db, NotificationService notify, DateTime now, CancellationToken ct = default)
        {
            var horizon = now + ReminderHorizon;
            var due = await db.Tasks
                .Where(t => t.Status == "In Progress" && t.Deadline > now && t.Deadline <= horizon)
                .Select(t => new { t.TaskId, t.Title, t.Deadline })
                .ToListAsync(ct);
            if (due.Count == 0) return [];

            var taskIds = due.Select(t => t.TaskId).ToList();
            var assignees = await db.TaskAssignments
                .Where(a => taskIds.Contains(a.TaskId))
                .Select(a => new { a.TaskId, a.UserId })
                .ToListAsync(ct);

            var alreadySent = (await db.Notifications
                .Where(n => n.Type!.TypeName == DeadlineType
                         && n.EntityType == nameof(ProjectTask)
                         && n.EntityId != null
                         && taskIds.Contains(n.EntityId.Value))
                .Select(n => new { TaskId = n.EntityId!.Value, n.UserId })
                .ToListAsync(ct))
                .Select(x => (x.TaskId, x.UserId))
                .ToHashSet();

            var pending = new List<(Notification N, int TaskId)>();
            foreach (var task in due)
                foreach (var a in assignees.Where(a => a.TaskId == task.TaskId))
                {
                    if (!alreadySent.Add((task.TaskId, a.UserId))) continue;
                    pending.Add((await notify.QueueAsync(
                        a.UserId, DeadlineType,
                        $"Task \"{task.Title}\" is due {task.Deadline:yyyy-MM-dd HH:mm} UTC.",
                        nameof(ProjectTask), task.TaskId), task.TaskId));
                }

            if (pending.Count == 0) return [];
            await db.SaveChangesAsync(ct);
            return pending.Select(p => new Dispatch(
                p.N, "deadlineApproaching", new { taskId = p.TaskId }, "A task deadline is approaching")).ToList();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Task_Schedule is "a recurring template that automatically generates tasks on a given day
        /// of the week". One task per template per day; the existing task for today is the guard, so
        /// re-running the sweep does not duplicate it.
        /// </summary>
        public static async Task<List<Dispatch>> GenerateScheduledTasksAsync(
            AppDbContext db, NotificationService notify, DateTime now, CancellationToken ct = default)
        {
            var today = now.DayOfWeek.ToString();
            var templates = await db.TaskSchedules
                .Where(s => s.IsActive && s.DayOfWeek == today)
                .ToListAsync(ct);
            if (templates.Count == 0) return [];

            var projectId = await db.Projects
                .Where(p => p.ProjectName == DailyTasksProject)
                .Select(p => (int?)p.ProjectId).FirstOrDefaultAsync(ct);
            var systemId = await SystemUserIdAsync(db, ct);
            if (projectId is null || systemId is null) return [];

            var dayStart = now.Date;
            var dayEnd = dayStart.AddDays(1);
            var created = new List<(ProjectTask Task, int AssignedTo)>();

            foreach (var template in templates)
            {
                if (await db.Tasks.AnyAsync(t => t.ScheduleId == template.ScheduleId
                        && t.Deadline >= dayStart && t.Deadline < dayEnd, ct))
                    continue;

                var task = new ProjectTask
                {
                    ProjectId = projectId.Value,
                    CreatedBy = template.CreatedBy,
                    ScheduleId = template.ScheduleId,
                    Title = template.Title,
                    Priority = "Normal",
                    Status = Lifecycle.TaskStart,
                    Deadline = dayEnd.AddSeconds(-1),
                };
                db.Tasks.Add(task);
                db.TaskAssignments.Add(new TaskAssignment { Task = task, UserId = template.AssignedTo });
                db.TaskStatusHistory.Add(new TaskStatusHistory
                {
                    Task = task,
                    NewStatus = Lifecycle.TaskStart,
                    ChangedBy = systemId.Value,
                    TimeCreated = now,
                });
                created.Add((task, template.AssignedTo));
            }

            if (created.Count == 0) return [];
            await db.SaveChangesAsync(ct);   // assigns TaskId, which the notifications point at

            var pending = new List<(Notification N, int TaskId)>();
            foreach (var (task, assignedTo) in created)
                pending.Add((await notify.QueueAsync(
                    assignedTo, ScheduledType,
                    $"Today's recurring task \"{task.Title}\" has been created and assigned to you.",
                    nameof(ProjectTask), task.TaskId), task.TaskId));

            await db.SaveChangesAsync(ct);
            return pending.Select(p => new Dispatch(
                p.N, "taskAssigned", new { taskId = p.TaskId }, "A recurring task was assigned to you")).ToList();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// The actor recorded against a clock-driven transition. Every history and audit row needs a
        /// real user (the audit NFR requires the FK to resolve), and these moves have no human behind
        /// them, so the seeded, login-disabled System account owns them.
        /// </summary>
        private static Task<int?> SystemUserIdAsync(AppDbContext db, CancellationToken ct) =>
            db.Users.Where(u => u.Email == SystemAccountEmail)
                .Select(u => (int?)u.UserId).FirstOrDefaultAsync(ct);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
