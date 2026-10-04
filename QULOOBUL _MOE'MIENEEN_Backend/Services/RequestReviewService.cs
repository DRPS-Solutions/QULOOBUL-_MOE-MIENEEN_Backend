using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using QULOOBUL__MOE_MIENEEN_Shared.Requests;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// UC-05 / SD-03: submit a resource request and route it for review. Owns the
    /// ResourceRequest lifecycle guard (SM-02) and every authorisation decision on it.
    /// Each transition writes a Request_Status_History row, notifies the requester, records an
    /// audit row, and pushes a SignalR event — all in one Unit of Work.
    /// </summary>
    public class RequestReviewService(
        AppDbContext db, AuditService audit, NotificationService notify, EmailService email)
    {
        public const string ReviewPermission = "ReviewRequests";
        public static readonly TimeSpan ResubmitWindow = TimeSpan.FromDays(30);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Reviewers and facility managers see every request (managers read-only, so nobody
        /// orders the same thing twice - test plan R6; acting on one is still decided in ReviewAsync and
        /// friends). Everyone else sees only their own.</summary>
        public async Task<IQueryable<ResourceRequest>> VisibleAsync(int userId)
        {
            var q = db.ResourceRequests.AsQueryable();
            if (await HasPermission(userId, ReviewPermission) || await IsFacilityManager(userId)) return q;
            return q.Where(r => r.RequestedBy == userId);
        }
//----------------------------------------------------------------------------------------------------------------------//
        public async Task<List<ResourceRequestDto>> ListAsync(int userId) =>
            await (await VisibleAsync(userId)).OrderByDescending(r => r.DateSubmitted)
                .Select(r => new ResourceRequestDto(
                    r.RequestId, r.RequestedBy, r.CategoryId, r.Category!.CategoryName,
                    r.Status, r.Priority, r.DateSubmitted, r.ReviewedBy,
                    db.RequestStatusHistory.Where(h => h.RequestId == r.RequestId).OrderBy(h => h.TimeCreated)
                        .Select(h => new RequestStatusHistoryDto(h.NewStatus, h.ChangedBy, h.TimeCreated, h.Note)).ToList(),
                    r.Detail))
                .ToListAsync();
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

            audit.Record(requestedBy, "request.submit", nameof(ResourceRequest), request.RequestId);
            await RouteToReviewersAsync(request, "submitted");
            return request;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-02 resubmit(requester) [within 30 days of the decision]: Declined → Pending.
        /// The request starts over, so the 48-hour escalation clock restarts with it.</summary>
        public async Task<ResourceRequest> ResubmitAsync(int requestId, int userId, string? detail)
        {
            var request = await db.ResourceRequests.FindAsync(requestId)
                ?? throw new KeyNotFoundException($"Request {requestId} not found.");
            if (request.RequestedBy != userId)
                throw new UnauthorizedAccessException("Only the requester can resubmit a request.");
            Lifecycle.EnsureRequestMove(request.Status, Lifecycle.RequestStart);

            var declinedAt = await db.RequestStatusHistory
                .Where(h => h.RequestId == requestId && h.NewStatus == Lifecycle.RequestDeclined)
                .MaxAsync(h => (DateTime?)h.TimeCreated);
            if (declinedAt is null || DateTime.UtcNow - declinedAt > ResubmitWindow)
                throw new InvalidOperationException("A declined request can only be resubmitted within 30 days of the decision.");

            request.Status = Lifecycle.RequestStart;
            request.ReviewedBy = null;
            request.DateSubmitted = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(detail)) request.Detail = detail.Trim();
            AddHistory(requestId, Lifecycle.RequestStart, userId);
            audit.Record(userId, "request.resubmit", nameof(ResourceRequest), requestId);
            await RouteToReviewersAsync(request, "resubmitted");
            return request;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>SM-02 beginReview: Pending → Under Review for a ReviewRequests holder, Escalated →
        /// Under Review for the facility manager it was routed to. The reviewer claims the request.</summary>
        public async Task<ResourceRequest> BeginReviewAsync(int requestId, int reviewerId)
        {
            var request = await db.ResourceRequests.FindAsync(requestId)
                ?? throw new KeyNotFoundException($"Request {requestId} not found.");
            await StartReview(request, reviewerId);
            await db.SaveChangesAsync();
            await notify.PushAsync(request.RequestedBy, "requestReviewed", new { requestId, status = request.Status });
            return request;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Approve or decline. A request still Pending or Escalated passes through Under Review
        /// first, so a one-click approve still leaves the full SM-02 trail in the history.</summary>
        public async Task<ResourceRequest> ReviewAsync(int requestId, int reviewerId, bool approve, string? note = null)
        {
            var request = await db.ResourceRequests.FindAsync(requestId)
                ?? throw new KeyNotFoundException($"Request {requestId} not found.");

            if (request.Status is Lifecycle.RequestStart or Lifecycle.RequestEscalated)
                await StartReview(request, reviewerId);
            else
                await EnsureCanReview(request, reviewerId);

            var target = approve ? "Approved" : Lifecycle.RequestDeclined;
            Lifecycle.EnsureRequestMove(request.Status, target);

            request.Status = target;
            request.ReviewedBy = reviewerId;
            AddHistory(requestId, target, reviewerId, string.IsNullOrWhiteSpace(note) ? null : note.Trim());

            var message = $"Your resource request #{requestId} was {target.ToLowerInvariant()}." +
                          (string.IsNullOrWhiteSpace(note) ? "" : $" Note: {note}");
            var notification = await notify.QueueAsync(request.RequestedBy, "RequestReviewed", message,
                nameof(ResourceRequest), requestId);
            audit.Record(reviewerId, approve ? "request.approve" : "request.decline",
                nameof(ResourceRequest), requestId);

            await db.SaveChangesAsync();
            await notify.PushAsync(request.RequestedBy, "requestReviewed", new { requestId, status = target });
            await email.SendAsync(notification, $"Your resource request was {target.ToLowerInvariant()}");
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
            var notification = await notify.QueueAsync(request.RequestedBy, "RequestCompleted",
                $"Your resource request #{requestId} has been fulfilled.", nameof(ResourceRequest), requestId);
            audit.Record(actorId, "request.complete", nameof(ResourceRequest), requestId);

            await db.SaveChangesAsync();
            await notify.PushAsync(request.RequestedBy, "requestReviewed", new { requestId, status = "Completed" });
            await email.SendAsync(notification, "Your resource request was fulfilled");
            return request;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-05: route a (re)submitted request to everyone who can review it, and commit.</summary>
        private async Task RouteToReviewersAsync(ResourceRequest request, string verb)
        {
            var requester = await db.Users.Where(u => u.UserId == request.RequestedBy)
                .Select(u => u.FirstName + " " + u.Surname).FirstAsync();
            var summary = request.Detail?.Split('\n', 2)[0];
            var message = $"{requester} {verb} resource request #{request.RequestId}" +
                          (summary is null ? "." : $": {summary}");
            var reviewers = await UsersWith(ReviewPermission).Where(id => id != request.RequestedBy).ToListAsync();
            foreach (var reviewer in reviewers)
                await notify.QueueAsync(reviewer, "RequestSubmitted", message,
                    nameof(ResourceRequest), request.RequestId);

            await db.SaveChangesAsync();
            foreach (var reviewer in reviewers)
                await notify.PushAsync(reviewer, "requestSubmitted", new { requestId = request.RequestId });
        }
//----------------------------------------------------------------------------------------------------------------------//
        private async Task StartReview(ResourceRequest request, int reviewerId)
        {
            await EnsureCanReview(request, reviewerId);
            Lifecycle.EnsureRequestMove(request.Status, Lifecycle.RequestUnderReview);
            if (request.Status == Lifecycle.RequestUnderReview) return;
            request.Status = Lifecycle.RequestUnderReview;
            request.ReviewedBy = reviewerId;
            AddHistory(request.RequestId, Lifecycle.RequestUnderReview, reviewerId);
            audit.Record(reviewerId, "request.review.begin", nameof(ResourceRequest), request.RequestId);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>ReviewRequests holders review anything; a facility manager reviews what was escalated
        /// to them and then sees it through. Nobody reviews their own request.</summary>
        private async Task EnsureCanReview(ResourceRequest request, int reviewerId)
        {
            if (request.RequestedBy == reviewerId)
                throw new InvalidOperationException("You cannot review your own request.");
            if (await HasPermission(reviewerId, ReviewPermission)) return;
            if (await IsFacilityManager(reviewerId)
                && (request.Status == Lifecycle.RequestEscalated
                    || (request.Status == Lifecycle.RequestUnderReview && request.ReviewedBy == reviewerId)))
                return;
            throw new UnauthorizedAccessException("You do not have permission to review this request.");
        }
//----------------------------------------------------------------------------------------------------------------------//
        private Task<bool> IsFacilityManager(int userId) =>
            db.Users.AnyAsync(u => u.UserId == userId && u.IsActive && u.Role!.RoleName == Roles.FacilityManager);
//----------------------------------------------------------------------------------------------------------------------//
        private void AddHistory(int requestId, string newStatus, int changedBy, string? note = null) =>
            db.RequestStatusHistory.Add(new RequestStatusHistory
            {
                RequestId = requestId,
                NewStatus = newStatus,
                ChangedBy = changedBy,
                Note = note,
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