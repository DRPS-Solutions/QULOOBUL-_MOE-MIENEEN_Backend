using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;

namespace Frontend_Mobile.Pages;

/// <summary>One cell in the week strip. Rebuilt whenever the week or selection moves.</summary>
public sealed class DayCell
{
    public required DateTime Date { get; init; }
    public required bool IsSelected { get; init; }
    public required bool IsToday { get; init; }
    public required bool HasTasks { get; init; }

    public string DayOfWeek => Date.ToString("ddd").ToUpperInvariant();
    public string DayNumber => Date.Day.ToString();
    public Color CircleColor => IsSelected ? Color.FromArgb("#2F5C4C") : Microsoft.Maui.Graphics.Colors.Transparent;
    public Color RingColor => IsToday && !IsSelected ? Microsoft.Maui.Graphics.Colors.White : Microsoft.Maui.Graphics.Colors.Transparent;
    public Color DotColor => IsSelected ? Microsoft.Maui.Graphics.Colors.White : Color.FromArgb("#2F5C4C");
}

/// <summary>One hour band of the timeline, with whatever is scheduled inside it.</summary>
public sealed class HourRow
{
    public required int Hour { get; init; }
    public required IReadOnlyList<TaskItem> Tasks { get; init; }

    public string Label => DateTime.Today.AddHours(Hour).ToString("h tt");
}

[QueryProperty(nameof(DeepLinkTaskId), "task")]
public partial class TasksPage : ContentPage
{
    private const int StartHour = 6;
    private const int EndHour = 20;

    private readonly AppState _state = AppState.Current;
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _weekStart = StartOfWeek(DateTime.Today);
    public string? DeepLinkTaskId { get; set; }
    private int? _openedDeepLinkId;

    public TasksPage()
    {
        InitializeComponent();
        Refresh();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)System.DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-diff);
    }

    // The week on screen is AppState's task window; a SignalR "boardChanged"
    // reloads it, and Changed re-renders while we're on screen.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _state.Changed -= Refresh; // a returning modal can re-run OnAppearing
        _state.Changed += Refresh;
        await LoadWeekAsync();
        await TryOpenDeepLinkAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _state.Changed -= Refresh;
    }

    private Task LoadWeekAsync()
    {
        Refresh();
        return _state.LoadTasksAsync(_weekStart, _weekStart.AddDays(7));
    }

    private async Task TryOpenDeepLinkAsync()
    {
        if (!int.TryParse(DeepLinkTaskId, out var id) || id == _openedDeepLinkId) return;

        _openedDeepLinkId = id;

        if (await _state.GetAsync<TaskDetailDto>($"api/tasks/{id}") is not { } detail) return;

        _selectedDate = detail.Deadline.Date;
        _weekStart = StartOfWeek(_selectedDate);
        await LoadWeekAsync();

        if (_state.Tasks.FirstOrDefault(task => task.TaskId == id) is { } card)
        {
            await TaskDetailsPage.ShowAsync(Navigation, card);
        }
    }

    private void Refresh()
    {
        MonthLabel.Text = _weekStart.ToString("MMMM yyyy");
        SelectedDayLabel.Text = _selectedDate.ToString("dddd, d MMMM");

        var tasks = _state.Tasks.Select(_state.ToItem).ToList();

        BindableLayout.SetItemsSource(DayStrip, Enumerable.Range(0, 7)
            .Select(i => _weekStart.AddDays(i))
            .Select(day => new DayCell
            {
                Date = day,
                IsSelected = day == _selectedDate,
                IsToday = day == DateTime.Today,
                HasTasks = tasks.Any(t => t.ScheduledStart.Date == day)
            })
            .ToList());

        var forDay = tasks
            .Where(t => t.ScheduledStart.Date == _selectedDate.Date)
            .OrderBy(t => t.ScheduledStart)
            .ToList();

        SummaryLabel.Text = forDay.Count == 0
            ? "No tasks scheduled"
            : $"{forDay.Count(t => t.IsDone)} of {forDay.Count} completed";

        var allDay = forDay.Where(t => t.IsAllDay).ToList();
        BindableLayout.SetItemsSource(AllDayList, allDay);
        AllDayBar.IsVisible = allDay.Count > 0;

        var timed = forDay.Where(t => !t.IsAllDay).ToList();
        BindableLayout.SetItemsSource(Timeline, Enumerable.Range(StartHour, EndHour - StartHour + 1)
            .Select(hour => new HourRow
            {
                Hour = hour,
                Tasks = timed.Where(t => t.ScheduledStart.Hour == hour).ToList()
            })
            .ToList());
    }

    private async void OnPreviousWeek(object? sender, EventArgs e)
    {
        _weekStart = _weekStart.AddDays(-7);
        await LoadWeekAsync();
    }

    private async void OnNextWeek(object? sender, EventArgs e)
    {
        _weekStart = _weekStart.AddDays(7);
        await LoadWeekAsync();
    }

    private void OnDaySelected(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not DayCell cell) return;
        _selectedDate = cell.Date;
        Refresh();
    }

    /// <summary>Same complete-as-me flow as the Home checklist.</summary>
    private async void OnTaskToggled(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox box || box.BindingContext is not TaskItem task) return;
        if (e.Value == task.IsDone) return;
        await _state.ToggleTaskFromCheckBoxAsync(box, task.Card, e.Value);
    }

    private async void OnTaskTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not TaskItem item) return;

        await TaskDetailsPage.ShowAsync(Navigation, item.Card);
    }

    private async void OnNewTask(object? sender, EventArgs e)
    {
        var result = await CreateTaskPage.ShowAsync(Navigation);
        if (result is null) return;

        var start = result.IsAllDay || result.Time is null
            ? result.Date.Date
            : result.Date.Date.Add(result.Time.Value);

        // Jump to whatever day the new task landed on, same as the Compose port.
        // The week loads first, so CreateTaskAsync's refresh reloads that same window.
        _selectedDate = start.Date;
        _weekStart = StartOfWeek(_selectedDate);
        await LoadWeekAsync();
        await _state.CreateTaskAsync(result.Title, start, result.Priority.ToString());
    }
}
