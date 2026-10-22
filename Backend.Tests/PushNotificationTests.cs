using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Backend.Tests
{
    // Mobile push (Azure Notification Hubs): which notifications reach the phone, and what the phone receives
    // The hub itself is never called here: with no connection string PushHubService is a no-op, as in CI
    public class PushNotificationTests
    {
        // Every service raises notifications through NotificationService; the push has to fillow the commit
        [Fact]
        public async Task A_saved_notification_is_handed_to_the_push_queue_once()
        {
            using var db = NewDb();
            var user = await AddUserAsync(db);
            var queue = new PushQueue();
            var notify = new NotificationService(db, null!, queue);

            await notify.QueueAsync(user.UserId, "TaskAssigned", "You have been assigned to task \"Sweep\".", "ProjectTask", 7);
            Assert.False(queue.TryDequeue(out _));

            await db.SaveChangesAsync();
            Assert.True(queue.TryDequeue(out var job));
            Assert.Equal(user.UserId, job!.UserId);
            Assert.Equal("TaskAssigned", job.TypeName);
            Assert.Equal(7, job.EntityId);
            Assert.True(job.NotificationId > 0);

            await db.SaveChangesAsync();
            Assert.False(queue.TryDequeue(out _));
        }

        //----------------------------------------------------------------------------------------------------------------------//
        // UC-08 preferences: a type the user switched off is neither stored nor pushed
        [Fact]
        public async Task A_muted_notification_type_is_not_pushed()
        {
            using var db = NewDb();
            var user = await AddUserAsync(db);
            db.NotificationPreferences.Add(new NotificationPreference
            {
                UserId = user.UserId,
                Type = new NotificationType { TypeName = "TaskAssigned" },
                IsEnabled = false,
            });
            await db.SaveChangesAsync();
            var queue = new PushQueue();
            var notify = new NotificationService(db, null!, queue);

            var muted = await notify.QueueAsync(user.UserId, "TaskAssigned", "muted", "ProjectTask", 1);
            await db.SaveChangesAsync();

            Assert.True(muted.IsMuted);
            Assert.False(queue.TryDequeue(out _));
        }

        //----------------------------------------------------------------------------------------------------------------------//
        // The app's channel id and the deep-link data must match what Frontend_Mobile reads, or a tap opens nothing
        [Fact]
        public void The_push_payload_uses_the_app_channel_and_carries_the_deep_link()
        {
            var json = PushHubService.BuildPayload(new PushJob(42, 5, "TaskAssigned", "You have a task", "ProjectTask", 7));

            var message = JsonDocument.Parse(json).RootElement.GetProperty("message");
            Assert.Equal("New task assigned", message.GetProperty("notification").GetProperty("title").GetString());
            Assert.Equal("You have a task", message.GetProperty("notification").GetProperty("body").GetString());
            Assert.Equal(PushHubService.ChannelId,
                message.GetProperty("android").GetProperty("notification").GetProperty("channel_id").GetString());
            
            var data = message.GetProperty("data");
            Assert.Equal("42", data.GetProperty("notificationId").GetString());
            Assert.Equal("ProjectTask", data.GetProperty("entityType").GetString());
            Assert.Equal("7", data.GetProperty("entityId").GetString());
        }

        //----------------------------------------------------------------------------------------------------------------------//
        [Fact]
        public void Each_phone_is_tagged_with_its_user_and_unknown_types_get_the_app_name()
        {
            Assert.Equal("user:5", PushHubService.TagFor(5));
            Assert.Equal("Quloobul Moe'mieneen", PushHubService.TitleFor("SomethingNew"));
        }

        //----------------------------------------------------------------------------------------------------------------------//
        // Local development and CI have no hub: registering and sending must quietly do nothing
        [Fact]
        public async Task Without_a_hub_configured_push_is_off_and_nothing_throws()
        {
            var hub = new PushHubService(new ConfigurationBuilder().Build(), NullLogger<PushHubService>.Instance);

            Assert.False(hub.IsConfigured);
            await hub.RegisterAsync(1, new PushInstallationRequest { InstallationId = "abc", PushChannel = "token" });
            await hub.RemoveAsync(1, "abc");
            await hub.SendAsync(new PushJob(1, 1, "TaskAssigned", "m", null, null));
        }

        //----------------------------------------------------------------------------------------------------------------------//
        [Fact]
        public async Task Only_android_phones_can_register()
        {
            var hub = new PushHubService(new ConfigurationBuilder().Build(), NullLogger<PushHubService>.Instance);

            await Assert.ThrowsAsync<ArgumentException>(() => hub.RegisterAsync(1,
                new PushInstallationRequest { InstallationId = "abc", PushChannel = "token", Platform = "ios" }));
        }

        //----------------------------------------------------------------------------------------------------------------------//
        private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static async Task<User> AddUserAsync(AppDbContext db)
        {
            var user = new User { Role = new Role { RoleName = "Staff" }, Email = "alice@test" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
