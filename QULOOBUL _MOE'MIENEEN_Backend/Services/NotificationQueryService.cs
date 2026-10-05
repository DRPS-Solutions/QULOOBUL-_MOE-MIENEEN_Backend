using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
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
//----------------------------------------------------------------------------------------------------------------------//
        public async Task MarkReadAsync(int userId, IReadOnlyList<int> ids)
        {
            var rows = await db.Notifications
                .Where(n => n.UserId == userId && ids.Contains(n.NotificationId)).ToListAsync();
            foreach (var n in rows) n.IsRead = true;
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-08 preferences: every known notification type with the caller's on/off setting.</summary>
        public Task<List<NotificationPreferenceDto>> PreferencesAsync(int userId) =>
            db.NotificationTypes.OrderBy(t => t.TypeName)
                .Select(t => new NotificationPreferenceDto(t.TypeName,
                    !db.NotificationPreferences.Any(p => p.UserId == userId && p.TypeId == t.TypeId && !p.IsEnabled)))
                .ToListAsync();
//----------------------------------------------------------------------------------------------------------------------//
        public async Task SetPreferencesAsync(int userId, IReadOnlyList<NotificationPreferenceDto> prefs)
        {
            var names = prefs.Select(p => p.Type).Distinct().ToList();
            var types = await db.NotificationTypes.Where(t => names.Contains(t.TypeName)).ToListAsync();
            if (names.Except(types.Select(t => t.TypeName)).FirstOrDefault() is { } unknown)
                throw new ArgumentException($"Unknown notification type \"{unknown}\".");

            var typeIds = types.Select(t => t.TypeId).ToList();
            var existing = await db.NotificationPreferences
                .Where(p => p.UserId == userId && typeIds.Contains(p.TypeId)).ToListAsync();

            foreach (var pref in prefs)
            {
                var typeId = types.First(t => t.TypeName == pref.Type).TypeId;
                var row = existing.FirstOrDefault(p => p.TypeId == typeId);
                if (row is null)
                {
                    row = new NotificationPreference { UserId = userId, TypeId = typeId };
                    db.NotificationPreferences.Add(row);
                    existing.Add(row);
                }
                row.IsEnabled = pref.Enabled;
            }
            audit.Record(userId, "notification.preferences", nameof(NotificationPreference), userId);
            await db.SaveChangesAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>UC-08 alternate flow — admin broadcast.</summary>
        public async Task SendAsync(SendNotificationRequest req, int senderId)
        {
            var ids = req.UserIds.Distinct().ToList();
            if (await db.Users.CountAsync(u => ids.Contains(u.UserId) && u.IsActive) != ids.Count)
                throw new ArgumentException("Every recipient must be an active user.");
            foreach (var uid in req.UserIds.Distinct())
                await notify.QueueAsync(uid, req.Type, req.Message, "announcement", 0, ignorePreferences: true);
            audit.Record(senderId, "notification.broadcast", "Notification", 0);
            await db.SaveChangesAsync();
            foreach (var uid in req.UserIds.Distinct())
                await notify.PushAsync(uid, "notification", new { req.Message, req.Type });
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//