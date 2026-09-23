using Frontend_Mobile.Data;

namespace Frontend_Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        BindingContext = AppState.Current;
        ThemeToggle.Text = App.ThemeGlyph;
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

    private void OnToggleTheme(object? sender, EventArgs e)
    {
        App.ToggleTheme();
        ThemeToggle.Text = App.ThemeGlyph;
    }

    private async void OnLogout(object? sender, EventArgs e) => await SignOutAsync();
}
