using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Shared.Reports;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-09: aggregate task/workload/project data over a period.</summary>
    public class ReportingService(AppDbContext db)
    {
        /// <summary>
        /// Builds the dashboard figures and records that the report was run. The Report row is the
        /// "who asked what, and when" trail the audit NFR expects; the figures themselves are
        /// recomputed rather than stored, so a later read never shows stale numbers.
        /// </summary>
        public async Task<ReportDto> BuildAsync(ReportRequest req, int generatedBy)
        {
            if (req.PeriodEnd < req.PeriodStart)
                throw new ArgumentException("PeriodEnd must be on or after PeriodStart.");

            var tasks = db.Tasks.Where(t => t.Deadline >= req.PeriodStart && t.Deadline <= req.PeriodEnd);

            var completion = await tasks
                .GroupBy(t => t.Priority)
                .Select(g => new TaskCompletionStat(
                    g.Key,
                    g.Count(t => t.Status == "Done"),
                    g.Count(t => t.Status != "Done")))
                .ToListAsync();

            var workload = await db.Users.Where(u => u.IsActive)
                .Select(u => new WorkloadStat(
                    u.UserId, u.FirstName + " " + u.Surname,
                    db.TaskAssignments.Count(a => a.UserId == u.UserId),
                    db.TaskAssignments.Count(a => a.UserId == u.UserId && a.Task!.Status == "Done"),
                    db.TaskAssignments.Count(a => a.UserId == u.UserId && a.Task!.Status != "Done" && a.Task.Deadline < DateTime.UtcNow)))
                .ToListAsync();

            var progress = await db.Projects.Select(p => new
            {
                p.ProjectId,
                p.ProjectName,
                Total = db.Tasks.Count(t => t.ProjectId == p.ProjectId),
                Done = db.Tasks.Count(t => t.ProjectId == p.ProjectId && t.Status == "Done"),
            }).ToListAsync();

            var progressStats = progress.Select(p => new ProjectProgressStat(
                p.ProjectId, p.ProjectName, p.Total, p.Done,
                p.Total == 0 ? 0 : Math.Round(100.0 * p.Done / p.Total, 1))).ToList();

            db.Reports.Add(new Report
            {
                GeneratedBy = generatedBy,
                SubjectUserId = req.SubjectUserId,
                ReportType = req.ReportType,
                DateGenerated = DateTime.UtcNow,
                PeriodStart = req.PeriodStart,
                PeriodEnd = req.PeriodEnd,
                FilePath = string.Empty,     // set once UC-09's "export" produces a file in Blob Storage
            });
            await db.SaveChangesAsync();

            return new ReportDto(req.ReportType, req.PeriodStart, req.PeriodEnd, completion, workload, progressStats);
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//