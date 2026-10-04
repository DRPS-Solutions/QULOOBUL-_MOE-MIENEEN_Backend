using System.Text;
using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Shared.Reports;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-09: aggregate task/workload/project data over a period, and export it.</summary>
    public class ReportingService(AppDbContext db, BlobStorageService blob)
    {
        /// <summary>Private container for database/report exports (architecture doc, Blob Storage).</summary>
        public const string Container = "reports";
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Builds the dashboard figures and records that the report was run. The Report row is the
        /// "who asked what, and when" trail the audit NFR expects; the figures themselves are
        /// recomputed rather than stored, so a later read never shows stale numbers.
        /// </summary>
        public async Task<ReportDto> BuildAsync(ReportRequest req, int generatedBy) =>
            (await BuildAndRecordAsync(req, generatedBy)).Dto;
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-09 "may export the report": a CSV in Blob Storage, handed back as a read SAS.</summary>
        public async Task<ReportExportResponse> ExportAsync(ReportRequest req, int generatedBy)
        {
            var (dto, report) = await BuildAndRecordAsync(req, generatedBy);

            var blobName = $"{report.ReportId}-{req.ReportType}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            await blob.UploadAsync(Container, blobName, BinaryData.FromString(ToCsv(dto)));
            report.FilePath = $"{Container}/{blobName}";
            await db.SaveChangesAsync();

            var uri = await blob.CreateReadSasAsync(Container, blobName);
            return new ReportExportResponse(report.ReportId, uri.ToString(), DateTime.UtcNow.AddMinutes(15));
        }
//----------------------------------------------------------------------------------------------------------------------//
        private async Task<(ReportDto Dto, Report Row)> BuildAndRecordAsync(ReportRequest req, int generatedBy)
        {
            if (req.PeriodEnd < req.PeriodStart)
                throw new ArgumentException("PeriodEnd must be on or after PeriodStart.");
            // Beyond a year the weekly trend runs to 50+ points and the charts stop being readable.
            // The web sends PeriodEnd as 23:59:59 on the To date, so a 4 Oct - 4 Oct range must still pass (P2).
            if (req.PeriodEnd >= req.PeriodStart.Date.AddYears(1).AddDays(1))
                throw new ArgumentException("That period is longer than a year. Pick a narrower date range.");

            var tasks = db.Tasks.Where(t => t.Deadline >= req.PeriodStart && t.Deadline <= req.PeriodEnd);
            var doneMoves = db.TaskStatusHistory.Where(h => h.NewStatus == "Done"
                && h.TimeCreated >= req.PeriodStart && h.TimeCreated <= req.PeriodEnd);

            // UC-09 exception flow: "Insufficient data available: system displays a message prompting
            // a wider date range". Refused before the Report row is written, so it is not logged as run.
            if (!await tasks.AnyAsync() && !await doneMoves.AnyAsync())
                throw new ArgumentException(
                    $"Not enough data between {req.PeriodStart:yyyy-MM-dd} and {req.PeriodEnd:yyyy-MM-dd}. " +
                    "Try a wider date range.");

            var completion = await tasks
                .GroupBy(t => t.Priority)
                .Select(g => new TaskCompletionStat(
                    g.Key,
                    g.Count(t => t.Status == "Done" || t.Status == Lifecycle.TaskArchived),
                    g.Count(t => !Lifecycle.TaskFinished.Contains(t.Status))))
                .ToListAsync();

            var workload = await db.Users.Where(u => u.IsActive)
                .Select(u => new WorkloadStat(
                    u.UserId, u.FirstName + " " + u.Surname,
                    db.TaskAssignments.Count(a => a.UserId == u.UserId),
                    db.TaskAssignments.Count(a => a.UserId == u.UserId && (a.Task!.Status == "Done" || a.Task!.Status == Lifecycle.TaskArchived)),
                    db.TaskAssignments.Count(a => a.UserId == u.UserId && !Lifecycle.TaskFinished.Contains(a.Task!.Status) && a.Task.Deadline < DateTime.UtcNow)))
                .ToListAsync();

            var progress = await db.Projects.Select(p => new
            {
                p.ProjectId,
                p.ProjectName,
                Total = db.Tasks.Count(t => t.ProjectId == p.ProjectId),
                Done = db.Tasks.Count(t => t.ProjectId == p.ProjectId && (t.Status == "Done" || t.Status == Lifecycle.TaskArchived)),
            }).ToListAsync();

            var progressStats = progress.Select(p => new ProjectProgressStat(
                p.ProjectId, p.ProjectName, p.Total, p.Done,
                p.Total == 0 ? 0 : Math.Round(100.0 * p.Done / p.Total, 1))).ToList();

            var completions = await doneMoves.Select(h => new { h.TaskId, h.TimeCreated }).ToListAsync();
            var trend = Trend(req.PeriodStart, req.PeriodEnd, completions.Select(c => (c.TaskId, c.TimeCreated)));

            var report = new Report
            {
                GeneratedBy = generatedBy,
                SubjectUserId = req.SubjectUserId,
                ReportType = req.ReportType,
                DateGenerated = DateTime.UtcNow,
                PeriodStart = req.PeriodStart,
                PeriodEnd = req.PeriodEnd,
                FilePath = string.Empty,     // set by ExportAsync when the report is exported
            };
            db.Reports.Add(report);
            await db.SaveChangesAsync();

            return (new ReportDto(req.ReportType, req.PeriodStart, req.PeriodEnd,
                completion, workload, progressStats, trend), report);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// UC-09 "progress over time": tasks completed per bucket plus the running total. Daily buckets
        /// up to a month, weekly beyond that. A task completed twice in one bucket (reopened) counts once.
        /// </summary>
        public static List<ProgressPoint> Trend(
            DateTime start, DateTime end, IEnumerable<(int TaskId, DateTime At)> completions)
        {
            var step = end - start <= TimeSpan.FromDays(31) ? TimeSpan.FromDays(1) : TimeSpan.FromDays(7);
            var byBucket = completions
                .GroupBy(c => (int)((c.At - start).Ticks / step.Ticks))
                .ToDictionary(g => g.Key, g => g.Select(c => c.TaskId).Distinct().Count());

            var points = new List<ProgressPoint>();
            var running = 0;
            for (var i = 0; start + step * i <= end; i++)
            {
                var done = byBucket.GetValueOrDefault(i);
                running += done;
                points.Add(new ProgressPoint(start + step * i, done, running));
            }
            return points;
        }
//----------------------------------------------------------------------------------------------------------------------//
        private static string ToCsv(ReportDto r)
        {
            var sb = new StringBuilder();
            void Row(params object[] cells) => sb.AppendLine(string.Join(",", cells.Select(c => Cell(c.ToString()))));

            Row("Report", r.ReportType, "From", r.PeriodStart.ToString("yyyy-MM-dd"), "To", r.PeriodEnd.ToString("yyyy-MM-dd"));
            sb.AppendLine();
            Row("Priority", "Completed", "Outstanding");
            foreach (var c in r.Completion) Row(c.Bucket, c.Completed, c.Outstanding);
            sb.AppendLine();
            Row("UserId", "Name", "Assigned", "Completed", "Overdue");
            foreach (var w in r.Workload) Row(w.UserId, w.FullName, w.Assigned, w.Completed, w.Overdue);
            sb.AppendLine();
            Row("ProjectId", "Project", "Total", "Done", "PercentComplete");
            foreach (var p in r.Progress) Row(p.ProjectId, p.ProjectName, p.Total, p.Done, p.PercentComplete);
            sb.AppendLine();
            Row("From", "Completed", "CumulativeCompleted");
            foreach (var t in r.Trend) Row(t.BucketStart.ToString("yyyy-MM-dd"), t.Completed, t.CumulativeCompleted);
            return sb.ToString();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Quoted, and a leading = + - @ neutralised so a user-entered name cannot run as a
        /// spreadsheet formula when an admin opens the export (CSV injection).
        /// Reference: OWASP Foundation, "CSV Injection." Accessed: Oct. 04, 2026. [Online]. Available: https://owasp.org/www-community/attacks/CSV_Injection
        /// Reference: Y. Shafranovich, "Common Format and MIME Type for Comma-Separated Values (CSV) Files," Internet Engineering Task Force (IETF), RFC 4180, Oct. 2005. Accessed: Oct. 04, 2026. [Online]. Available: https://www.rfc-editor.org/rfc/rfc4180
        /// </summary>
        public static string Cell(string? value)
        {
            var v = value ?? string.Empty;
            if (v.Length > 0 && "=+-@".Contains(v[0]) && !double.TryParse(v, out _)) v = "'" + v;
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
