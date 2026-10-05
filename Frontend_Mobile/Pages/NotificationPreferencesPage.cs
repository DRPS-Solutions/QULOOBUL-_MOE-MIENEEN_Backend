using Frontend_Mobile.Data;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;

namespace Frontend_Mobile.Pages;

/// <summary>
/// UC-08 / test plan N4: per-type opt-out, the mobile twin of the web Profile's Notifications card.
/// A muted type sends neither in-app nor email; an admin's announcement ignores these, so it isn't listed.
/// Each switch saves as soon as it flips.
/// </summary>
public class NotificationPreferencesPage : ContentPage
{
    private readonly AppState _state = AppState.Current;
    private readonly VerticalStackLayout _list = new() { Spacing = 4 };
    private bool _syncing;

    private NotificationPreferencesPage()
    {
        Title = "Notification settings";
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Notification settings", Style = Res("TitleLarge") },
                    new Label
                    {
                        Text = "Choose what you hear about. Messages sent directly by an administrator always come through.",
                        Style = Res("Muted"),
                    },
                    _list,
                    new Button { Text = "Close", Style = Res("OutlineButton"), Margin = new Thickness(0, 12, 0, 0),
                                 Command = new Command(async () => await Navigation.PopModalAsync()) },
                },
            },
        };
    }

    public static Task ShowAsync(INavigation navigation) => navigation.PushModalAsync(new NotificationPreferencesPage());

    private static Style Res(string key) => (Style)Application.Current!.Resources[key];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _list.Children.Clear();
        _list.Children.Add(new ActivityIndicator { IsRunning = true });
        var prefs = await _state.GetAsync<List<NotificationPreferenceDto>>("api/notifications/preferences") ?? [];

        _list.Children.Clear();
        foreach (var pref in prefs.Where(p => p.CanMute))
        {
            var toggle = new Switch { IsToggled = pref.Enabled, VerticalOptions = LayoutOptions.Center };
            toggle.Toggled += async (_, e) =>
            {
                if (_syncing) return;
                toggle.IsEnabled = false;
                var ok = await _state.SendAsync(HttpMethod.Put, "api/notifications/preferences",
                    new UpdateNotificationPreferencesRequest { Preferences = [pref with { Enabled = e.Value }] });
                if (!ok) { _syncing = true; toggle.IsToggled = !e.Value; _syncing = false; } // AppState showed the error
                toggle.IsEnabled = true;
            };
            Grid.SetColumn(toggle, 1);
            _list.Children.Add(new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                ColumnSpacing = 12,
                Children =
                {
                    new Label { Text = pref.Label, VerticalOptions = LayoutOptions.Center, FontSize = 14 },
                    toggle,
                },
            });
        }
    }
}
