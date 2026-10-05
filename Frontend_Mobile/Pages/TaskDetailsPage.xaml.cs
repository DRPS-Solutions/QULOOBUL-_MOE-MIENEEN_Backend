using Frontend_Mobile.Data;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages
{
    public partial class TaskDetailsPage : ContentPage
    {
        private readonly AppState _state = AppState.Current;
        private TaskCardDto _task;
        private bool _syncing; // set while code (not the user) moves the picker/slider

        private static readonly string[] Statuses = ["To Do", "In Progress", "Blocked", "Done"];

        private TaskDetailsPage(TaskCardDto task)
        {
            InitializeComponent();
            _task = task;
            TitleLabel.Text = task.Title;
            DescriptionLabel.Text = task.Description;
            DescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(task.Description);
            StatusPicker.ItemsSource = Statuses;
            ShowTask();
        }

        private void ShowTask()
        {
            _syncing = true;
            MetaLabel.Text = $"{_task.Status} - {_task.Priority} priority - " +
                $"Due {_task.Deadline:dd MMM yyyy, h:mm tt}";
            var status = string.IsNullOrWhiteSpace(_task.Status) ? "To Do" : _task.Status;
            StatusPicker.SelectedIndex = Array.FindIndex(Statuses, s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
            ProgressSection.IsVisible = status.Equals("In Progress", StringComparison.OrdinalIgnoreCase);
            ProgressSlider.Value = _task.PercentComplete;
            ProgressLabel.Text = $"Progress: {_task.PercentComplete}%";
            _syncing = false;
        }

        // After a save, pick up the server's copy (status, or 100% on Done). Read by id: a task opened
        // from a notification may sit outside the loaded task window.
        private async Task Reload()
        {
            if (await _state.GetAsync<TaskDetailDto>($"api/tasks/{_task.TaskId}") is { } d)
                _task = _task with { Status = d.Status, PercentComplete = d.PercentComplete };
            ShowTask();
        }

        private async void OnStatusChanged(object? sender, EventArgs e)
        {
            if (_syncing || StatusPicker.SelectedItem is not string status
                || status.Equals(_task.Status, StringComparison.OrdinalIgnoreCase)) return;
            StatusPicker.IsEnabled = false;
            await _state.MoveTaskAsync(_task, status); // a refusal shows the API's message
            StatusPicker.IsEnabled = true;
            await Reload();
        }

        private static int Snap(double value) => (int)Math.Round(value / 10) * 10;

        private void OnProgressSliding(object? sender, ValueChangedEventArgs e)
        {
            if (!_syncing) ProgressLabel.Text = $"Progress: {Snap(e.NewValue)}%";
        }

        private async void OnProgressReleased(object? sender, EventArgs e)
        {
            var percent = Snap(ProgressSlider.Value);
            if (percent == _task.PercentComplete) { ShowTask(); return; }
            await _state.SetTaskProgressAsync(_task, percent);
            await Reload();
        }

        /// <summary>Has the same pattern as CreateTaskPage.ShowAsync</summary>
        public static Task ShowAsync(INavigation navigation, TaskCardDto task)
        {
            return navigation.PushModalAsync(new TaskDetailsPage(task));
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCommentsAsync();
        }

        private async Task LoadCommentsAsync()
        {
            LoadingSpinner.IsVisible = true;
            LoadingSpinner.IsRunning = true;

            var detail = await _state.GetAsync<TaskDetailDto>($"api/tasks/{_task.TaskId}");
            var comments = detail?.Comments.Select(comment => new TaskCommentItem(
                comment, _state.UserName(comment.UserId))).ToList() ?? [];

            LoadingSpinner.IsVisible = false;
            LoadingSpinner.IsRunning = false;

            CommentsHeader.Text = $"Comments ({comments.Count})";
            EmptyCommentsLabel.IsVisible = comments.Count == 0;
            BindableLayout.SetItemsSource(CommentList, comments);
        }

        private void OnCommentTextChanged(object? sender, TextChangedEventArgs e)
        {
            PostButton.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);
        }

        private async void OnPostComment(object? sender, EventArgs e)
        {
            var text = CommentEntry.Text?.Trim();
            
            if (string.IsNullOrWhiteSpace(text)) return;

            PostButton.IsEnabled = false;
            
            if (await _state.SendAsync<TaskCommentDto>(HttpMethod.Post, $"api/tasks/{_task.TaskId}/comments",
                new AddTaskCommentRequest { Comment = text }) is not null)
            {
                CommentEntry.Text = string.Empty;
                await LoadCommentsAsync();
            }
            else
            {
                PostButton.IsEnabled = true;
            }
        }

        private async void OnClose(object? sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
