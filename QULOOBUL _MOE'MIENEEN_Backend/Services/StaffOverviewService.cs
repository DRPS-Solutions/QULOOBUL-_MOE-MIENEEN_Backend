using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>UC-06 admin dashboard - read-only aggregate over staff, tasks, requests.</summary>
    public class StaffOverviewService(AppDbContext db)
    {
        public Task<List<StaffOverviewRow>> RowsAsync() =>
            db.Users.Where(u => u.IsActive).Select(u => new StaffOverviewRow(
                u.UserId,
                u.FirstName + " " + u.Surname,
                u.Role!.RoleName,
                db.TaskAssignments.Count(a => a.UserId == u.UserId && !Lifecycle.TaskFinished.Contains(a.Task!.Status)),
                db.TaskAssignments.Count(a => a.UserId == u.UserId && !Lifecycle.TaskFinished.Contains(a.Task!.Status) && a.Task.Deadline < DateTime.UtcNow),
                db.ResourceRequests.Count(r => r.RequestedBy == u.UserId && r.Status == "Pending")))
                .ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>The seeded automation account is an implementation detail, not a colleague.</summary>
        public Task<List<UserSummaryDto>> StaffAsync() =>
            db.UserDirectory.Where(u => u.Email != SystemAccountEmail).Select(u => new UserSummaryDto(
                u.UserId, u.FullName, u.Email, u.RoleName, u.IsActive, u.Status)).ToListAsync();
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//