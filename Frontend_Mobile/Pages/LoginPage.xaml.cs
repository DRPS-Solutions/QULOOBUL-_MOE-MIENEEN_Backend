using Frontend_Mobile.Data;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;

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

    private void OnShowForgot(object? sender, EventArgs e) => ShowForgotForm(true);

    private void OnHideForgot(object? sender, EventArgs e) => ShowForgotForm(false);

    private void ShowForgotForm(bool show)
    {
        SignInSection.IsVisible = !show;
        ForgotSection.IsVisible = show;
        ForgotForm.IsVisible = true;
        ForgotIntroLabel.Text = "Tell the system admin who you are, and they'll send you a temporary password.";
        ForgotNameEntry.Text = ForgotSurnameEntry.Text = string.Empty;
        ForgotEmailEntry.Text = EmailEntry.Text;
    }

    private void OnForgotFormChanged(object? sender, TextChangedEventArgs e) =>
        ForgotButton.IsEnabled = !_busy
            && !string.IsNullOrWhiteSpace(ForgotNameEntry.Text)
            && !string.IsNullOrWhiteSpace(ForgotSurnameEntry.Text)
            && !string.IsNullOrWhiteSpace(ForgotEmailEntry.Text);

    private async void OnSendForgot(object? sender, EventArgs e)
    {
        if (!ForgotButton.IsEnabled) return;
        _busy = true;
        ForgotButton.IsEnabled = false;
        ForgotButton.Text = "Sending...";
        var ok = await AppState.Current.SendAsync(HttpMethod.Post, "api/auth/forgot-password", new ForgotPasswordRequest
        {
            FirstName = ForgotNameEntry.Text!, Surname = ForgotSurnameEntry.Text!, Email = ForgotEmailEntry.Text!,
        });
        _busy = false;
        ForgotButton.Text = "Send Request";
        if (!ok) { OnForgotFormChanged(null, null!); return; } // AppState already showed the error
        ForgotForm.IsVisible = false;
        ForgotIntroLabel.Text = "Request sent. The system admin will contact you with a temporary password.";
    }

    // Each eye button sits in a Grid beside the Entry it reveals.
    private void OnTogglePassword(object? sender, EventArgs e)
    {
        var button = (Button)sender!;
        var entry = (Entry)((Grid)button.Parent).Children[0];
        entry.IsPassword = !entry.IsPassword;
        button.Text = entry.IsPassword ? "\U0001F441" : "\U0001F648";
        SemanticProperties.SetDescription(button, entry.IsPassword ? "Show password" : "Hide password");
    }

    private void OnToggleTheme(object? sender, EventArgs e)
    {
        App.ToggleTheme();
        ThemeToggle.Text = App.ThemeGlyph;
    }
}
