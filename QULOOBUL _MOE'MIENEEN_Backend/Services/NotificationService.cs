using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Hubs;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// The one place that turns a domain event into a stored Notification row and a live
    /// SignalR push (architecture doc, "Observer"). Callers Queue during their Unit of Work,
    /// SaveChanges, then Push once the commit has succeeded.
    /// </summary>
    public class NotificationService(AppDbContext db, IHubContext<NotificationHub> hub)
    {
        /// <summary>Add a notification row to the current Unit of Work. Not yet saved.</summary>
        public async Task<Notification> QueueAsync(
            int userId, string typeName, string message, string entityType, int entityId)
        {
            // Check unsaved types first: queueing several notifications of a brand-new type in one
            // Unit of Work must not insert that type more than once.
            var type = db.NotificationTypes.Local.FirstOrDefault(t => t.TypeName == typeName)
                       ?? await db.NotificationTypes.FirstOrDefaultAsync(t => t.TypeName == typeName);
            if (type is null)
            {
                type = new NotificationType { TypeName = typeName };
                db.NotificationTypes.Add(type);
            }
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Message = message,
                EntityType = entityType,
                EntityId = entityId,
                IsRead = false,
            };
            db.Notifications.Add(notification);
            return notification;
        }

        /// <summary>Push a live event to every device the user has connected. Call after SaveChanges.</summary>
        public Task PushAsync(int userId, string eventName, object payload) =>
            hub.Clients.Group(NotificationHub.GroupFor(userId)).SendAsync(eventName, payload);

        /// <summary>Push to every connected client, for changes everyone's view shows (UC-02 board).
        /// ponytail: goes to all users; narrow to project members once project-scoped visibility exists.</summary>
        public Task BroadcastAsync(string eventName, object payload) =>
            hub.Clients.All.SendAsync(eventName, payload);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
