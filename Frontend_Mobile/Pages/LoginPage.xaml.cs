using Frontend_Mobile.Data;

namespace Frontend_Mobile.Pages;

public partial class LoginPage : ContentPage
{
    // Resume a remembered session once per app launch, not after every logout.
    private static bool _restoreTried;
    private bool _busy;

    public LoginPage()
    {
        InitializeComponent();
        ThemeToggle.Text = App.ThemeGlyph;
    }

    /// <summary>
    /// The Shell keeps this page alive between sessions, so a logout has to
    /// leave a blank form behind rather than the previous user's details.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (AppState.Current.IsLoggedIn) return;
        EmailEntry.Text = string.Empty;
        PasswordEntry.Text = string.Empty;
        ErrorLabel.IsVisible = false;
        ThemeToggle.Text = App.ThemeGlyph;

        if (_restoreTried) return;
        _restoreTried = true;
        SetBusy(true);
        if (await AppState.Current.TryRestoreAsync()) await AppShell.EnterAppAsync();
        SetBusy(false);
    }

    private void OnFormChanged(object? sender, TextChangedEventArgs e) => SetBusy(_busy);

    private void SetBusy(bool busy)
    {
        _busy = busy;
        LoginButton.Text = busy ? "Signing in..." : "Log In";
        LoginButton.IsEnabled = !busy
            && !string.IsNullOrWhiteSpace(EmailEntry.Text)
            && !string.IsNullOrEmpty(PasswordEntry.Text);
    }

    private async void OnLogin(object? sender, EventArgs e)
    {
        if (!LoginButton.IsEnabled) return;
        SetBusy(true);
        var error = await AppState.Current.LoginAsync(EmailEntry.Text!, PasswordEntry.Text!);
        SetBusy(false);

        ErrorLabel.Text = error;
        ErrorLabel.IsVisible = error is not null;
        if (error is null) await AppShell.EnterAppAsync();
    }

    private void OnToggleTheme(object? sender, EventArgs e)
    {
        App.ToggleTheme();
        ThemeToggle.Text = App.ThemeGlyph;
    }
}
