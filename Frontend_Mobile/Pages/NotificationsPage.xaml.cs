using Frontend_Mobile.Data;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages;

public partial class NotificationsPage : ContentPage
{
    private readonly AppState _state = AppState.Current;
    private NotificationType? _activeFilter;
    private bool _unreadOnly;

    public NotificationsPage()
    {
        InitializeComponent();
        Refresh();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var all = _state.Notifications;
        var unread = _state.UnreadNotificationCount;

        SubtitleLabel.Text = "Resource request updates and event assignments. " +
            (unread > 0 ? $"{unread} unread." : "You're all caught up.");
        MarkAllButton.IsEnabled = unread > 0;
        ClearAllButton.IsEnabled = all.Count > 0;

        BuildFilterBar();

        var filtered = all
            .Where(n => _activeFilter is null || n.Type == _activeFilter)
            .Where(n => !_unreadOnly || !n.IsRead)
            .ToList();

        BindableLayout.SetItemsSource(NotificationList, filtered);
        NotificationList.IsVisible = filtered.Count > 0;
        EmptyLabel.IsVisible = filtered.Count == 0;
        EmptyLabel.Text = all.Count == 0 ? "No notifications yet." : "Nothing matches this filter.";
    }

    private void BuildFilterBar()
    {
        var all = _state.Notifications;
        FilterBar.Children.Clear();

        AddChip($"All ({all.Count})", _activeFilter is null && !_unreadOnly, () =>
        {
            _activeFilter = null;
            _unreadOnly = false;
        });
        AddChip($"Resource Requests ({all.Count(n => n.Type == NotificationType.ResourceRequest)})",
            _activeFilter == NotificationType.ResourceRequest,
            () => _activeFilter = NotificationType.ResourceRequest);
        AddChip($"Event Assignments ({all.Count(n => n.Type == NotificationType.EventAssignment)})",
            _activeFilter == NotificationType.EventAssignment,
            () => _activeFilter = NotificationType.EventAssignment);
        AddChip("Unread only", _unreadOnly, () => _unreadOnly = !_unreadOnly);
    }

    private void AddChip(string label, bool selected, Action onTap)
    {
        var chip = new Button
        {
            Text = label,
            FontSize = 11,
            Padding = new Thickness(12, 6),
            MinimumHeightRequest = 32,
            HeightRequest = 32,
            CornerRadius = 16,
            Margin = new Thickness(0, 0, 8, 8),
            BackgroundColor = selected
                ? (Color)Application.Current!.Resources["Primary"]
                : Microsoft.Maui.Graphics.Colors.Transparent,
            BorderColor = (Color)Application.Current!.Resources["BorderSoft"],
            BorderWidth = selected ? 0 : 1,
            TextColor = selected
                ? Microsoft.Maui.Graphics.Colors.White
                : (Color)Application.Current!.Resources["TextMuted"]
        };
        chip.Clicked += (_, _) =>
        {
            onTap();
            Refresh();
        };
        FilterBar.Children.Add(chip);
    }

    private void OnMarkAllRead(object? sender, EventArgs e)
    {
        _state.MarkAllNotificationsAsRead();
        Refresh();
    }

    private void OnClearAll(object? sender, EventArgs e)
    {
        _state.ClearAllNotifications();
        Refresh();
    }

    private async void OnNotificationTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not NotificationItem item) return;

        _state.MarkNotificationAsRead(item);
        Refresh();

        await Shell.Current.GoToAsync(item.IsRequest ? "//requests" : "//events");
    }
}
