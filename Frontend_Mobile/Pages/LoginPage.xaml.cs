using Frontend_Mobile.Data;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        RolePicker.ItemsSource = (System.Collections.IList)RoleInfo.All;
        ThemeToggle.Text = App.ThemeGlyph;
    }

    private Role? SelectedRole => (RolePicker.SelectedItem as Option<Role>)?.Value;

    /// <summary>
    /// The Shell keeps this page alive between sessions, so a logout has to
    /// leave a blank form behind rather than the previous user's details.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (AppState.Current.IsLoggedIn) return;
        NameEntry.Text = string.Empty;
        RolePicker.SelectedItem = null;
        ThemeToggle.Text = App.ThemeGlyph;
    }

    private void OnRoleChanged(object? sender, EventArgs e)
    {
        RoleHint.Text = SelectedRole?.Hint() ?? "Pick a role to see which tabs it can access.";
        LoginButton.IsEnabled = SelectedRole is not null;
    }

    private async void OnLogin(object? sender, EventArgs e)
    {
        if (SelectedRole is not { } role) return;
        AppState.Current.Login(NameEntry.Text ?? string.Empty, role);
        await AppShell.EnterAppAsync();
    }

    private void OnToggleTheme(object? sender, EventArgs e)
    {
        App.ToggleTheme();
        ThemeToggle.Text = App.ThemeGlyph;
    }
}
