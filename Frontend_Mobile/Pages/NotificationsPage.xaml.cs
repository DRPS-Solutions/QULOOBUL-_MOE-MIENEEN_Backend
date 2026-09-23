using Frontend_Mobile.Data;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages;

public partial class NotificationsPage : ContentPage
{
    private readonly AppState _state = AppState.Current;
    private NotificationKind? _activeFilter;
    private bool _unreadOnly;

    public NotificationsPage()
    {
        InitializeComponent();
        Refresh();
    }

    // The API only ever returns the signed-in user's own notifications; a
    // SignalR push reloads them and Changed re-renders while we're on screen.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _state.Changed -= Refresh;
        _state.Changed += Refresh;
        Refresh();
        await _state.RefreshNotificationsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _state.Changed -= Refresh;
    }

    private void Refresh()
    {
        var all = _state.Notifications.Select(n => new NotificationItem(n)).ToList();
        var unread = _state.UnreadNotificationCount;

        SubtitleLabel.Text = "Task assignments, resource request updates and event assignments. " +
            (unread > 0 ? $"{unread} unread." : "You're all caught up.");
        MarkAllButton.IsEnabled = unread > 0;

        BuildFilterBar(all);

        var filtered = all
            .Where(n => _activeFilter is null || n.Kind == _activeFilter)
            .Where(n => !_unreadOnly || !n.IsRead)
            .ToList();

        BindableLayout.SetItemsSource(NotificationList, filtered);
        NotificationList.IsVisible = filtered.Count > 0;
        EmptyLabel.IsVisible = filtered.Count == 0;
        EmptyLabel.Text = all.Count == 0 ? "No notifications yet." : "Nothing matches this filter.";
    }

    private void BuildFilterBar(List<NotificationItem> all)
    {
        FilterBar.Children.Clear();

        AddChip($"All ({all.Count})", _activeFilter is null && !_unreadOnly, () =>
        {
            _activeFilter = null;
            _unreadOnly = false;
        });
        AddKindChip(all, "Tasks", NotificationKind.TaskAssignment);
        AddKindChip(all, "Resource Requests", NotificationKind.ResourceRequest);
        AddKindChip(all, "Event Assignments", NotificationKind.EventAssignment);
        AddChip("Unread only", _unreadOnly, () => _unreadOnly = !_unreadOnly);
    }

    private void AddKindChip(List<NotificationItem> all, string label, NotificationKind kind) =>
        AddChip($"{label} ({all.Count(n => n.Kind == kind)})", _activeFilter == kind, () => _activeFilter = kind);

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

    private async void OnMarkAllRead(object? sender, EventArgs e) =>
        await _state.MarkReadAsync(_state.Notifications.Where(n => !n.IsRead).Select(n => n.NotificationId).ToList());

    /// <summary>
    /// Marks the notification read and opens the page that owns that kind of
    /// item, if this role can see it (UC-08). Task notifications fall back to Home.
    /// </summary>
    private async void OnNotificationTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not NotificationItem item) return;

        if (!item.IsRead) await _state.MarkReadAsync([item.Dto.NotificationId]);

        var route = item.Kind switch
        {
            NotificationKind.ResourceRequest when _state.CanSeeRequests => "//requests",
            NotificationKind.EventAssignment when _state.CanSeeEvents => "//events",
            NotificationKind.TaskAssignment => _state.CanSeeTasks ? "//tasks" : "//home",
            _ => null
        };
        if (route is not null) await Shell.Current.GoToAsync(route);
    }
}
