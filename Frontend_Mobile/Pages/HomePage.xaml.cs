using Frontend_Mobile.Data;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppState _state = AppState.Current;

    public HomePage()
    {
        InitializeComponent();
        TodayLabel.Text = DateTime.Today.ToString("dddd, dd MMMM yyyy");
        BindableLayout.SetItemsSource(TaskList, _state.TodayTasks);
        Refresh();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        var tasks = _state.TodayTasks;
        CompletedChip.Text = $"{tasks.Count(t => t.IsCompleted)} of {tasks.Count} completed today";

        var completed = tasks.Where(t => t.IsCompleted && !string.IsNullOrEmpty(t.CompletedBy)).ToList();
        BindableLayout.SetItemsSource(CompletedList, completed);
        CompletedList.IsVisible = completed.Count > 0;
        NothingCompleted.IsVisible = completed.Count == 0;
    }

    /// <summary>
    /// Ticking a task asks who completed it first (CompletedByDialog.razor);
    /// backing out of that prompt leaves the task untouched, so the checkbox
    /// is put back. Unticking clears the attribution outright.
    /// </summary>
    private async void OnDailyTaskToggled(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox box || box.BindingContext is not DailyTask task) return;

        // Also swallows the echo from reverting the box below.
        if (e.Value == task.IsCompleted) return;

        if (!e.Value)
        {
            _state.SetDailyTaskCompletion(task, false, null);
            Refresh();
            return;
        }

        var employee = await DisplayActionSheetAsync($"Completed by — {task.Title}", "Cancel", null, [.. _state.StaffMembers]);
        if (string.IsNullOrEmpty(employee) || employee == "Cancel")
        {
            box.IsChecked = false;
            return;
        }

        _state.SetDailyTaskCompletion(task, true, employee);
        Refresh();
    }
}
