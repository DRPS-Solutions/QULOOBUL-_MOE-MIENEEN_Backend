using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// UC-04 / SD-02: the one place that decides whether a task assignment is legal, writes the
    /// assignment, moves the task's status, records the history and audit rows, and raises the
    /// SignalR event. All writes commit in a single Unit of Work (one SaveChanges); the push
    /// happens only after the commit succeeds.
    /// Also owns the ProjectTask lifecycle guard (SM-01).
    /// Reference: M. Fowler, Patterns of Enterprise Application Architecture. Boston, MA, USA: Addison-Wesley, 2003.
    /// Reference: E. Evans, Domain-Driven Design: Tackling Complexity in the Heart of Software. Boston, MA, USA: Addison-Wesley, 2007.
    /// </summary>
    public class TaskAssignmentService(
        AppDbContext db, AuditService audit, NotificationService notify, EmailService email, AccessGuard guard)
    {
        /// <summary>
        /// Assign <paramref name="assigneeId"/> to <paramref name="taskId"/>. Tasks may be assigned
        /// down the role hierarchy or sideways, never up (architecture doc, "Role.hierarchy_level");
        /// daily tasks strictly down. A "To Do" task moves to "In Progress" on first assignment.
        /// </summary>
        public async Task<TaskAssignment> AssignAsync(int taskId, int assigneeId, int assignedById)
        {
            // Without this a stranger could assign themselves, and an assignee may open the task.
            await guard.EnsureTaskAsync(assignedById, taskId);
            var task = await db.Tasks.FindAsync(taskId)
                ?? throw new KeyNotFoundException($"Task {taskId} not found.");

            if (Is(task.Status, Lifecycle.TaskCancelled) || Is(task.Status, Lifecycle.TaskArchived))
                throw new InvalidOperationException($"Cannot assign a task that is {task.Status.ToLowerInvariant()}.");
            var isDaily = await db.Projects.AnyAsync(p => p.ProjectId == task.ProjectId && p.ProjectName == DailyTasksProject);
            await EnsureMayAssignAsync(assignedById, assigneeId, strictlyDown: isDaily);

            if (await db.TaskAssignments.AnyAsync(a => a.TaskId == taskId && a.UserId == assigneeId))
                throw new InvalidOperationException("That user is already assigned to this task.");

            var assignment = new TaskAssignment { TaskId = taskId, UserId = assigneeId };
            db.TaskAssignments.Add(assignment);

            if (string.IsNullOrEmpty(task.Status) ||
                task.Status.Equals(Lifecycle.TaskStart, StringComparison.OrdinalIgnoreCase))
                MoveStatus(task, "In Progress", assignedById);

            var notification = await notify.QueueAsync(assigneeId, "TaskAssigned",
                $"You have been assigned to task \"{task.Title}\".", nameof(ProjectTask), taskId);
            audit.Record(assignedById, "task.assign", nameof(TaskAssignment), taskId);

            await db.SaveChangesAsync(); // UoW commit: assignment + status + history + notification + audit
            await notify.PushAsync(assigneeId, "taskAssigned",
                new { taskId, title = task.Title, assignment.AssignmentId });
            await email.SendAsync(notification, "A task was assigned to you");
            await notify.BroadcastAsync("boardChanged", new { projectId = task.ProjectId });

            return assignment;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-04 reassign: take <paramref name="userId"/> off a task. Same hierarchy rule as
        /// assigning: nobody removes someone more senior than themselves.</summary>
        public async Task UnassignAsync(int taskId, int userId, int actorId)
        {
            await guard.EnsureTaskAsync(actorId, taskId);
            var assignment = await db.TaskAssignments.Include(a => a.User).Include(a => a.Task)
                .FirstOrDefaultAsync(a => a.TaskId == taskId && a.UserId == userId)
                ?? throw new KeyNotFoundException("That user is not assigned to this task.");
            var actor = await db.Users.FindAsync(actorId)
                ?? throw new KeyNotFoundException($"User {actorId} not found.");

            if (await LevelOf(actor.RoleId) > await LevelOf(assignment.User!.RoleId))
                throw new InvalidOperationException("You may not unassign someone higher in the hierarchy.");

            db.TaskAssignments.Remove(assignment);
            audit.Record(actorId, "task.unassign", nameof(TaskAssignment), taskId);
            await db.SaveChangesAsync();
            await notify.BroadcastAsync("boardChanged", new { projectId = assignment.Task!.ProjectId });
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-01 guarded status change, callable directly by a task-board move. The table in
        /// Lifecycle says which moves exist; the guards that need data are checked here.</summary>
        /// <remarks><paramref name="completedById"/> names who did the work when it wasn't the caller (a manager
        /// ticking off a daily task on someone's behalf); it need not be an assignee.</remarks>
        public async Task ChangeStatusAsync(int taskId, string newStatus, int changedById, int? completedById = null)
        {
            var task = await db.Tasks.FindAsync(taskId)
                ?? throw new KeyNotFoundException($"Task {taskId} not found.");
            var from = string.IsNullOrEmpty(task.Status) ? Lifecycle.TaskStart : task.Status;

            if (Is(newStatus, Lifecycle.TaskArchived))
                throw new InvalidOperationException("Tasks are archived automatically when their project closes.");
            if (Is(newStatus, Lifecycle.TaskCancelled) && !Is(from, newStatus) && !await guard.IsAdminAsync(changedById))
                throw new UnauthorizedAccessException("Only an admin may cancel a task.");
            if (Is(from, "Done") && Is(newStatus, "In Progress"))
                await guard.EnsurePermissionAsync(changedById, Permissions.ReopenTask);
            if (Is(from, Lifecycle.TaskStart) && Is(newStatus, "In Progress")
                && !await db.TaskAssignments.AnyAsync(a => a.TaskId == taskId))
                throw new InvalidOperationException("Assign the task to someone before starting it.");
            if (completedById is { } cid && !await db.Users.AnyAsync(u => u.UserId == cid && u.IsActive))
                throw new InvalidOperationException("The person who completed the task must be an active user.");
            // Crediting someone else is a manager's call (a daily task ticked off on a team member's behalf),
            // so only someone senior to them may; anyone could otherwise put their work on a colleague's record.
            if (completedById is { } who && who != changedById
                && await LevelOfUserAsync(changedById) >= await LevelOfUserAsync(who))
                throw new UnauthorizedAccessException("You can only credit completion to someone below you in the hierarchy.");

            MoveStatus(task, newStatus, changedById, completedById);

            // cancel(admin) / notify assignee
            var notices = new List<Notification>();
            if (Is(newStatus, Lifecycle.TaskCancelled) && !Is(from, newStatus))
                foreach (var userId in await db.TaskAssignments.Where(a => a.TaskId == taskId).Select(a => a.UserId).ToListAsync())
                    notices.Add(await notify.QueueAsync(userId, "TaskCancelled",
                        $"Task \"{task.Title}\" was cancelled.", nameof(ProjectTask), taskId));

            audit.Record(changedById, "task.status", nameof(ProjectTask), taskId);
            await db.SaveChangesAsync();
            foreach (var n in notices)
            {
                await notify.PushAsync(n.UserId, "taskCancelled", new { taskId });
                await email.SendAsync(n, "A task was cancelled");
            }
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-01 archive() [parent project is closed]: every Done task in the project.
        /// The caller owns the guard and the SaveChanges.</summary>
        public async Task ArchiveDoneTasksAsync(int projectId, int actorId)
        {
            foreach (var task in await db.Tasks.Where(t => t.ProjectId == projectId && t.Status == "Done").ToListAsync())
                MoveStatus(task, Lifecycle.TaskArchived, actorId);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Event cancelled: every open task SM-01 lets cancel (To Do, In Progress) is cancelled.
        /// Blocked has no Cancelled edge in SM-01, so a blocked task is left for its owner to resolve.</summary>
        public async Task CancelOpenTasksAsync(int projectId, int actorId)
        {
            foreach (var task in (await db.Tasks.Where(t => t.ProjectId == projectId).ToListAsync())
                         .Where(t => Lifecycle.TaskCanMove(t.Status, Lifecycle.TaskCancelled)))
                MoveStatus(task, Lifecycle.TaskCancelled, actorId);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// SM-03 deactivate() "/ reassign open tasks". The leaver comes off every unfinished task; a task
        /// left with nobody on it goes to its project owner, or to the deactivating admin when the owner
        /// is the leaver or inactive. Queued into the caller's Unit of Work; returns the notifications
        /// to deliver once it commits.
        /// </summary>
        public async Task<List<Notification>> ReassignOpenTasksAsync(int userId, int actorId)
        {
            var open = await db.TaskAssignments.Include(a => a.Task!).ThenInclude(t => t.Project)
                .Where(a => a.UserId == userId && !Lifecycle.TaskFinished.Contains(a.Task!.Status))
                .ToListAsync();

            var notices = new List<Notification>();
            foreach (var a in open)
            {
                db.TaskAssignments.Remove(a);
                audit.Record(actorId, "task.unassign", nameof(TaskAssignment), a.TaskId);
                if (await db.TaskAssignments.AnyAsync(x => x.TaskId == a.TaskId && x.UserId != userId)) continue;

                var ownerId = a.Task!.Project!.OwnerId;
                var heir = ownerId != userId && await db.Users.AnyAsync(u => u.UserId == ownerId && u.IsActive)
                    ? ownerId : actorId;
                db.TaskAssignments.Add(new TaskAssignment { TaskId = a.TaskId, UserId = heir });
                audit.Record(actorId, "task.assign", nameof(TaskAssignment), a.TaskId);
                notices.Add(await notify.QueueAsync(heir, "TaskAssigned",
                    $"Task \"{a.Task.Title}\" was reassigned to you because its assignee was deactivated.",
                    nameof(ProjectTask), a.TaskId));
            }

            // Recurring schedules would keep generating tasks for the leaver (test plan U6): hand them to
            // whoever set the schedule up, or to the acting admin when that's the leaver or inactive.
            foreach (var s in await db.TaskSchedules.Where(s => s.AssignedTo == userId).ToListAsync())
            {
                var heir = s.CreatedBy != userId && await db.Users.AnyAsync(u => u.UserId == s.CreatedBy && u.IsActive)
                    ? s.CreatedBy : actorId;
                s.AssignedTo = heir;
                audit.Record(actorId, "schedule.reassign", nameof(TaskSchedule), s.ScheduleId);
                notices.Add(await notify.QueueAsync(heir, "TaskAssigned",
                    $"Recurring task \"{s.Title}\" was reassigned to you because its assignee was deactivated.",
                    nameof(TaskSchedule), s.ScheduleId));
            }
            return notices;
        }
//----------------------------------------------------------------------------------------------------------------------//
        private static bool Is(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
//----------------------------------------------------------------------------------------------------------------------//
        private void MoveStatus(ProjectTask task, string newStatus, int changedById, int? completedById = null)
        {
            if (string.Equals(task.Status, newStatus, StringComparison.OrdinalIgnoreCase))
                return;
            Lifecycle.EnsureTaskMove(task.Status, newStatus);

            task.Status = newStatus;
            if (newStatus.Equals("Done", StringComparison.OrdinalIgnoreCase))
            {
                task.PercentComplete = 100;
                task.CompletedBy = completedById ?? changedById;
            }

            // Task.status is a cache of the latest history row, maintained in the same transaction
            // (architecture doc, "Current-value columns kept alongside the history").
            db.TaskStatusHistory.Add(new TaskStatusHistory
            {
                TaskId = task.TaskId,
                NewStatus = newStatus,
                ChangedBy = changedById,
                TimeCreated = DateTime.UtcNow,
            });
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// The assignment rule on its own, shared with recurring schedules: the assignee is active and
        /// not higher in the hierarchy than the assigner (lower level = more authority). Daily work
        /// (<paramref name="strictlyDown"/>) is delegated: a facility manager hands it to staff, an
        /// admin to anyone below them, never to a peer or to themselves.
        /// </summary>
        public async Task EnsureMayAssignAsync(int assignerId, int assigneeId, bool strictlyDown = false)
        {
            var assignee = await db.Users.FindAsync(assigneeId)
                ?? throw new KeyNotFoundException($"User {assigneeId} not found.");
            var assigner = await db.Users.FindAsync(assignerId)
                ?? throw new KeyNotFoundException($"User {assignerId} not found.");

            if (!assignee.IsActive)
                throw new InvalidOperationException("Cannot assign a task to a deactivated user.");
            var (from, to) = (await LevelOf(assigner.RoleId), await LevelOf(assignee.RoleId));
            if (from > to)
                throw new InvalidOperationException("A task may not be assigned to someone higher in the hierarchy.");
            if (strictlyDown && from == to)
                throw new InvalidOperationException("Daily tasks are assigned to someone below you in the hierarchy.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        private Task<int> LevelOfUserAsync(int userId) =>
            db.Users.Where(u => u.UserId == userId).Select(u => u.Role!.HierarchyLevel).FirstAsync();
//----------------------------------------------------------------------------------------------------------------------//
        private async Task<int> LevelOf(int roleId)
        {
            var role = await db.Roles.FindAsync(roleId)
                ?? throw new InvalidOperationException($"Role {roleId} not found.");
            return role.HierarchyLevel;
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//