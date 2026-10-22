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
    /// Reference: E. Gamma, R. Helm, R. E. Johnson, and J. Vlissides, Design Patterns: Elements of Reusable Object-Oriented Software. Boston, MA, USA: Addison-Wesley, 2016.
    /// Reference: Microsoft, "Send messages from outside a hub." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/signalr/hubcontext
    /// </summary>
    public class NotificationService
    {
        private readonly AppDbContext db;
        private readonly IHubContext<NotificationHub> hub;
        private readonly PushQueue? pushes;

        // Queued this Unit of Work and not yet handed to the phone-push queue
        private readonly List<Notification> unsent = [];

        /// <summary>
        /// <paramref name="pushes"/> is optional so the tests that build this service by hand still compile;
        /// without it, no phone push goes out. Every saved notification becomes a phone push here, once
        /// SaveChanges has given it an id, so none of the services that raise notifications had to change.
        /// </summary>
        public NotificationService(AppDbContext db, IHubContext<NotificationHub> hub, PushQueue? pushes = null)
        {
            this.db = db;
            this.hub = hub;
            this.pushes = pushes;

            if (pushes is not null)
            {
                db.SavedChanges += (_, _) => HandOffSaved();
            }
        }

        private void HandOffSaved()
        {
            foreach (var notification in unsent.Where(notification => notification.NotificationId > 0).ToList())
            {
                pushes!.Enqueue(new PushJob(notification.NotificationId, notification.UserId, notification.Type?.TypeName ?? "",
                    notification.Message, notification.EntityType, notification.EntityId));
                unsent.Remove(notification);
            }
        }

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Add a notification row to the current Unit of Work. Not yet saved. If the user has switched
        /// this type off (UC-08 preferences) the returned notification is muted and never stored;
        /// <paramref name="ignorePreferences"/> is for an admin's direct message (UC-08 alternate flow).
        /// </summary>
        public async Task<Notification> QueueAsync(int userId, string typeName, string message, 
            string entityType, int entityId, bool ignorePreferences = false)
        {
            if (!ignorePreferences && await db.NotificationPreferences.AnyAsync(p =>
                    p.UserId == userId && p.Type!.TypeName == typeName && !p.IsEnabled))
                
                return new Notification
                {
                    UserId = userId,
                    Message = message,
                    EntityType = entityType,
                    EntityId = entityId,
                    IsMuted = true,
                };

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
            unsent.Add(notification); // muted notifications dont reach the phone
            return notification;
        }

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Push a live event to every device the user has connected. Call after SaveChanges.</summary>
        public Task PushAsync(int userId, string eventName, object payload) =>
            hub.Clients.Group(NotificationHub.GroupFor(userId)).SendAsync(eventName, payload);
        
        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Push to every connected client, for changes everyone's view shows (UC-02 board).
        /// ponytail: goes to all users; narrow to project members once project-scoped visibility exists.</summary>
        public Task BroadcastAsync(string eventName, object payload) =>
            hub.Clients.All.SendAsync(eventName, payload);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
