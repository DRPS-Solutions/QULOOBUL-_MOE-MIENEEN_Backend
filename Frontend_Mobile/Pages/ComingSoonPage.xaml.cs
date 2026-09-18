namespace Frontend_Mobile.Pages;

public partial class ComingSoonPage : ContentPage
{
    public ComingSoonPage() => InitializeComponent();

    /// <summary>Set from AppShell.xaml so one page serves both stubs.</summary>
    public string FeatureName
    {
        get => TitleLabel.Text;
        set => TitleLabel.Text = value;
    }
}
