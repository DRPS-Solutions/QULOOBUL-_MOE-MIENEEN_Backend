using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using Microsoft.EntityFrameworkCore;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public class ProjectService(AppDbContext db, AuditService audit, NotificationService notify)
    {
        public Task<List<ProjectSummaryDto>> ListAsync() =>
            db.Projects.OrderBy(p => p.ProjectName).Select(p => new ProjectSummaryDto(
                p.ProjectId, p.ProjectName, p.Status,
                db.ProjectMembers.Count(m => m.ProjectId == p.ProjectId),
                db.Tasks.Count(t => t.ProjectId == p.ProjectId && t.Status != "Done"))).ToListAsync();

        public async Task<ProjectDto> GetAsync(int id) =>
            await db.Projects.Where(p => p.ProjectId == id)
                .Select(p => new ProjectDto(p.ProjectId, p.OwnerId, p.ProjectName, p.Status, p.Description))
                .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Project {id} not found.");

        public Task<List<ProjectMemberDto>> MembersAsync(int projectId) =>
            db.ProjectMembers.Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.User!.FirstName).ThenBy(m => m.User!.Surname)
                .Select(m => new ProjectMemberDto(m.UserId, m.User!.FirstName + " " + m.User.Surname, m.DateAdded))
                .ToListAsync();

        public async Task<ProjectDto> CreateAsync(CreateProjectRequest req, int actorId)
        {
            var ownerId = req.OwnerId ?? actorId;
            await EnsureActiveUser(ownerId);
            var p = new Project { OwnerId = ownerId, ProjectName = req.ProjectName, Status = req.Status, Description = req.Description };
            db.Projects.Add(p);
            audit.Record(actorId, "project.create", nameof(Project), 0);
            await db.SaveChangesAsync();
            return new ProjectDto(p.ProjectId, p.OwnerId, p.ProjectName, p.Status, p.Description);
        }

        public async Task<ProjectDto> UpdateAsync(int id, UpdateProjectRequest req, int actorId)
        {
            var p = await db.Projects.FindAsync(id) ?? throw new KeyNotFoundException($"Project {id} not found.");
            if (req.ProjectName is { } n) p.ProjectName = n;
            if (req.Status is { } s) p.Status = s;
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

        public async Task AddMemberAsync(int projectId, int userId, int actorId)
        {
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

        public async Task RemoveMemberAsync(int projectId, int userId, int actorId)
        {
            var member = await db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId)
                ?? throw new KeyNotFoundException("That user is not on this project.");
            db.ProjectMembers.Remove(member);
            audit.Record(actorId, "project.member.remove", nameof(ProjectMember), projectId);
            await db.SaveChangesAsync();
        }

        private async Task EnsureActiveUser(int userId)
        {
            if (!await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive))
                throw new InvalidOperationException($"User {userId} does not exist or is deactivated.");
        }
    }

}
