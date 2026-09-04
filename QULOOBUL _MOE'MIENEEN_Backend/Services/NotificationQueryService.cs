using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public class NotificationQueryService(AppDbContext db, NotificationService notify, AuditService audit)
    {
        public Task<List<NotificationDto>> ForUserAsync(int userId, bool unreadOnly) =>
            db.Notifications.Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead))
                .OrderByDescending(n => n.NotificationId)
                .Select(n => new NotificationDto(
                    n.NotificationId, n.Type!.TypeName, n.Message, n.IsRead, n.EntityType, n.EntityId))
                .ToListAsync();

        public async Task MarkReadAsync(int userId, IReadOnlyList<int> ids)
        {
            var rows = await db.Notifications
                .Where(n => n.UserId == userId && ids.Contains(n.NotificationId)).ToListAsync();
            foreach (var n in rows) n.IsRead = true;
            await db.SaveChangesAsync();
        }

        /// <summary>UC-08 alternate flow — admin broadcast.</summary>
        public async Task SendAsync(SendNotificationRequest req, int senderId)
        {
            foreach (var uid in req.UserIds.Distinct())
                await notify.QueueAsync(uid, req.Type, req.Message, "announcement", 0);
            audit.Record(senderId, "notification.broadcast", "Notification", 0);
            await db.SaveChangesAsync();
            foreach (var uid in req.UserIds.Distinct())
                await notify.PushAsync(uid, "notification", new { req.Message, req.Type });
        }
    }
}