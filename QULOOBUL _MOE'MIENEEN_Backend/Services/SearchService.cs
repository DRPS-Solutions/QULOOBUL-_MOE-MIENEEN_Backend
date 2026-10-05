using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;
using QULOOBUL__MOE_MIENEEN_Shared.Search;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// One search box over everything the caller may already open. Every query starts from the same
    /// visibility rule its own list endpoint uses, so search can never surface a record the list hides.
    /// </summary>
    public class SearchService(
        AppDbContext db, AccessGuard guard, ProjectService projects, RequestReviewService requests,
        MeetingMinutesService meetings)
    {
        public const int MinLength = 2, PerKind = 10;
//----------------------------------------------------------------------------------------------------------------------//
        // ponytail: sequential LIKE '%q%' scans per kind; add full-text indexes if the tables grow large.
        public async Task<List<SearchResultDto>> SearchAsync(string? q, int userId)
        {
            q = q?.Trim();
            if (q is null || q.Length < MinLength) return [];
            var isAdmin = await guard.IsAdminAsync(userId);
            var results = new List<SearchResultDto>();

            results.AddRange(await guard.VisibleTasks(db.Tasks, userId, isAdmin)
                .Where(t => t.Title.Contains(q) || (t.Description != null && t.Description.Contains(q)))
                .OrderBy(t => t.Deadline).Take(PerKind)
                .Select(t => new SearchResultDto("ProjectTask", t.TaskId, t.Title, t.Status))
                .ToListAsync());

            results.AddRange(await (await requests.VisibleAsync(userId))
                .Where(r => r.Category!.CategoryName.Contains(q) || (r.Detail != null && r.Detail.Contains(q)))
                .OrderByDescending(r => r.DateSubmitted).Take(PerKind)
                .Select(r => new SearchResultDto("ResourceRequest", r.RequestId,
                    r.Category!.CategoryName + " request #" + r.RequestId, r.Status))
                .ToListAsync());

            results.AddRange(await (await projects.VisibleAsync(userId))
                .Where(p => p.ProjectName.Contains(q) || (p.Description != null && p.Description.Contains(q)))
                .OrderBy(p => p.ProjectName).Take(PerKind)
                .Select(p => new SearchResultDto("Project", p.ProjectId, p.ProjectName, p.Status))
                .ToListAsync());

            // A meeting matches on its title or on any version of its minutes.
            results.AddRange(await meetings.VisibleMeetings(userId, await guard.CanSeeAllMeetingsAsync(userId))
                .Where(m => m.Title.Contains(q)
                         || db.MeetingMinutes.Any(x => x.MeetingId == m.MeetingId && x.MinutesText.Contains(q)))
                .OrderByDescending(m => m.MeetingDate).Take(PerKind)
                .Select(m => new SearchResultDto("Meeting", m.MeetingId, m.Title, null))
                .ToListAsync());

            // Events are public within the organisation (EventService.ListAsync is unfiltered).
            results.AddRange(await db.Events
                .Where(e => e.Title.Contains(q) || e.Location.Contains(q))
                .OrderByDescending(e => e.EventDate).Take(PerKind)
                .Select(e => new SearchResultDto("Event", e.EventId, e.Title, e.Location))
                .ToListAsync());

            results.AddRange(await guard.VisibleFiles(db.FileUploads.Where(f => !f.IsArchived), userId,
                    await guard.HasPermissionAsync(userId, Permissions.ManageFiles))
                .Where(f => f.FileName.Contains(q))
                .OrderBy(f => f.FileName).Take(PerKind)
                .Select(f => new SearchResultDto("File", f.FileId, f.FileName, f.Category!.CategoryName))
                .ToListAsync());

            return results;
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
