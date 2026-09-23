using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// Recurring weekly templates (Task_Schedule). MaintenanceJobs turns them into tasks; this is
    /// where people create, edit and pause them. There is no delete: "the isActive flag lets a
    /// recurrence be paused without deleting it", and generated tasks keep pointing at the template.
    /// </summary>
    public class TaskScheduleService(
        AppDbContext db, AuditService audit, AccessGuard guard, TaskAssignmentService assignments)
    {
        /// <summary>An admin sees every schedule; anyone else the ones they created or are assigned to.</summary>
        public async Task<List<TaskScheduleDto>> ListAsync(int userId)
        {
            var q = db.TaskSchedules.AsQueryable();
            if (!await guard.IsAdminAsync(userId))
                q = q.Where(s => s.CreatedBy == userId || s.AssignedTo == userId);
            return await q.OrderBy(s => s.DayOfWeek).ThenBy(s => s.Title).Select(s => Map(s)).ToListAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<TaskScheduleDto> CreateAsync(CreateTaskScheduleRequest req, int actorId)
        {
            await assignments.EnsureMayAssignAsync(actorId, req.AssignedTo);
            var schedule = new TaskSchedule
            {
                CreatedBy = actorId,
                AssignedTo = req.AssignedTo,
                Title = req.Title.Trim(),
                DayOfWeek = ParseDay(req.DayOfWeek),
                IsActive = true,
            };
            db.TaskSchedules.Add(schedule);
            await db.SaveChangesAsync();
            await audit.RecordAndSaveAsync(actorId, "schedule.create", nameof(TaskSchedule), schedule.ScheduleId);
            return Map(schedule);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Creator or admin. Changes apply from the next generated task; past ones are untouched.</summary>
        public async Task<TaskScheduleDto> UpdateAsync(int scheduleId, UpdateTaskScheduleRequest req, int actorId)
        {
            var s = await db.TaskSchedules.FindAsync(scheduleId)
                ?? throw new KeyNotFoundException($"Schedule {scheduleId} not found.");
            if (s.CreatedBy != actorId && !await guard.IsAdminAsync(actorId))
                throw new UnauthorizedAccessException("Only the schedule's creator or an admin may change it.");

            if (req.AssignedTo is int assignee && assignee != s.AssignedTo)
            {
                await assignments.EnsureMayAssignAsync(actorId, assignee);
                s.AssignedTo = assignee;
            }
            if (!string.IsNullOrWhiteSpace(req.Title)) s.Title = req.Title.Trim();
            if (req.DayOfWeek is { } day) s.DayOfWeek = ParseDay(day);
            if (req.IsActive is bool active) s.IsActive = active;

            audit.Record(actorId, req.IsActive == false ? "schedule.pause" : "schedule.update",
                nameof(TaskSchedule), scheduleId);
            await db.SaveChangesAsync();
            return Map(s);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Stored as DayOfWeek.ToString(), which is exactly what MaintenanceJobs compares against.</summary>
        private static string ParseDay(string day) =>
            Enum.GetNames<DayOfWeek>().FirstOrDefault(n => n.Equals(day.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"\"{day}\" is not a day of the week.");

        private static TaskScheduleDto Map(TaskSchedule s) =>
            new(s.ScheduleId, s.Title, s.DayOfWeek, s.AssignedTo, s.CreatedBy, s.IsActive);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
