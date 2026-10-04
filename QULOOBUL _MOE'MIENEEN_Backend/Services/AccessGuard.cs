using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// Object-level authorisation (NFR "Object-level authorisation (anti-IDOR)"): the single place
    /// that answers "may this user see this record". Every read that takes an id straight off the
    /// URL routes through here, so altering an id in the address bar cannot reach someone else's data.
    ///
    /// The rule: a user holding ManageUsers sees everything; otherwise a project is visible to its
    /// owner and its members, and a task is visible to anyone who can see its project plus its
    /// creator and its assignees (ProjectMember: "a member allocated to a project sees only that project").
    /// Reference: OWASP Foundation (2025) OWASP Top 10:2025. Available at: https://owasp.org/Top10/2025/ (Accessed: 8 August 2026).
    /// Reference: Microsoft (2026) Resource-based authorization in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased (Accessed: 4 October 2026).
    /// </summary>
    public class AccessGuard(AppDbContext db)
    {
        /// <summary>ManageUsers is the admin marker the seeded Admin role carries.</summary>
        public Task<bool> IsAdminAsync(int userId) => HasPermissionAsync(userId, Permissions.ManageUsers);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Read from the database, not the JWT, so a revoked grant takes effect immediately.</summary>
        public Task<bool> HasPermissionAsync(int userId, string permission) =>
            (from u in db.Users
             join rp in db.RolePermissions on u.RoleId equals rp.RoleId
             join p in db.Permissions on rp.PermissionId equals p.PermissionId
             where u.UserId == userId && u.IsActive && p.PermissionName == permission
             select u.UserId).AnyAsync();
//----------------------------------------------------------------------------------------------------------------------//
        public async Task EnsurePermissionAsync(int userId, string permission)
        {
            if (!await HasPermissionAsync(userId, permission))
                throw new UnauthorizedAccessException($"You need the {permission} permission to do that.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<bool> CanSeeProjectAsync(int userId, int projectId) =>
            await db.Projects.AnyAsync(p => p.ProjectId == projectId
                && (p.OwnerId == userId || p.ProjectName == DailyTasksProject)) // the seeded shared project
            || await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId)
            || await IsAdminAsync(userId);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>404 when the project does not exist, 403 when it does but is not the caller's.</summary>
        public async Task EnsureProjectAsync(int userId, int projectId)
        {
            if (!await db.Projects.AnyAsync(p => p.ProjectId == projectId))
                throw new KeyNotFoundException($"Project {projectId} not found.");
            if (!await CanSeeProjectAsync(userId, projectId))
                throw new UnauthorizedAccessException("You do not have access to this project.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Least privilege: changing a project or its roster is the owner's or an admin's.</summary>
        public async Task EnsureProjectOwnerAsync(int userId, int projectId)
        {
            var ownerId = await db.Projects.Where(p => p.ProjectId == projectId)
                .Select(p => (int?)p.OwnerId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Project {projectId} not found.");

            if (ownerId != userId && !await IsAdminAsync(userId))
                throw new UnauthorizedAccessException("Only the project owner or an admin may change this project.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task EnsureTaskAsync(int userId, int taskId)
        {
            var task = await db.Tasks.FindAsync(taskId)
                ?? throw new KeyNotFoundException($"Task {taskId} not found.");

            if (task.CreatedBy == userId) return;
            if (await db.TaskAssignments.AnyAsync(a => a.TaskId == taskId && a.UserId == userId)) return;
            if (await CanSeeProjectAsync(userId, task.ProjectId)) return;

            throw new UnauthorizedAccessException("You do not have access to this task.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Who reads every meeting's minutes (Admin, Secondary Admin). Reading only: recording,
        /// editing and approving keep the organiser/attendee/admin rules below.</summary>
        public async Task<bool> CanSeeAllMeetingsAsync(int userId) =>
            await HasPermissionAsync(userId, Permissions.ViewAllMeetings) || await IsAdminAsync(userId);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>A meeting is its organiser's, its attendees' and an admin's.</summary>
        public async Task EnsureMeetingAsync(int userId, int meetingId)
        {
            var organiserId = await db.Meetings.Where(m => m.MeetingId == meetingId)
                .Select(m => (int?)m.OrganiserId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Meeting {meetingId} not found.");

            if (organiserId == userId) return;
            if (await db.MeetingAttendees.AnyAsync(a => a.MeetingId == meetingId && a.UserId == userId)) return;
            if (await IsAdminAsync(userId)) return;

            throw new UnauthorizedAccessException("You do not have access to this meeting.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Events are public within the organisation, but only the organiser or an admin
        /// changes who is on one.</summary>
        public async Task EnsureEventOrganiserAsync(int userId, int eventId)
        {
            var organiserId = await db.Events.Where(e => e.EventId == eventId)
                .Select(e => (int?)e.OrganiserId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");

            if (organiserId != userId && !await IsAdminAsync(userId))
                throw new UnauthorizedAccessException("Only the event organiser or an admin may change its attendees.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>A file inherits the access rule of whatever it is attached to; an unattached
        /// file (a monthly report filed against a period) is the uploader's. ManageFiles (Admin and
        /// Secondary Admin) reaches every file, attached or not.</summary>
        public async Task EnsureFileAsync(int userId, int fileId)
        {
            var file = await db.FileUploads.FindAsync(fileId)
                ?? throw new KeyNotFoundException($"File {fileId} not found.");

            if (file.UploadedBy == userId) return;
            if (await HasPermissionAsync(userId, Permissions.ManageFiles)) return;
            if (file.TaskId is int taskId) { await EnsureTaskAsync(userId, taskId); return; }
            if (file.MeetingId is int meetingId) { await EnsureMeetingAsync(userId, meetingId); return; }

            throw new UnauthorizedAccessException("You do not have access to this file.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// The same rule as <see cref="EnsureTaskAsync"/> expressed as a predicate EF can translate,
        /// for list endpoints. The seeded "Daily Tasks" is a shared checklist, so everyone sees all
        /// of it and can tick off work that wasn't assigned to them.
        /// </summary>
        public IQueryable<ProjectTask> VisibleTasks(IQueryable<ProjectTask> tasks, int userId, bool isAdmin) =>
            isAdmin ? tasks : tasks.Where(t =>
                t.CreatedBy == userId
                || db.Projects.Any(p => p.ProjectId == t.ProjectId && p.ProjectName == DailyTasksProject)
                || db.Projects.Any(p => p.ProjectId == t.ProjectId && p.OwnerId == userId)
                || db.ProjectMembers.Any(m => m.ProjectId == t.ProjectId && m.UserId == userId)
                || db.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.UserId == userId));
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// The library listing counterpart of <see cref="EnsureFileAsync"/>: a file is visible when its
        /// task or its meeting is, and an unattached one only to its uploader.
        /// </summary>
        public IQueryable<FileUpload> VisibleFiles(IQueryable<FileUpload> files, int userId, bool isAdmin) =>
            isAdmin ? files : files.Where(f =>
                f.UploadedBy == userId
                || db.Tasks.Any(t => t.TaskId == f.TaskId
                    && (t.CreatedBy == userId
                        || db.Projects.Any(p => p.ProjectId == t.ProjectId && p.OwnerId == userId)
                        || db.ProjectMembers.Any(m => m.ProjectId == t.ProjectId && m.UserId == userId)
                        || db.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.UserId == userId)))
                || db.Meetings.Any(m => m.MeetingId == f.MeetingId
                    && (m.OrganiserId == userId
                        || db.MeetingAttendees.Any(a => a.MeetingId == m.MeetingId && a.UserId == userId))));
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
