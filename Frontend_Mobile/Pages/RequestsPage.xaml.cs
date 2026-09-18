using Frontend_Mobile.Data;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages;

public partial class RequestsPage : ContentPage
{
    private readonly AppState _state = AppState.Current;
    private RequestStatus? _activeFilter;

    public RequestsPage()
    {
        InitializeComponent();
        TypePicker.ItemsSource = (System.Collections.IList)RequestEnums.ResourceTypes;
        TypePicker.SelectedIndex = 0;
        LinkTypePicker.ItemsSource = (System.Collections.IList)RequestEnums.LinkTypes;
        LinkTypePicker.SelectedIndex = 0;
        Refresh();
    }

    /// <summary>
    /// Rebuilds the rows so the Admin-only Approve/Deny controls re-evaluate.
    /// The page instance outlives a logout, so a re-login as a different role
    /// would otherwise keep showing the previous role's controls.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private RequestLinkType SelectedLinkType =>
        (LinkTypePicker.SelectedItem as Option<RequestLinkType>)?.Value ?? RequestLinkType.None;

    private string? SelectedLinkedName => LinkedNamePicker.SelectedItem as string;

    private void Refresh()
    {
        BuildFilterBar();

        var filtered = _state.Requests
            .Where(r => _activeFilter is null || r.Status == _activeFilter)
            .OrderByDescending(r => r.RequestedDate)
            .ToList();

        BindableLayout.SetItemsSource(RequestList, filtered);
        RequestList.IsVisible = filtered.Count > 0;
        EmptyLabel.IsVisible = filtered.Count == 0;

        UpdateSubmitState();
    }

    private void BuildFilterBar()
    {
        FilterBar.Children.Clear();
        AddChip($"All ({_state.Requests.Count})", _activeFilter is null, null);
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            var count = _state.Requests.Count(r => r.Status == status);
            AddChip($"{status} ({count})", _activeFilter == status, status);
        }
    }

    private void AddChip(string label, bool selected, RequestStatus? filter)
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
            _activeFilter = filter;
            Refresh();
        };
        FilterBar.Children.Add(chip);
    }

    private void OnLinkTypeChanged(object? sender, EventArgs e)
    {
        var linkType = SelectedLinkType;
        LinkedNameSection.IsVisible = linkType != RequestLinkType.None;
        if (linkType != RequestLinkType.None)
        {
            LinkedNameLabel.Text = linkType == RequestLinkType.Event ? "Which event?" : "Which task?";
            LinkedNamePicker.ItemsSource = (System.Collections.IList)(linkType == RequestLinkType.Event
                ? _state.EventOptions
                : _state.TaskOptions);
        }
        LinkedNamePicker.SelectedItem = null;
        UpdateSubmitState();
    }

    private void OnFormChanged(object? sender, EventArgs e) => UpdateSubmitState();

    private void UpdateSubmitState() =>
        SubmitButton.IsEnabled = !string.IsNullOrWhiteSpace(TitleEntry.Text)
            && (SelectedLinkType == RequestLinkType.None || !string.IsNullOrWhiteSpace(SelectedLinkedName));

    private void OnSubmit(object? sender, EventArgs e)
    {
        _state.SubmitRequest(
            TitleEntry.Text ?? string.Empty,
            ((Option<ResourceType>)TypePicker.SelectedItem).Value,
            string.IsNullOrWhiteSpace(AmountEntry.Text) ? null : AmountEntry.Text,
            string.IsNullOrWhiteSpace(NotesEditor.Text) ? null : NotesEditor.Text,
            SelectedLinkType,
            SelectedLinkedName);

        // Reset, but keep the last-used resource type - people often submit a
        // few similar requests in a row, same as the Blazor original.
        TitleEntry.Text = string.Empty;
        AmountEntry.Text = string.Empty;
        NotesEditor.Text = string.Empty;
        LinkTypePicker.SelectedIndex = 0;

        Refresh();
    }

    private void OnApprove(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not ResourceRequest request) return;
        _state.ApproveRequest(request);
        Refresh();
    }

    private async void OnDeny(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not ResourceRequest request) return;

        // DenyRequestDialog.razor: an optional reason. Cancelling returns null
        // and leaves the request Pending.
        var reason = await DisplayPromptAsync(
            "Deny request",
            request.Title,
            accept: "Deny request",
            cancel: "Cancel",
            placeholder: "Reason (optional)");

        if (reason is null) return;

        _state.DenyRequest(request, string.IsNullOrWhiteSpace(reason) ? null : reason);
        Refresh();
    }
}
