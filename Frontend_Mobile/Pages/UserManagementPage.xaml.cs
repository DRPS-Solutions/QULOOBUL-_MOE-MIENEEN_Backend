using AndroidX.Core.Provider;
using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Frontend_Mobile.Pages
{
    public partial class UserManagementPage : ContentPage
    {
        private readonly AppState _state = AppState.Current;
        private List<StaffOverviewRow> _staffOverview = [];
        private UserSummaryDto? _selectedUser;

        public UserManagementPage()
        {
            InitializeComponent();
            RolePicker.ItemDisplayBinding = new Binding(nameof(RoleDto.RoleName));
            _ = LoadAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            RosterLoadingSpinner.IsVisible = true;
            RosterLoadingSpinner.IsRunning = true;

            var overviewTask = _state.GetAsync<List<StaffOverviewRow>>("api/users/overview");
            await Task.WhenAll(overviewTask, _state.RefreshUsersAsync());
            _staffOverview = await overviewTask ?? [];

            RosterLoadingSpinner.IsRunning = false;
            RosterLoadingSpinner.IsVisible = false;

            RenderRoster();

            // Keep current selected user in step with reload
            if (_selectedUser is not null && _state.Users.FirstOrDefault(user => 
                user.UserId == _selectedUser.UserId) is { } refreshed)
            {
                await SelectUserAsync(refreshed);
            }
        }

        private void RenderRoster()
        {
            var query = SearchEntry.Text?.Trim();
            var filtered = _state.Users.Where(user =>
                string.IsNullOrWhiteSpace(query) ||
                user.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                user.Role.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(user => user.FullName)
                .ToList();

            var rows = filtered.Select(user => new UserRow(user, WorkloadFor(user))).ToList();
            BindableLayout.SetItemsSource(RosterList, rows);
            EmptyRosterLabel.IsVisible = rows.Count == 0;
        }

        private string WorkloadFor(UserSummaryDto user) => !user.IsActive ? user.Status 
            : _staffOverview.FirstOrDefault(roster => roster.UserId == user.UserId) 
            is { } row ? $"{row.ActiveTasks} active - {row.OverdueTasks} overdue" 
            : string.Empty;

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => RenderRoster();

        private async void OnUserTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as Element)?.BindingContext is not UserRow userRow) return;
            await SelectUserAsync(userRow.Dto);
        }

        private async Task SelectUserAsync(UserSummaryDto user)
        {
            _selectedUser = user;
            DetailPanel.IsVisible = true;

            DetailNameLabel.Text = user.FullName;
            DetailRoleLabel.Text = user.Role;
            DetailEmailLabel.Text = user.Email;
            DetailStatusLabel.Text = user.IsActive ? "Active" : user.Status;
            DeactivateButton.IsVisible = user.IsActive;
            ReactivateButton.IsVisible = !user.IsActive;

            var tasks = await _state.GetAsync<List<TaskCardDto>>($"api/tasks?assigneeId={user.UserId}") ?? [];

            if (_selectedUser != user) return;

            var items = tasks.Select(_state.ToItem).ToList();
            var completed = items.Where(task => task.IsDone).ToList();
            var pending = items.Where(task => !task.IsDone).ToList();

            BindableLayout.SetItemsSource(CompletedTaskList, completed);
            BindableLayout.SetItemsSource(PendingTaskList, pending);
            EmptyCompletedLabel.IsVisible = completed.Count == 0;
            EmptyPendingLabel.IsVisible = pending.Count == 0;

            CompletedCountLabel.Text = completed.Count.ToString();
            PendingCountLabel.Text = pending.Count.ToString();
            CompletionRateLabel.Text = items.Count == 0 ? "0%" 
                : $"{Math.Round(completed.Count * 100.0 / items.Count)}%";
        }

        private async void OnDeactivate(object? sender, EventArgs e) => await RunAdminAction(
            "Deactivate account", $"Deactivate {_selectedUser?.FullName}? They will no longer be able to sign in",
            "deactivate");

        private async void OnReactivate(object? sender, EventArgs e) => await RunAdminAction(
            "Reactivate account", $"Reactivate {_selectedUser?.FullName}?", "reactivate");

        private async void OnResetPassword(object? sender, EventArgs e) => await RunAdminAction(
            "Reset Password", $"Send {_selectedUser?.FullName} a password reset?", "reset-password");

        private async void OnAnonymise(object? sender, EventArgs e) => await RunAdminAction(
            "Anonymise account", $"This permanently removes {_selectedUser?.FullName}'s personal details. This cannot be undone",
            "anonymise", destructive: true);

        private async Task RunAdminAction(string title, string message, string endpoint, bool destructive = false)
        {
            if (_selectedUser is not { } user) return;

            var confirmed = await DisplayAlertAsync(title, message, destructive ? "Anonymise" : "Confirm", "Cancel");

            if (!confirmed) return;

            if (await _state.SendAsync(HttpMethod.Post, $"api/users/{user.UserId}/{endpoint}"))
            {
                await LoadAsync();
            }
            else
            {
                await DisplayAlertAsync("Could not complete that", "The server did not accept that action. Please try again", "OK");
            }
        }

        private void OnNewUser(object? sender, EventArgs e)
        {
            FirstNameEntry.Text = string.Empty;
            SurnameEntry.Text = string.Empty;
            EmailEntry.Text = string.Empty;
            CellEntry.Text = string.Empty;
            RolePicker.SelectedItem = null;
            RefreshSaveState();

            if (RolePicker.ItemsSource is null)
            {
                _ = LoadRolesAsync();
            }

            RosterSection.IsVisible = false;
            CreateSection.IsVisible = true;
        }

        private async Task LoadRolesAsync()
        {
            var roles = await _state.GetAsync<List<RoleDto>>("api/roles") ?? [];
            RolePicker.ItemsSource = roles;
        }

        private void OnBackToRoster(object? sender, EventArgs e)
        {
            CreateSection.IsVisible = false;
            RosterSection.IsVisible = true;
        }

        private void OnFormChanged(object? sender, EventArgs e) => RefresheSaveState();

        private void RefreshSaveState() => SaveUserButton.IsEnabled =
            !string.IsNullOrWhiteSpace(FirstNameEntry.Text) &&
            !string.IsNullOrWhiteSpace(SurnameEntry.Text) &&
            !string.IsNullOrWhiteSpace(EmailEntry.Text) &&
            !string.IsNullOrWhiteSpace(CellEntry.Text) &&
            RolePicker.SelectedItem is not null;

        private async void OnSaveUser(object? sender, EventArgs e)
        {
            if (RolePicker.SelectedItem is not RoleDto role) return;

            SaveUserButton.IsEnabled = false;

            var created = await _state.SendAsync<UserDto>(HttpMethod.Post, "api/users", new CreateUserRequest
            {
                FirstName = FirstNameEntry.Text.Trim(),
                Surname = SurnameEntry.Text.Trim(),
                Email = EmailEntry.Text.Trim(),
                CellNo = CellEntry.Text.Trim(),
                RoleId = role.RoleId
            });

            if (created is null)
            {
                RefreshSaveState();
                await DisplayAlertAsync("Could not create user", "Check the details and try again", "OK");
                return;
            }

            OnBackToRoster(null, EventArgs.Empty);
            await LoadAsync();

            if (_state.Users.FirstOrDefault(user => user.UserId == created.UserId) is { } newUser)
            {
                await SelectUserAsync(newUser);
            }
        }
    }
}
