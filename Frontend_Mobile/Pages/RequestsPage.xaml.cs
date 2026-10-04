using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using QULOOBUL__MOE_MIENEEN_Shared.Requests;

namespace Frontend_Mobile.Pages;

public partial class RequestsPage : ContentPage
{
    private readonly AppState _state = AppState.Current;
    private RequestStatus? _activeFilter;
    private bool _submitting;

    public RequestsPage()
    {
        InitializeComponent();
        TypePicker.ItemDisplayBinding = new Binding(nameof(RequestCategoryDto.CategoryName));
        LinkTypePicker.ItemsSource = (System.Collections.IList)RequestEnums.LinkTypes;
        LinkTypePicker.SelectedIndex = 0;
        Refresh();
    }

    // A SignalR push (a request submitted or reviewed elsewhere) reloads the
    // list, and Changed re-renders while we're on screen.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _state.Changed -= Refresh;
        _state.Changed += Refresh;
        Refresh();

        var categories = _state.GetAsync<List<RequestCategoryDto>>("api/requests/categories");
        await Task.WhenAll(categories, _state.RefreshRequestsAsync());
        if (await categories is { } list && TypePicker.ItemsSource is null)
        {
            TypePicker.ItemsSource = list;
            TypePicker.SelectedIndex = list.Count > 0 ? 0 : -1;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _state.Changed -= Refresh;
    }

    private RequestLinkType SelectedLinkType =>
        (LinkTypePicker.SelectedItem as Option<RequestLinkType>)?.Value ?? RequestLinkType.None;

    private string? SelectedLinkedName => LinkedNamePicker.SelectedItem as string;

    private List<ResourceRequest> Items() =>
        _state.Requests
            .Select(r => new ResourceRequest(
                r,
                _state.UserName(r.RequestedBy),
                r.ReviewedBy is null ? null : _state.UserName(r.ReviewedBy),
                _state.CanReview(r),
                _state.CanResubmit(r),
                _state.CanComplete(r)))
            .ToList();

    private void Refresh()
    {
        var all = Items();
        BuildFilterBar(all);

        var filtered = all
            .Where(r => _activeFilter is null || r.Status == _activeFilter)
            .OrderByDescending(r => r.Dto.DateSubmitted)
            .ToList();

        BindableLayout.SetItemsSource(RequestList, filtered);
        RequestList.IsVisible = filtered.Count > 0;
        EmptyLabel.IsVisible = filtered.Count == 0;

        UpdateSubmitState();
    }

    private void BuildFilterBar(List<ResourceRequest> all)
    {
        FilterBar.Children.Clear();
        AddChip($"All ({all.Count})", _activeFilter is null, null);
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            var count = all.Count(r => r.Status == status);
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

    // Events are the API's projects; tasks are whichever window was last loaded
    // (same as the web client's link dropdown).
    private void OnLinkTypeChanged(object? sender, EventArgs e)
    {
        var linkType = SelectedLinkType;
        LinkedNameSection.IsVisible = linkType != RequestLinkType.None;
        if (linkType != RequestLinkType.None)
        {
            LinkedNameLabel.Text = linkType == RequestLinkType.Event ? "Which event?" : "Which task?";
            LinkedNamePicker.ItemsSource = linkType == RequestLinkType.Event
                ? _state.EventProjects.Select(p => p.ProjectName).ToList()
                : _state.Tasks.Select(t => t.Title).Distinct().ToList();
        }
        LinkedNamePicker.SelectedItem = null;
        UpdateSubmitState();
    }

    private void OnFormChanged(object? sender, EventArgs e) => UpdateSubmitState();

    private void UpdateSubmitState() =>
        SubmitButton.IsEnabled = !_submitting
            && !string.IsNullOrWhiteSpace(TitleEntry.Text)
            && TypePicker.SelectedItem is RequestCategoryDto
            && (SelectedLinkType == RequestLinkType.None || !string.IsNullOrWhiteSpace(SelectedLinkedName));

    private async void OnSubmit(object? sender, EventArgs e)
    {
        if (TypePicker.SelectedItem is not RequestCategoryDto category) return;

        // A request stores one Detail text; its first line is the title (same layout as the web client).
        var detail = string.Join("\n", new[]
        {
            TitleEntry.Text!.Trim(),
            string.IsNullOrWhiteSpace(AmountEntry.Text) ? null : $"Quantity: {AmountEntry.Text.Trim()}",
            SelectedLinkType == RequestLinkType.None ? null : $"For {SelectedLinkType.ToString().ToLower()}: {SelectedLinkedName}",
            NotesEditor.Text?.Trim(),
        }.Where(line => !string.IsNullOrWhiteSpace(line)));

        _submitting = true;
        UpdateSubmitState();
        var ok = await _state.SubmitRequestAsync(category.CategoryId, detail);
        _submitting = false;

        // Reset on success, but keep the category - people often submit a few
        // similar requests in a row, same as the Blazor original.
        if (ok)
        {
            TitleEntry.Text = string.Empty;
            AmountEntry.Text = string.Empty;
            NotesEditor.Text = string.Empty;
            LinkTypePicker.SelectedIndex = 0;
        }
        UpdateSubmitState();
    }

    private async void OnApprove(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not ResourceRequest request) return;
        await _state.ReviewRequestAsync(request.Dto.RequestId, approve: true, note: null);
    }

    private async void OnDeny(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not ResourceRequest request) return;

        // DenyRequestDialog.razor: an optional reason, which the API puts in the
        // requester's notification. Cancelling returns null and leaves the request as it was.
        var reason = await DisplayPromptAsync(
            "Deny request",
            request.Title,
            accept: "Deny request",
            cancel: "Cancel",
            placeholder: "Reason (optional)",
            maxLength: 500); // the API's limit (R3)

        if (reason is null) return;

        await _state.ReviewRequestAsync(request.Dto.RequestId, approve: false,
            note: string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
    }

    // SM-02 fulfil: an approved request is closed once the items are handed over.
    private async void OnComplete(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not ResourceRequest request) return;
        await _state.CompleteRequestAsync(request.Dto.RequestId);
    }

    // The prompt edits the whole request text; its first line stays the title.
    private async void OnResubmit(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not ResourceRequest request) return;

        var detail = await DisplayPromptAsync(
            "Revise & resubmit",
            "Change what the reviewer asked about, then send it back. The first line is the title.",
            accept: "Send for review",
            cancel: "Cancel",
            maxLength: 1000,
            initialValue: request.Dto.Detail ?? string.Empty);

        if (string.IsNullOrWhiteSpace(detail)) return;
        await _state.ResubmitRequestAsync(request.Dto.RequestId, detail.Trim());
    }
}
