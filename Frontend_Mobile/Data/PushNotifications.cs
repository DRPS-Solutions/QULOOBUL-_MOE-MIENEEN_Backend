#if ANDROID
using System.Diagnostics;
using Android.Gms.Extensions;
#endif

namespace Frontend_Mobile.Data;

/// <summary>
/// Phone push notifications (UC-08 on mobile) through Azure Notification Hubs, as the architecture doc
/// specifies: "the mobile app registers its device token on startup".
///
/// Flow: after sign-in, the app asks for notification permission, gets its FCM registration token from
/// Firebase and sends it to the API (PUT api/notifications/installations). The API registers the phone
/// with the hub under the tag user:{id}; from then on, every notification the API saves for that user
/// reaches this phone, even with the app closed. Tapping one opens the same page the Notifications list
/// would. On sign-out, the API removes the phone from the hub.
///
/// Android only: iOS needs an Apple Developer account and an APNs key on the hub (architecture doc,
/// "Key Edge Case"). Everywhere else these methods do nothing, so the other targets build unchanged.
/// </summary>
public static class PushNotifications
{
    /// <summary>Android notification channel; the API's PushHubService sends with the same id</summary>
    public const string ChannelId = "qm_general";

    private const string InstallationIdKey = "push_installation_id";

    // A tapped notification waiting for the user to be signed in (cold start lands on Login first).
    private static Dictionary<string, string>? _pendingTap;

    /// <summary>
    /// This install's id in the hub: a GUID made once and kept, so re-registering after a sign-in or a
    /// new token updates the same hub entry instead of adding another one.
    /// </summary>
    public static string InstallationId
    {
        get
        {
            var id = Preferences.Default.Get<string?>(InstallationIdKey, null);
            
            if (id is null)
            {
                id = Guid.NewGuid().ToString("N");
                Preferences.Default.Set(InstallationIdKey, id);
            }

            return id;
        }
    }

    /// <summary>Called by AppState once a session has started (fresh login or restored)</summary>
    public static async Task EnableAsync()
    {
        #if ANDROID
        try
        {
            // Ask for permission (Android 13+) but don't wait for the answer: the token doesn't need it, and if the
            // request never reports back, registration would stall silently. If the user says no, they can
            // switch notifications on later in system settings.
            _ = MainThread.InvokeOnMainThreadAsync(() =>
                Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.PostNotifications>());

            // See PushMessagingService for why the (obsolete) token API is the right one here.
            // Give up after 30 seconds rather than waiting silently when Google can't be reached.
            #pragma warning disable CS0618
            var token = await Firebase.Messaging.FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>()
                .WaitAsync(TimeSpan.FromSeconds(30));
            
            #pragma warning restore CS0618
            #if DEBUG
            // Debug builds only: lets you send a test message to this phone from the Firebase console.
            Debug.WriteLine($"FCM token: {token}");
            
            #endif
            await OnNewTokenAsync(token.ToString());
        }
        catch (Exception ex)
        {
            // Usually a missing google-services.json, or no Google Play services on the emulator image.
            // The app works without push, so this is logged rather than shown to the user.
            Debug.WriteLine($"Push notifications unavailable: {ex.Message}");
        }
        
        #else
        await Task.CompletedTask;
        #endif
    }

    /// <summary>Called by AppState while signing out, before the auth header is cleared.</summary>
    public static async Task DisableAsync()
    {
        #if ANDROID
        await AppState.Current.RemovePushInstallationAsync(InstallationId);
        
        try
        {
            // Covers a sign-out the API couldn't hear about (expired token): the old FCM token stops
            // working, so the hub drops it, and the next sign-in gets a fresh one.
            #pragma warning disable CS0618
            await Firebase.Messaging.FirebaseMessaging.Instance.DeleteToken().AsAsync();
            #pragma warning restore CS0618
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FCM token delete failed: {ex.Message}");
        }
        
        #else
        await Task.CompletedTask;
        #endif
    }

    /// <summary>
    /// A token from sign-in or from Firebase's OnNewToken. Registered with the API if someone is
    /// signed in; otherwise the next sign-in fetches the current token itself.
    /// </summary>
    public static async Task OnNewTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !AppState.Current.IsLoggedIn)
        {
            return;
        }

        await AppState.Current.RegisterPushInstallationAsync(InstallationId, token);
    }

    /// <summary>
    /// MainActivity passes the data of a tapped notification here. If the user is already signed in
    /// it opens straight away; otherwise it waits for <see cref="OpenPendingAsync"/> after sign-in
    /// </summary>
    public static void HandleTap(Dictionary<string, string> data)
    {
        _pendingTap = data;

        if (AppState.Current.IsLoggedIn)
        {
            MainThread.BeginInvokeOnMainThread(async () => await OpenPendingAsync());
        }
    }

    /// <summary>Opens the record a tapped notification is about, and marks it read</summary>
    public static async Task OpenPendingAsync()
    {
        if (_pendingTap is not { } data || !AppState.Current.IsLoggedIn || Shell.Current is null)
        {
            return;
        }

        _pendingTap = null;

        if (int.TryParse(data.GetValueOrDefault("notificationId"), out var notificationId))
        {
            await AppState.Current.MarkReadAsync([notificationId]);
        }

        int? entityId = int.TryParse(data.GetValueOrDefault("entityId"), out var id) ? id : null;
        // Same routing as tapping the row on the Notifications page; anything this role
        // has no page for opens the Notifications list instead
        var route = AppState.Current.LinkTo(data.GetValueOrDefault("entityType"), entityId) ?? "//notifications";
        await Shell.Current.GoToAsync(route);
    }
}