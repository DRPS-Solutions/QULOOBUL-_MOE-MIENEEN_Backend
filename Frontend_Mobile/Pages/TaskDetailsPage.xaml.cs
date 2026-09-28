using Frontend_Mobile.Data;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Frontend_Mobile.Pages
{
    public partial class TaskDetailsPage : ContentPage
    {
        private readonly AppState _state = AppState.Current;
        private readonly TaskCardDto _task;

        private TaskDetailsPage(TaskCardDto task)
        {
            InitializeComponent();
            _task = task;
            TitleLabel.Text = task.Title;
            MetaLabel.Text = $"{task.Status} - {task.Priority} priority - " +
                $"Due {task.Deadline:dd MMM yyyy, h:mm tt}";
            DescriptionLabel.Text = task.Description;
            DescriptionLabel.IsVisible = !string.IsNullOrWhiteSpace(task.Description);
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

        private void OnCommentTextChanged(object? sender, TappedEventArgs e)
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
