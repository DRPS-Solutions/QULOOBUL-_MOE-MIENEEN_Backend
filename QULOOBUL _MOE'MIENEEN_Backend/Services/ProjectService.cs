using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using Microsoft.EntityFrameworkCore;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public class ProjectService(AppDbContext db, AuditService audit)
    {
        public Task<List<ProjectSummaryDto>> ListAsync() =>
            db.Projects.Select(p => new ProjectSummaryDto(
                p.ProjectId, p.ProjectName, p.Status,
                db.ProjectMembers.Count(m => m.ProjectId == p.ProjectId),
                db.Tasks.Count(t => t.ProjectId == p.ProjectId && t.Status != "Done"))).ToListAsync();

        public async Task<ProjectDto> CreateAsync(CreateProjectRequest req, int ownerId)
        {
            var p = new Project { OwnerId = ownerId, ProjectName = req.ProjectName, Status = req.Status, Description = req.Description };
            db.Projects.Add(p);
            audit.Record(ownerId, "project.create", nameof(Project), 0);
            await db.SaveChangesAsync();
            return new ProjectDto(p.ProjectId, p.OwnerId, p.ProjectName, p.Status, p.Description);
        }

        public async Task<ProjectDto> UpdateAsync(int id, UpdateProjectRequest req, int actorId)
        {
            var p = await db.Projects.FindAsync(id) ?? throw new KeyNotFoundException($"Project {id} not found.");
            if (req.ProjectName is { } n) p.ProjectName = n;
            if (req.Status is { } s) p.Status = s;
            if (req.Description is { } d) p.Description = d;
            audit.Record(actorId, "project.update", nameof(Project), id);
            await db.SaveChangesAsync();
            return new ProjectDto(p.ProjectId, p.OwnerId, p.ProjectName, p.Status, p.Description);
        }

        public async Task AddMemberAsync(int projectId, int userId, int actorId)
        {
            if (await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId))
                throw new InvalidOperationException("Already a member.");
            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, DateAdded = DateTime.UtcNow });
            audit.Record(actorId, "project.member.add", nameof(ProjectMember), projectId);
            await db.SaveChangesAsync();
        }
    }

}
