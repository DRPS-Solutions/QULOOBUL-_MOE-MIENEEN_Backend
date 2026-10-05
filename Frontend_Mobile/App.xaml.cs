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
}
