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
        if (AppState.Current.IsLoggedIn || AppState.Current.MustChangePassword) return;
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
        if (error is not null) return;
        if (AppState.Current.MustChangePassword) ShowChangeForm(true);
        else await AppShell.EnterAppAsync();
    }

    // The password just typed is the temporary one, so it doubles as the "current password".
    private void ShowChangeForm(bool show)
    {
        SignInSection.IsVisible = !show;
        ChangeSection.IsVisible = show;
        NewPasswordEntry.Text = ConfirmPasswordEntry.Text = string.Empty;
        ChangeErrorLabel.IsVisible = false;
        if (!show) PasswordEntry.Text = string.Empty;
    }

    private void OnChangeFormChanged(object? sender, TextChangedEventArgs e)
    {
        // Same policy as AuthService.EnsurePolicy, so the message shows before submitting.
        var p = NewPasswordEntry.Text ?? "";
        var error = p.Length == 0 ? null
            : p.Length < 8 || !p.Any(char.IsLetter) || !p.Any(char.IsDigit) ? "At least 8 characters, with a letter and a digit."
            : !string.IsNullOrEmpty(ConfirmPasswordEntry.Text) && ConfirmPasswordEntry.Text != p ? "Passwords do not match."
            : null;
        ChangeErrorLabel.Text = error;
        ChangeErrorLabel.IsVisible = error is not null;
        ChangeButton.IsEnabled = !_busy && error is null && p.Length > 0 && ConfirmPasswordEntry.Text == p;
    }

    private async void OnChangePassword(object? sender, EventArgs e)
    {
        if (!ChangeButton.IsEnabled) return;
        _busy = true;
        ChangeButton.IsEnabled = false;
        ChangeButton.Text = "Saving...";
        var ok = await AppState.Current.ChangePasswordAsync(PasswordEntry.Text!, NewPasswordEntry.Text!);
        _busy = false;
        ChangeButton.Text = "Change Password";
        if (!ok) { OnChangeFormChanged(null, null!); return; } // AppState already showed the error
        ShowChangeForm(false);
        await AppShell.EnterAppAsync();
    }

    private async void OnCancelChange(object? sender, EventArgs e)
    {
        await AppState.Current.LogoutAsync();
        ShowChangeForm(false);
    }

    private void OnToggleTheme(object? sender, EventArgs e)
    {
        App.ToggleTheme();
        ThemeToggle.Text = App.ThemeGlyph;
    }
}
