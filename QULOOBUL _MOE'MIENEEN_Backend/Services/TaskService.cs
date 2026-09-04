using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-02 / UC-04: task CRUD and the live kanban board. Status moves delegate to
    /// TaskAssignmentService.ChangeStatusAsync so SM-01 stays in one place.</summary>
    public class TaskService(
        AppDbContext db, TaskAssignmentService assignments, AuditService audit, NotificationService notify)
    {
        public async Task<IReadOnlyList<TaskCardDto>> BoardAsync(int projectId) =>
            await db.Tasks.Where(t => t.ProjectId == projectId)
                .Select(t => new TaskCardDto(
                    t.TaskId, t.ProjectId, t.Title, t.Status, t.Priority, t.PercentComplete, t.Deadline,
                    db.TaskAssignments.Where(a => a.TaskId == t.TaskId).Select(a => a.UserId).ToList()))
                .ToListAsync();

        public async Task<TaskCardDto> CreateAsync(CreateTaskRequest req, int creatorId)
        {
            if (!await db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
                throw new KeyNotFoundException($"Project {req.ProjectId} not found.");

            var task = new ProjectTask
            {
                ProjectId = req.ProjectId,
                CreatedBy = creatorId,
                Title = req.Title,
                Priority = string.IsNullOrWhiteSpace(req.Priority) ? "Normal" : req.Priority,
                Status = Lifecycle.TaskStart,
                Deadline = req.Deadline,
            };
            db.Tasks.Add(task);
            audit.Record(creatorId, "task.create", nameof(ProjectTask), 0);
            await db.SaveChangesAsync();

            var card = await Card(task.TaskId);
            await notify.PushAsync(creatorId, "boardChanged", new { projectId = req.ProjectId });
            return card;
        }

        public async Task<TaskCardDto> UpdateAsync(int taskId, UpdateTaskRequest req, int actorId)
        {
            var task = await db.Tasks.FindAsync(taskId)
                ?? throw new KeyNotFoundException($"Task {taskId} not found.");

            if (req.Title is { } title) task.Title = title;
            if (req.Priority is { } pr) task.Priority = pr;
            if (req.Deadline is { } d) task.Deadline = d;
            if (req.PercentComplete is { } pc) task.PercentComplete = pc;

            audit.Record(actorId, "task.update", nameof(ProjectTask), taskId);
            await db.SaveChangesAsync();
            return await Card(taskId);
        }

        /// <summary>Kanban column move — SM-01 enforced by TaskAssignmentService.</summary>
        public async Task MoveAsync(int taskId, string newStatus, int actorId)
        {
            await assignments.ChangeStatusAsync(taskId, newStatus, actorId);
            var projectId = await db.Tasks.Where(t => t.TaskId == taskId).Select(t => t.ProjectId).FirstAsync();
            await notify.PushAsync(actorId, "boardChanged", new { projectId });
        }

        public async Task<TaskCommentDto> AddCommentAsync(int taskId, string text, int userId)
        {
            if (!await db.Tasks.AnyAsync(t => t.TaskId == taskId))
                throw new KeyNotFoundException($"Task {taskId} not found.");

            var c = new TaskComment { TaskId = taskId, UserId = userId, Comment = text, TimeCreated = DateTime.UtcNow };
            db.TaskComments.Add(c);
            await db.SaveChangesAsync();
            return new TaskCommentDto(c.CommentId, c.UserId, c.Comment, c.TimeCreated);
        }

        public async Task<TaskDetailDto> DetailAsync(int taskId)
        {
            var task = await db.Tasks.FindAsync(taskId)
                ?? throw new KeyNotFoundException($"Task {taskId} not found.");
            var comments = await db.TaskComments.Where(c => c.TaskId == taskId)
                .OrderBy(c => c.TimeCreated)
                .Select(c => new TaskCommentDto(c.CommentId, c.UserId, c.Comment, c.TimeCreated)).ToListAsync();
            var history = await db.TaskStatusHistory.Where(h => h.TaskId == taskId)
                .OrderBy(h => h.TimeCreated)
                .Select(h => new TaskStatusHistoryDto(h.NewStatus, h.ChangedBy, h.TimeCreated)).ToListAsync();
            return new TaskDetailDto(task.TaskId, task.ProjectId, task.Title, task.Status, task.Priority,
                task.PercentComplete, task.Deadline, task.CreatedBy, task.CompletedBy, comments, history);
        }

        private async Task<TaskCardDto> Card(int taskId) =>
            (await BoardAsync(await db.Tasks.Where(t => t.TaskId == taskId).Select(t => t.ProjectId).FirstAsync()))
            .First(c => c.TaskId == taskId);
    }
}