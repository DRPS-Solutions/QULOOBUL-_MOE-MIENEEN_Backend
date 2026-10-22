using Frontend_Mobile.Data;

namespace Frontend_Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        BindingContext = AppState.Current;
    }

    /// <summary>
    /// Called from LoginPage once the API accepts the sign-in. The drawer only exists for
    /// a signed-in session, matching MainLayout.razor showing the nav menu only
    /// when currentRole is set.
    /// </summary>
    public static async Task EnterAppAsync()
    {
        if (Current is not AppShell shell) return;
        shell.FlyoutBehavior = FlyoutBehavior.Flyout;
        await Current.GoToAsync("//home");

        // Opened the app by tapping a push notification: go on to what it was about
        await PushNotifications.OpenPendingAsync();
    }

    public static async Task SignOutAsync()
    {
        if (Current is not AppShell shell) return;
        shell.FlyoutIsPresented = false;
        await AppState.Current.LogoutAsync();
        await Current.GoToAsync("//login");
        shell.FlyoutBehavior = FlyoutBehavior.Disabled;
    }

    private async void OnOpenNotifications(object? sender, EventArgs e) =>
        await GoToAsync("//notifications");

    private async void OnOpenSearch(object? sender, EventArgs e) =>
        await GoToAsync("//search");

    private async void OnLogout(object? sender, EventArgs e) => await SignOutAsync();
}
