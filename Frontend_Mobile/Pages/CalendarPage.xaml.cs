using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;
using Permissions = QULOOBUL__MOE_MIENEEN_Shared.Classes.Common.Permissions;

namespace Frontend_Mobile.Pages;

/// <summary>
/// UC-06 Calendar.razor's mobile equivalent: dated organisational events (the API's Event
/// entity, distinct from Event Planning's Project entity on EventsPage). Open to every role.
/// calendar?event=5 (an EventInvite notification's link) opens that event, same as the web client.
/// </summary>
[QueryProperty(nameof(DeepLinkEventId), "event")]
public partial class CalendarPage : ContentPage
{
    private readonly AppState _state = AppState.Current;

    private List<EventDto> _events = [];
    private EventDto? _selected;
    private List<EventAttendeeDto> _attendees = [];
    private bool _showPast;
    private bool _creating;
    private int? _pendingDeepLinkEventId;

    public CalendarPage()
    {
        InitializeComponent();
        InviteePicker.ItemDisplayBinding = new Binding(nameof(UserSummaryDto.FullName));
        ResetCreateForm();
    }

    // Shell hands query string values in as strings; a bad/missing one is just ignored.
    // Public, not private, because Shell's query-property reflection only sees public members.
    public string? DeepLinkEventId
    {
        set
        {
            if (int.TryParse(value, out var id)) _pendingDeepLinkEventId = id;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Load();
        if (_pendingDeepLinkEventId is { } id)
        {
            _pendingDeepLinkEventId = null;
            await Select(id);
        }
    }

    private int MyId => _state.CurrentUser?.UserId ?? 0;

    // The API lets only the organiser or an admin change who is on an event.
    private bool CanManage => _selected is not null
        && (_selected.OrganiserId == MyId || _state.HasPermission(Permissions.ManageUsers));

    // ----- Loading -----

    private async Task Load()
    {
        _events = await _state.ListEventsAsync(_showPast) ?? [];
        RenderEventList();
    }

    private void RenderEventList()
    {
        var rows = _events.Select(e => new CalendarEventRow(e, e.EventId == _selected?.EventId)).ToList();
        BindableLayout.SetItemsSource(EventList, rows);
        EventList.IsVisible = rows.Count > 0;
        EmptyEventsLabel.IsVisible = rows.Count == 0;
    }

    private async void OnShowPastToggled(object? sender, ToggledEventArgs e)
    {
        _showPast = e.Value;
        await Load();
    }

    // ----- New event -----

    private void OnToggleCreate(object? sender, EventArgs e)
    {
        _creating = !_creating;
        _selected = null;
        CreateSection.IsVisible = _creating;
        DetailSection.IsVisible = false;
        NoSelectionLabel.IsVisible = !_creating;
        RenderEventList();
    }

    private void OnCancelCreate(object? sender, EventArgs e)
    {
        _creating = false;
        CreateSection.IsVisible = false;
        NoSelectionLabel.IsVisible = _selected is null;
    }

    private void OnFormChanged(object? sender, EventArgs e) => UpdateCreateState();

    private void UpdateCreateState() =>
        CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(TitleEntry.Text);

    private void ResetCreateForm()
    {
        TitleEntry.Text = string.Empty;
        LocationEntry.Text = string.Empty;
        EventDatePicker.Date = DateTime.Today;
        EventTimePicker.Time = new TimeSpan(10, 0, 0);
        UpdateCreateState();
    }

    private async void OnCreate(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleEntry.Text)
            || EventDatePicker.Date is not { } date
            || EventTimePicker.Time is not { } time)
            return;

        var ev = await _state.CreateEventAsync(new CreateEventRequest
        {
            Title = TitleEntry.Text.Trim(),
            EventDate = AppState.Wall(date.Date + time),
            Location = LocationEntry.Text?.Trim() ?? string.Empty,
        });
        if (ev is null) return;

        _creating = false;
        CreateSection.IsVisible = false;
        ResetCreateForm();

        await Load();
        await Select(ev.EventId);
    }

    // ----- Selection / detail -----

    private async void OnSelectEvent(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not CalendarEventRow row) return;
        await Select(row.Dto.EventId);
    }

    private async Task Select(int eventId)
    {
        _creating = false;
        CreateSection.IsVisible = false;

        _selected = await _state.GetEventAsync(eventId);
        _attendees = _selected is null ? [] : await _state.GetEventAttendeesAsync(eventId) ?? [];

        RenderEventList();
        RenderDetail();
    }

    private void RenderDetail()
    {
        NoSelectionLabel.IsVisible = _selected is null && !_creating;
        DetailSection.IsVisible = _selected is not null;
        if (_selected is not { } ev) return;

        SelectedTitleLabel.Text = ev.Title;
        SelectedMetaLabel.Text = $"{ev.EventDate:dddd d MMMM yyyy, h:mm tt}"
            + (string.IsNullOrEmpty(ev.Location) ? string.Empty : $" \u2022 {ev.Location}")
            + $" \u2022 organised by {_state.UserName(ev.OrganiserId)}";

        var me = _attendees.FirstOrDefault(a => a.UserId == MyId);
        MyAnswerSection.IsVisible = me is not null;
        if (me is not null)
        {
            MyAnswerLabel.Text = $"Your answer: {me.Status}";
            AcceptButton.IsEnabled = me.Status != "Accepted";
            DeclineButton.IsEnabled = me.Status != "Declined";
        }

        AttendeesHeaderLabel.Text = $"Attendees ({_attendees.Count})";
        var attendeeRows = _attendees
            .Select(a => new AttendeeRow(a, CanManage || a.UserId == MyId, a.UserId == MyId))
            .ToList();
        BindableLayout.SetItemsSource(AttendeeList, attendeeRows);

        InviteSection.IsVisible = CanManage;
        if (CanManage)
        {
            var invitable = _state.Users
                .Where(u => u.IsActive && _attendees.All(a => a.UserId != u.UserId))
                .OrderBy(u => u.FullName)
                .ToList();
            InviteePicker.ItemsSource = invitable;
            InviteePicker.SelectedItem = null;
            InviteButton.IsEnabled = false;
        }
    }

    private async void OnRespond(object? sender, EventArgs e)
    {
        if (_selected is null) return;
        var status = ReferenceEquals(sender, AcceptButton) ? "Accepted" : "Declined";
        if (await _state.RespondToEventAsync(_selected.EventId, status))
            await Select(_selected.EventId);
    }

    private void OnInviteeChanged(object? sender, EventArgs e) =>
        InviteButton.IsEnabled = InviteePicker.SelectedItem is UserSummaryDto;

    private async void OnInvite(object? sender, EventArgs e)
    {
        if (_selected is null || InviteePicker.SelectedItem is not UserSummaryDto user) return;
        if (await _state.InviteToEventAsync(_selected.EventId, user.UserId))
            await Select(_selected.EventId);
    }

    private async void OnRemoveAttendee(object? sender, EventArgs e)
    {
        if (_selected is null || (sender as Element)?.BindingContext is not AttendeeRow row) return;
        if (await _state.RemoveEventAttendeeAsync(_selected.EventId, row.Dto.UserId))
            await Select(_selected.EventId);
    }
}
