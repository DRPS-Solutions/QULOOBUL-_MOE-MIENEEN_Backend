using Android.App;
using Firebase.Messaging;
using Frontend_Mobile.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Frontend_Mobile.Platforms.Android
{
    /// <summary>
    /// Receives Firebase Cloud Messaging callbacks. The attributes below generate the manifest entry, so
    /// AndroidManifest.xml doesn't list this service by hand.
    ///
    /// App in the background or closed: Android puts the notification in the tray itself and this class
    /// is not called; tapping it starts MainActivity with the message data as intent extras.
    /// App in the foreground: OnMessageReceived runs and we show the notification ourselves.
    /// </summary>
    [Service(Exported = false)]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class PushMessagingService : FirebaseMessagingService
    {
        // Firebase 25.1 marked the token API obsolete in favour of installation IDs, but Azure Notification
        // Hubs still addresses Android phones by FCM registration token, so the token API is the one to use.
        // Suppressed here only, because Directory.Build.props makes every warning a build error.
        #pragma warning disable CS0618, CS0672 // obsolete member / overrides obsolete member
        /// <summary>Firebase issued a new token (first launch, reinstall, cleared data): re-register it</summary>
        public override void OnNewToken(string token)
        {
            base.OnNewToken(token);
            _ = PushNotifications.OnNewTokenAsync(token);
        }

        #pragma warning restore CS0618, CS0672
        public override void OnMessageReceived(RemoteMessage message)
        {
            base.OnMessageReceived(message);
            
            var notification = message.GetNotification();
            PushSetup.Show(this,
                notification?.Title ?? "Quloobul Moe'mieneen",
                notification?.Body ?? "",
                message.Data);
        }
    }
}
