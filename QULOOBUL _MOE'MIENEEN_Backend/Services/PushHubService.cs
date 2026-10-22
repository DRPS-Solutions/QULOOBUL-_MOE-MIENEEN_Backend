using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.NotificationHubs.Messaging;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;
using System.Text.Json;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    public class PushHubService
    {
        /// <summary>
        /// The mobile app creates a new Android notification channel with the same Id
        /// </summary>
        public const string ChannelId = "qm-general";

        private readonly NotificationHubClient? hubClient;

        public PushHubService(IConfiguration config, ILogger<PushHubService> log)
        {
            var connectionString = config["NotificationHub:ConnectionString"];
            var hubName = config["NotificationHub:hubName"];

            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(hubName))
            {
                log.LogInformation("Notification Hub is not configured. Mobile Push Notifications are off (in-app and email still work).");
                return;
            }

            try
            {
                hubClient = NotificationHubClient.CreateClientFromConnectionString(connectionString, hubName);
            }
            catch (Exception exception)
            {
                log.LogError(exception, "Notification Hub connection string is invalid. Mobile push notifications are off.");
            }
        }

        public bool IsConfigured => hubClient is not null;

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Only tag the API ever sends to the server. Set Server-side from the JWT, never by the phone
        /// </summary>
        public static string TagFor(int userId) => $"user:{userId}";

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Called after every sign-in. Create or update, keyed on the app's installation id: the same phone
        /// signing in as someone else is re-tagged to them, so the previous user's notifications stop
        /// arriving on it. The hub keeps the token current and drops it when Firebase reports it expired.
        /// </summary>
        public async Task RegisterAsync(int userId, PushInstallationRequest request, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(request.Platform, "android", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only android push notifications are set up.");
            }

            if (hubClient is null)
            {
                return;
            }

            var pushInstallation = new FcmV1Installation(request.InstallationId.Trim(), request.PushChannel.Trim())
            {
                Tags = [TagFor(userId)]
            };

            await hubClient.CreateOrUpdateInstallationAsync(pushInstallation, cancellationToken);
        }

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Called after sign-out. Only the callers own phone can be removed (anti-IDOR, as AccessGuard)
        /// </summary>
        public async Task RemoveAsync(int userId, string installationId, CancellationToken cancellationToken = default)
        {
            if (hubClient is null)
            {
                return;
            }

            Installation existing;

            try
            {
                existing = await hubClient.GetInstallationAsync(installationId, cancellationToken);
            }
            catch (MessagingEntityNotFoundException)
            {
                return;
            }

            if (existing.Tags?.Contains(TagFor(userId)) != true)
            {
                return;
            }

            await hubClient.DeleteInstallationAsync(installationId, cancellationToken);
        }

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Sends one notification to every phone with its tagged owner
        /// </summary>
        public async Task SendAsync(PushJob pushJob, CancellationToken cancellationToken = default)
        {
            if (hubClient is null)
            {
                return;
            }

            await hubClient.SendFcmV1NativeNotificationAsync(BuildPayload(pushJob), TagFor(pushJob.UserId), cancellationToken);
        }

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// The FCM v1 message, wrapped in "message" as Notification Hubs expects. "notification" is what
        /// Android shows when the app is closed; "data" is what the app reads when it is tapped
        /// FCM data values must all be strings.
        /// </summary>
        public static string BuildPayload(PushJob pushJob) => JsonSerializer.Serialize(new
        {
            message = new
            {
                notification = new { title = TitleFor(pushJob.TypeName), body = pushJob.Message },
                android = new
                {
                    priority = "high",
                    notification = new
                    {
                        channel_id = ChannelId,
                        icon = "ic_stat_notify",
                        color = "#12271F",
                        tag = $"n{pushJob.NotificationId}"
                    }
                },

                data = new Dictionary<string, string>
                {
                    ["notificationId"] = pushJob.NotificationId.ToString(),
                    ["type"] = pushJob.TypeName,
                    ["entityType"] = pushJob.EntityType ?? "",
                    ["entityId"] = pushJob.EntityId?.ToString() ?? ""
                }
            }
        });

        //----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Notification titles. Unknown types fall back to the app name
        /// </summary>
        public static string TitleFor(string type) => type switch
        {
            "TaskAssigned" => "New task assigned",
            "TaskCancelled" => "Task cancelled",
            "DeadlineApproaching" => "Deadline approaching",
            "ProjectAssigned" => "Added to an event",
            "ProjectCancelled" => "Event cancelled",
            "EventInvite" => "Calendar invite",
            "RequestSubmitted" => "Request awaiting review",
            "RequestReviewed" => "Request reviewed",
            "RequestCompleted" => "Request fulfilled",
            "RequestEscalated" => "Request escalated",
            "UserLocked" => "Account locked",
            "PasswordResetRequested" => "Password reset requested",
            "Announcement" => "Announcement",
            _ => "Quloobul Moe'mieneen",
        };
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
