using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using Microsoft.EntityFrameworkCore;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public class ProjectService(
        AppDbContext db, AuditService audit, NotificationService notify, AccessGuard guard, TaskAssignmentService tasks)
    {
        /// <summary>Only the projects the caller belongs to (ProjectMember: "sees only that project").</summary>
        public async Task<List<ProjectSummaryDto>> ListAsync(int userId) =>
            await (await VisibleAsync(userId)).OrderBy(p => p.ProjectName).Select(p => new ProjectSummaryDto(
                p.ProjectId, p.ProjectName, p.Status,
                db.ProjectMembers.Count(m => m.ProjectId == p.ProjectId),
                db.Tasks.Count(t => t.ProjectId == p.ProjectId && !Lifecycle.TaskFinished.Contains(t.Status)))).ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<IQueryable<Project>> VisibleAsync(int userId) =>
            await guard.IsAdminAsync(userId) ? db.Projects : db.Projects.Where(p =>
                p.OwnerId == userId || p.ProjectName == DailyTasksProject
                || db.ProjectMembers.Any(m => m.ProjectId == p.ProjectId && m.UserId == userId));
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<ProjectDto> GetAsync(int id, int userId)
        {
            await guard.EnsureProjectAsync(userId, id);
            return await db.Projects.Where(p => p.ProjectId == id)
                .Select(p => new ProjectDto(p.ProjectId, p.OwnerId, p.ProjectName, p.Status, p.Description))
                .FirstAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<List<ProjectMemberDto>> MembersAsync(int projectId, int userId)
        {
            await guard.EnsureProjectAsync(userId, projectId);
            return await db.ProjectMembers.Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.User!.FirstName).ThenBy(m => m.User!.Surname)
                .Select(m => new ProjectMemberDto(m.UserId, m.User!.FirstName + " " + m.User.Surname, m.DateAdded))
                .ToListAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<ProjectDto> CreateAsync(CreateProjectRequest req, int actorId)
        {
            var ownerId = req.OwnerId ?? actorId;
            await EnsureActiveUser(ownerId);
            var p = new Project { OwnerId = ownerId, ProjectName = req.ProjectName, Status = req.Status, Description = req.Description };
            db.Projects.Add(p);
            await db.SaveChangesAsync();                 // assigns p.ProjectId
            await audit.RecordAndSaveAsync(actorId, "project.create", nameof(Project), p.ProjectId);
            return new ProjectDto(p.ProjectId, p.OwnerId, p.ProjectName, p.Status, p.Description);
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<ProjectDto> UpdateAsync(int id, UpdateProjectRequest req, int actorId)
        {
            await guard.EnsureProjectOwnerAsync(actorId, id);
            var p = await db.Projects.FindAsync(id) ?? throw new KeyNotFoundException($"Project {id} not found.");
            if (req.ProjectName is { } n) p.ProjectName = n;
            if (req.Status is { } s)
            {
                // SM-01 archive() [parent project is closed]: fires on the move into Closed, in this UoW.
                if (s.Equals(Lifecycle.ProjectClosed, StringComparison.OrdinalIgnoreCase)
                    && !p.Status.Equals(Lifecycle.ProjectClosed, StringComparison.OrdinalIgnoreCase))
                    await tasks.ArchiveDoneTasksAsync(id, actorId);
                p.Status = s;
            }
            if (req.Description is { } d) p.Description = d;
            if (req.OwnerId is { } o)
            {
                await EnsureActiveUser(o);
                p.OwnerId = o;
            }
            audit.Record(actorId, "project.update", nameof(Project), id);
            await db.SaveChangesAsync();
            return new ProjectDto(p.ProjectId, p.OwnerId, p.ProjectName, p.Status, p.Description);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Event Cancellation: owner or admin. Kept, not deleted, so its history and files survive;
        /// open tasks are cancelled and the staff on it are told why.</summary>
        public async Task<ProjectDto> CancelAsync(int id, string? reason, int actorId)
        {
            await guard.EnsureProjectOwnerAsync(actorId, id);
            var p = await db.Projects.FindAsync(id) ?? throw new KeyNotFoundException($"Project {id} not found.");
            if (p.Status.Equals(Lifecycle.ProjectCancelled, StringComparison.OrdinalIgnoreCase)
                || p.Status.Equals(Lifecycle.ProjectClosed, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"This event is already {p.Status.ToLowerInvariant()}.");

            p.Status = Lifecycle.ProjectCancelled;
            await tasks.CancelOpenTasksAsync(id, actorId);

            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            var message = $"\"{p.ProjectName}\" has been cancelled." + (reason is null ? "" : $" Reason: {reason}");
            // "Staff on it" = Staff Helping members plus anyone assigned a task on the event, who may not be a member.
            var members = await db.ProjectMembers.Where(m => m.ProjectId == id).Select(m => m.UserId)
                .Union(db.TaskAssignments.Where(a => a.Task!.ProjectId == id).Select(a => a.UserId))
                .Where(uid => uid != actorId).ToListAsync();
            foreach (var uid in members)
                await notify.QueueAsync(uid, "ProjectCancelled", message, nameof(Project), id);
            audit.Record(actorId, "project.cancel", nameof(Project), id);
            await db.SaveChangesAsync();

            foreach (var uid in members) await notify.PushAsync(uid, "notification", new { projectId = id });
            await notify.BroadcastAsync("boardChanged", new { projectId = id });
            return new ProjectDto(p.ProjectId, p.OwnerId, p.ProjectName, p.Status, p.Description);
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task AddMemberAsync(int projectId, int userId, int actorId)
        {
            await guard.EnsureProjectOwnerAsync(actorId, projectId);
            var projectName = await db.Projects.Where(p => p.ProjectId == projectId)
                .Select(p => p.ProjectName).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Project {projectId} not found.");
            await EnsureActiveUser(userId);
            if (await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId))
                throw new InvalidOperationException("Already a member.");

            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, DateAdded = DateTime.UtcNow });
            await notify.QueueAsync(userId, "ProjectAssigned",
                $"You were added to the staff for \"{projectName}\".", nameof(Project), projectId);
            audit.Record(actorId, "project.member.add", nameof(ProjectMember), projectId);
            await db.SaveChangesAsync();
            await notify.PushAsync(userId, "projectAssigned", new { projectId });
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task RemoveMemberAsync(int projectId, int userId, int actorId)
        {
            await guard.EnsureProjectOwnerAsync(actorId, projectId);
            var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId)
                ?? throw new KeyNotFoundException("That user is not on this project.");
            db.ProjectMembers.Remove(member);
            audit.Record(actorId, "project.member.remove", nameof(ProjectMember), projectId);
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        private async Task EnsureActiveUser(int userId)
        {
            if (!await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive))
                throw new InvalidOperationException($"User {userId} does not exist or is deactivated.");
        }
    }

}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//