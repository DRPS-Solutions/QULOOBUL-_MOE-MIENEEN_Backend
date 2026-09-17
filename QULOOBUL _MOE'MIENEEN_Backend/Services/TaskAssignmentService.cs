using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// UC-04 / SD-02: the one place that decides whether a task assignment is legal, writes the
    /// assignment, moves the task's status, records the history and audit rows, and raises the
    /// SignalR event. All writes commit in a single Unit of Work (one SaveChanges); the push
    /// happens only after the commit succeeds.
    /// Also owns the ProjectTask lifecycle guard (SM-01).
    /// </summary>
    public class TaskAssignmentService(AppDbContext db, AuditService audit, NotificationService notify)
    {
        /// <summary>
        /// Assign <paramref name="assigneeId"/> to <paramref name="taskId"/>. Tasks may be assigned
        /// down the role hierarchy or sideways, never up (architecture doc, "Role.hierarchy_level").
        /// A "To Do" task moves to "In Progress" on first assignment.
        /// </summary>
        public async Task<TaskAssignment> AssignAsync(int taskId, int assigneeId, int assignedById)
        {
            var task = await db.Tasks.FindAsync(taskId)
                ?? throw new KeyNotFoundException($"Task {taskId} not found.");

            var assignee = await db.Users.FindAsync(assigneeId)
                ?? throw new KeyNotFoundException($"User {assigneeId} not found.");
            var assigner = await db.Users.FindAsync(assignedById)
                ?? throw new KeyNotFoundException($"User {assignedById} not found.");

            if (!assignee.IsActive)
                throw new InvalidOperationException("Cannot assign a task to a deactivated user.");

            // Lower hierarchy level = more authority. The assigner must be at least as senior.
            if (await LevelOf(assigner.RoleId) > await LevelOf(assignee.RoleId))
                throw new InvalidOperationException("A task may not be assigned to someone higher in the hierarchy.");

            if (await db.TaskAssignments.AnyAsync(a => a.TaskId == taskId && a.UserId == assigneeId))
                throw new InvalidOperationException("That user is already assigned to this task.");

            var assignment = new TaskAssignment { TaskId = taskId, UserId = assigneeId };
            db.TaskAssignments.Add(assignment);

            if (string.IsNullOrEmpty(task.Status) ||
                task.Status.Equals(Lifecycle.TaskStart, StringComparison.OrdinalIgnoreCase))
                MoveStatus(task, "In Progress", assignedById);

            await notify.QueueAsync(assigneeId, "TaskAssigned",
                $"You have been assigned to task \"{task.Title}\".", nameof(ProjectTask), taskId);
            audit.Record(assignedById, "task.assign", nameof(TaskAssignment), taskId);

            await db.SaveChangesAsync(); // UoW commit: assignment + status + history + notification + audit
            await notify.PushAsync(assigneeId, "taskAssigned",
                new { taskId, title = task.Title, assignment.AssignmentId });
            await notify.BroadcastAsync("boardChanged", new { projectId = task.ProjectId });

            return assignment;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-04 reassign: take <paramref name="userId"/> off a task. Same hierarchy rule as
        /// assigning: nobody removes someone more senior than themselves.</summary>
        public async Task UnassignAsync(int taskId, int userId, int actorId)
        {
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
        /// <summary>SM-01 guarded status change, callable directly by a task-board move.</summary>
        public async Task ChangeStatusAsync(int taskId, string newStatus, int changedById)
        {
            var task = await db.Tasks.FindAsync(taskId)
                ?? throw new KeyNotFoundException($"Task {taskId} not found.");
            MoveStatus(task, newStatus, changedById);
            audit.Record(changedById, "task.status", nameof(ProjectTask), taskId);
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        private void MoveStatus(ProjectTask task, string newStatus, int changedById)
        {
            if (string.Equals(task.Status, newStatus, StringComparison.OrdinalIgnoreCase))
                return;
            Lifecycle.EnsureTaskMove(task.Status, newStatus);

            task.Status = newStatus;
            if (newStatus.Equals("Done", StringComparison.OrdinalIgnoreCase))
            {
                task.PercentComplete = 100;
                task.CompletedBy = changedById;
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
        private async Task<int> LevelOf(int roleId)
        {
            var role = await db.Roles.FindAsync(roleId)
                ?? throw new InvalidOperationException($"Role {roleId} not found.");
            return role.HierarchyLevel;
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//