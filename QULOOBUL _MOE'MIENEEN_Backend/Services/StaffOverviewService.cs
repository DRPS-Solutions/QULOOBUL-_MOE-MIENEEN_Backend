using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Project;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;

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
                db.TaskAssignments.Count(a => a.UserId == u.UserId && a.Task!.Status != "Done"),
                db.TaskAssignments.Count(a => a.UserId == u.UserId && a.Task!.Status != "Done" && a.Task.Deadline < DateTime.UtcNow),
                db.ResourceRequests.Count(r => r.RequestedBy == u.UserId && r.Status == "Pending")))
                .ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        public Task<List<UserSummaryDto>> StaffAsync() =>
            db.Users.Select(u => new UserSummaryDto(
                u.UserId, u.FirstName + " " + u.Surname, u.Email, u.Role!.RoleName, u.IsActive)).ToListAsync();
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//