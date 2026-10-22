using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Frontend_Mobile.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Frontend_Mobile.Platforms.Android
{
    /// <summary>
    /// Android plumbing for push notifications: the notification channel, showing a notification
    /// ourselves when one arrives while the app is open, and reading a tapped notification's data.
    /// </summary>
    public static class PushSetup
    {
        // The keys PushHubService puts in each message's data; the only extras we read back
        private static readonly string[] DataKeys = ["notificationId", "type", "entityType", "entityId"];

        /// <summary>
        /// Android 8+ shows nothing without a channel. Created at app start (MainApplication) so it already
        /// exists when a push arrives for an app that isn't running. Safe to call repeatedly
        /// </summary>
        public static void EnsureChannel(Context context)
        {
            // OperatingSystem.IsAndroidVersionAtLeast rather than Build.VERSION.SdkInt: it is the check the
            // platform analyzer (CA1416) understands, and CI builds with warnings as errors
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                return;
            }

            var channel = new NotificationChannel(PushNotifications.ChannelId, "Notifications", NotificationImportance.High)
            {
                Description = "Task assignments, resource requests, events and announcements"
            };

            (context.GetSystemService(Context.NotificationService) as NotificationManager)?.CreateNotificationChannel(channel);
        }

        /// <summary>
        /// While the app is in the foreground Firebase hands the message to us instead of the tray, so we
        /// build the notification ourselves, with the same data, so tapping it behaves the same.
        /// </summary>
        public static void Show(Context context, string title, string body, IDictionary<string, string> data)
        {
            EnsureChannel(context);

            var intent = new Intent(context, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            foreach (var key in DataKeys)
            {
                if (data.TryGetValue(key, out var value))
                {
                    intent.PutExtra(key, value);
                }
            }

            var requestCode = data.TryGetValue("notificationId", out var raw) && int.TryParse(raw, out var id)
                ? id
                : System.Environment.TickCount;
            
            var tap = PendingIntent.GetActivity(context, requestCode, intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            // One call per line, not a chain: every AndroidX builder method is declared as returning a
            // nullable Builder, so chaining them is a nullable warning (an error in CI)
            var builder = new NotificationCompat.Builder(context, PushNotifications.ChannelId);
            builder.SetSmallIcon(Resource.Drawable.ic_stat_notify);
            builder.SetColor(AndroidX.Core.Content.ContextCompat.GetColor(context, Resource.Color.notification_accent));
            builder.SetContentTitle(title);
            builder.SetContentText(body);
            
            var bigText = new NotificationCompat.BigTextStyle();
            bigText.BigText(body);
            builder.SetStyle(bigText);
            builder.SetPriority(NotificationCompat.PriorityHigh);
            builder.SetAutoCancel(true);
            builder.SetContentIntent(tap);

            try
            {
                NotificationManagerCompat.From(context)?.Notify($"n{requestCode}", requestCode, builder.Build());
            }
            catch (Java.Lang.SecurityException)
            {
                // POST_NOTIFICATIONS was refused on Android 13+: the in-app list still updates over SignalR
            }
        }

        /// <summary>
        /// If the activity was opened by tapping one of our notifications (from the tray, or one built by
        /// <see cref="Show"/>), pass its data on and clear it so a recreated activity doesn't open it twice
        /// </summary>
        public static void HandleIntent(Intent? intent)
        {
            var extras = intent?.Extras;

            if (extras is null || !extras.ContainsKey("notificationId"))
            {
                return;
            }

            var data = new Dictionary<string, string>();
            
            foreach (var key in DataKeys)
            {
                if (extras.GetString(key) is { } value) data[key] = value;
                intent!.RemoveExtra(key);
            }

            PushNotifications.HandleTap(data);
        }
    }
}
