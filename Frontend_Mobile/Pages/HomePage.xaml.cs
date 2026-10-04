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
    }

    // Home is a view over AppState.Tasks loaded for today only. The API pushes
    // "boardChanged" after any change, so Changed re-renders while we're on screen.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _state.Changed -= Refresh; // a returning modal can re-run OnAppearing
        _state.Changed += Refresh;
        Refresh();
        await _state.LoadTasksAsync(DateTime.Today, DateTime.Today.AddDays(1));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _state.Changed -= Refresh;
    }

    private void Refresh()
    {
        var tasks = _state.Tasks
            .Where(t => t.Deadline.Date == DateTime.Today)
            .OrderBy(t => t.Deadline)
            .Select(_state.ToItem)
            .ToList();

        BindableLayout.SetItemsSource(TaskList, tasks);
        CompletedChip.Text = $"{tasks.Count(t => t.IsDone)} of {tasks.Count} completed today";

        var completed = tasks.Where(t => t.ShowCompletedBy).ToList();
        BindableLayout.SetItemsSource(CompletedList, completed);
        CompletedList.IsVisible = completed.Count > 0;
        NothingCompleted.IsVisible = completed.Count == 0;
    }

    /// <summary>
    /// Ticking completes the task as the signed-in user (the API records who);
    /// unticking reopens it. The reload afterwards puts the box back if the API refused.
    /// </summary>
    private async void OnDailyTaskToggled(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox box || box.BindingContext is not TaskItem task) return;
        if (e.Value == task.IsDone) return;
        await _state.ToggleTaskFromCheckBoxAsync(box, task.Card, e.Value);
    }
}
