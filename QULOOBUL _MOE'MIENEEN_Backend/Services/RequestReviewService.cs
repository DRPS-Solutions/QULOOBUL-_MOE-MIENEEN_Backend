using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using QULOOBUL__MOE_MIENEEN_Shared.Requests;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// UC-05 / SD-03: submit a resource request and route it for review. Owns the
    /// ResourceRequest lifecycle guard (SM-02) and every authorisation decision on it.
    /// Each transition writes a Request_Status_History row, notifies the requester, records an
    /// audit row, and pushes a SignalR event — all in one Unit of Work.
    /// </summary>
    public class RequestReviewService(AppDbContext db, AuditService audit, NotificationService notify)
    {
        public const string ReviewPermission = "ReviewRequests";
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Reviewers see every request; everyone else sees only their own.</summary>
        public async Task<List<ResourceRequestDto>> ListAsync(int userId)
        {
            var q = db.ResourceRequests.AsQueryable();
            if (!await HasPermission(userId, ReviewPermission)) q = q.Where(r => r.RequestedBy == userId);

            return await q.OrderByDescending(r => r.DateSubmitted)
                .Select(r => new ResourceRequestDto(
                    r.RequestId, r.RequestedBy, r.CategoryId, r.Category!.CategoryName,
                    r.Status, r.Priority, r.DateSubmitted, r.ReviewedBy,
                    db.RequestStatusHistory.Where(h => h.RequestId == r.RequestId).OrderBy(h => h.TimeCreated)
                        .Select(h => new RequestStatusHistoryDto(h.NewStatus, h.ChangedBy, h.TimeCreated)).ToList(),
                    r.Detail))
                .ToListAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        public Task<List<RequestCategoryDto>> CategoriesAsync() =>
            db.RequestCategories.OrderBy(c => c.CategoryName)
                .Select(c => new RequestCategoryDto(c.CategoryId, c.CategoryName)).ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<ResourceRequest> SubmitAsync(int requestedBy, int categoryId, string priority, string? detail)
        {
            if (!await db.RequestCategories.AnyAsync(c => c.CategoryId == categoryId))
                throw new InvalidOperationException($"Unknown request category {categoryId}.");

            var request = new ResourceRequest
            {
                RequestedBy = requestedBy,
                CategoryId = categoryId,
                Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority,
                Status = Lifecycle.RequestStart,
                DateSubmitted = DateTime.UtcNow,
                Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
            };
            db.ResourceRequests.Add(request);
            db.RequestStatusHistory.Add(new RequestStatusHistory
            {
                Request = request,
                NewStatus = Lifecycle.RequestStart,
                ChangedBy = requestedBy,
                TimeCreated = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(); // the notifications below point at the new request's id

            // UC-05: route the request to everyone who can review it.
            var requester = await db.Users.Where(u => u.UserId == requestedBy)
                .Select(u => u.FirstName + " " + u.Surname).FirstAsync();
            var summary = request.Detail?.Split('\n', 2)[0];
            var message = $"{requester} submitted resource request #{request.RequestId}" +
                          (summary is null ? "." : $": {summary}");
            var reviewers = await UsersWith(ReviewPermission).Where(id => id != requestedBy).ToListAsync();
            foreach (var reviewer in reviewers)
                await notify.QueueAsync(reviewer, "RequestSubmitted", message,
                    nameof(ResourceRequest), request.RequestId);
            audit.Record(requestedBy, "request.submit", nameof(ResourceRequest), request.RequestId);

            await db.SaveChangesAsync();
            foreach (var reviewer in reviewers)
                await notify.PushAsync(reviewer, "requestSubmitted", new { requestId = request.RequestId });
            return request;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Approve or decline a pending request. Reviewer needs the ReviewRequests permission
        /// and may not review their own request.</summary>
        public async Task<ResourceRequest> ReviewAsync(int requestId, int reviewerId, bool approve, string? note = null)
        {
            var request = await db.ResourceRequests.FindAsync(requestId)
                ?? throw new KeyNotFoundException($"Request {requestId} not found.");

            if (request.RequestedBy == reviewerId)
                throw new InvalidOperationException("You cannot review your own request.");
            if (!await HasPermission(reviewerId, ReviewPermission))
                throw new UnauthorizedAccessException("You do not have permission to review requests.");

            var target = approve ? "Approved" : "Declined";
            Lifecycle.EnsureRequestMove(request.Status, target);

            request.Status = target;
            request.ReviewedBy = reviewerId;
            AddHistory(requestId, target, reviewerId);

            var message = $"Your resource request #{requestId} was {target.ToLowerInvariant()}." +
                          (string.IsNullOrWhiteSpace(note) ? "" : $" Note: {note}");
            await notify.QueueAsync(request.RequestedBy, "RequestReviewed", message,
                nameof(ResourceRequest), requestId);
            audit.Record(reviewerId, approve ? "request.approve" : "request.decline",
                nameof(ResourceRequest), requestId);

            await db.SaveChangesAsync();
            await notify.PushAsync(request.RequestedBy, "requestReviewed", new { requestId, status = target });
            return request;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Mark an approved request fulfilled.</summary>
        public async Task<ResourceRequest> CompleteAsync(int requestId, int actorId)
        {
            var request = await db.ResourceRequests.FindAsync(requestId)
                ?? throw new KeyNotFoundException($"Request {requestId} not found.");
            if (!await HasPermission(actorId, ReviewPermission))
                throw new UnauthorizedAccessException("You do not have permission to close requests.");

            Lifecycle.EnsureRequestMove(request.Status, "Completed");
            request.Status = "Completed";
            AddHistory(requestId, "Completed", actorId);
            await notify.QueueAsync(request.RequestedBy, "RequestCompleted",
                $"Your resource request #{requestId} has been fulfilled.", nameof(ResourceRequest), requestId);
            audit.Record(actorId, "request.complete", nameof(ResourceRequest), requestId);

            await db.SaveChangesAsync();
            await notify.PushAsync(request.RequestedBy, "requestReviewed", new { requestId, status = "Completed" });
            return request;
        }
//----------------------------------------------------------------------------------------------------------------------//
        private void AddHistory(int requestId, string newStatus, int changedBy) =>
            db.RequestStatusHistory.Add(new RequestStatusHistory
            {
                RequestId = requestId,
                NewStatus = newStatus,
                ChangedBy = changedBy,
                TimeCreated = DateTime.UtcNow,
            });
//----------------------------------------------------------------------------------------------------------------------//
        private Task<bool> HasPermission(int userId, string permissionName) =>
            UsersWith(permissionName).AnyAsync(id => id == userId);
//----------------------------------------------------------------------------------------------------------------------//
        private IQueryable<int> UsersWith(string permissionName) =>
            from u in db.Users
            join rp in db.RolePermissions on u.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.PermissionId
            where u.IsActive && p.PermissionName == permissionName
            select u.UserId;
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//