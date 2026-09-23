namespace Frontend_Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    // The Shell is the one and only root page for the whole app lifetime;
    // login/logout navigate between its //login and //home routes rather than
    // replacing the root (see the comment in AppShell.xaml).
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell()) { Title = "Quloobul Moe'mieneen" };

    // The in-app dark-mode switch from MainLayout.razor's isDarkMode toggle.
    // Setting UserAppTheme is all AppThemeBinding needs to re-resolve.
    public static void ToggleTheme() =>
        Current!.UserAppTheme = Current.RequestedTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;

    public static string ThemeGlyph => Current?.RequestedTheme == AppTheme.Dark ? "☀" : "\U0001F319";
}
