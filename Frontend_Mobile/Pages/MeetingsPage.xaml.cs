using System;
using System.Collections.Generic;
using System.Text;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;
using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace Frontend_Mobile.Pages
{
    public partial class MeetingsPage : ContentPage
    {
        private readonly AppState _state = AppState.Current;
        private List<MeetingDto> _meetings = [];

        private MeetingForm? _active;
        private string _originalMinutes = string.Empty;
        private bool _saving;

        public MeetingsPage()
        {
            InitializeComponent();
            _ = LoadMeetingsAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadMeetingsAsync();
        }

        private async Task LoadMeetingsAsync()
        {
            ListLoadingSpinner.IsVisible = true;
            ListLoadingSpinner.IsRunning = true;
            _meetings = await _state.GetAsync<List<MeetingDto>>("api/meetings") ?? [];
            ListLoadingSpinner.IsRunning = false;
            ListLoadingSpinner.IsVisible = false;

            var rows = _meetings.Select(meeting => new MeetingRow(meeting, _state.UserName(meeting.OrganiserId))).ToList();
            BindableLayout.SetItemsSource(MeetingList, rows);
            EmptyMeetingsLabel.IsVisible = rows.Count == 0;
        }

        private void OnNewMeeting(object? sender, EventArgs e) => Open(new MeetingForm());

        private void OnMeetingTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as Element)?.BindingContext is not MeetingRow meetingRow) return;
            OpenFrom(meetingRow.MeetingDto);
        }

        private void OpenFrom(MeetingDto meeting) => OpenFrom(new MeetingForm
        {
            MeetingId = meeting.MeetingId,
            Title = meeting.Title,
            MinutesText = meeting.Minutes?.MinutesText ?? string.Empty,
            OrganiserId = meeting.OrganiserId,
            Minutes = meeting.Minutes
        });

        private void Open(MeetingForm meeting)
        {
            _active = meeting;
            _originalMinutes = meeting.MinutesText;

            MeetingTitleEntry.Text = meeting.Title;
            MeetingTitleEntry.IsEnabled = meeting.MeetingId is null;
            MinutesEditor.Text = meeting.MinutesText;

            RefreshDetailState();
            ListSection.IsVisible = false;
            DetailSection.IsVisible = true;
        }

        private void OnBackToList(object? sender, EventArgs e)
        {
            _active = null;
            ListSection.IsVisible = true;
            DetailSection.IsVisible = false;
            _ = LoadMeetingsAsync();
        }

        private void OnDetailChanged(object? sender, EventArgs e) => RefreshDetailState();

        private void RefreshDetailState()
        {
            if (_active is null) return;

            _active.Title = MeetingTitleEntry.Text ?? string.Empty;
            _active.MinutesText = MinutesEditor.Text ?? string.Empty;

            var changed = _active.MinutesText.Trim().Length > 0 && _active.MinutesText != _originalMinutes;
            SaveButton.IsEnabled = !_saving && (_active.MeetingId is null || changed);

            var canApprove = _active.Minutes is not null
                && _active.MinutesText == _originalMinutes
                && (_active.OrganiserId == _state.CurrentUser?.UserId
                    || _state.CurrentUser?.Permissions.Contains(Permissions.ManageUsers) == true);
            ApproveButton.IsVisible = canApprove;

            ApprovedLabel.IsVisible = _active.Minutes?.ApprovedBy is not null;
            ApprovedLabel.Text = _active.Minutes?.ApprovedBy is { } approver ? $"Approved by {_state.UserName(approver)}" : string.Empty;
        }

        private async void OnApprove(object? sender, EventArgs e)
        {
            if (_active?.Minutes is not { } minutes) return;
            
            if (await _state.SendAsync<MeetingMinutesDto>(HttpMethod.Post,
                $"api/meetings/minutes/{minutes.MinutesId}/approve", new { }) is { } approved)
            {
                _active.Minutes = approved;
                RefreshDetailState();
            }
        }

        private async void OnSaveMeeting(object? sender, EventArgs e)
        {
            if (_active is null) return;

            var meeting = _active;
            var minutes = meeting.MinutesText.Trim();

            _saving = true;
            SaveButton.IsEnabled = false;

            if (meeting.MeetingId is null)
            {
                meeting.MeetingId = await _state.SendAsync<int?>(HttpMethod.Post, "api/meetings", new CreateMeetingRequest
                {
                    Title = string.IsNullOrWhiteSpace(meeting.Title) ? "Untitled meeting" : meeting.Title.Trim(),
                    MeetingDate = AppState.Wall(DateTime.Now)
                });
            }

            var saved = meeting.MeetingId is not null && (minutes.Length == 0
                || await _state.SendAsync(HttpMethod.Post, $"api/meetings/{meeting.MeetingId}/minutes", 
                    new RecordMinutesRequest { MinutesText = minutes }));

            _saving = false;
            
            if (saved)
            {
                OnBackToList(null, EventArgs.Empty);
            }
        }

        private async void OnSearch(object? sender, EventArgs e)
        {
            var q = SearchEntry.Text?.Trim();
            var url = string.IsNullOrWhiteSpace(q) ? "api/meetings/minutes" : $"api/meetings/minutes?q={Uri.EscapeDataString(q)}";
            var hits = await _state.GetAsync<List<MeetingMinutesDto>>(url) ?? [];

            var searchResults = hits.Select(hit => new MinutesSearch(hit, _meetings
                .FirstOrDefault(meeting => meeting.MeetingId == hit.MeetingId)?.Title)).ToList();

            BindableLayout.SetItemSource(SearchResultsList, searchResults);
            SearchEmtpyLabel.IsVisible = searchResults.Count == 0;
        }

        private sealed class MeetingForm
        {
            public int? MeetingId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string MinutesText { get; set; } = string.Empty;
            public int? OrganiserId { get; init; }
            public MeetingMinutesDto? Minutes { get; set; }
        }
    }
}
