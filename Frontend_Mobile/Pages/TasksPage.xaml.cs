using Frontend_Mobile.Data;
using Frontend_Mobile.Models;

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

public partial class TasksPage : ContentPage
{
    private const int StartHour = 6;
    private const int EndHour = 20;

    private readonly AppState _state = AppState.Current;
    private DateTime _selectedDate = DateTime.Today;
    private DateTime _weekStart = StartOfWeek(DateTime.Today);

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

    private void Refresh()
    {
        MonthLabel.Text = _weekStart.ToString("MMMM yyyy");
        SelectedDayLabel.Text = _selectedDate.ToString("dddd, d MMMM");

        BindableLayout.SetItemsSource(DayStrip, Enumerable.Range(0, 7)
            .Select(i => _weekStart.AddDays(i))
            .Select(day => new DayCell
            {
                Date = day,
                IsSelected = day == _selectedDate,
                IsToday = day == DateTime.Today,
                HasTasks = _state.Tasks.Any(t => t.ScheduledStart.Date == day)
            })
            .ToList());

        var forDay = _state.Tasks
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

    private void OnPreviousWeek(object? sender, EventArgs e)
    {
        _weekStart = _weekStart.AddDays(-7);
        Refresh();
    }

    private void OnNextWeek(object? sender, EventArgs e)
    {
        _weekStart = _weekStart.AddDays(7);
        Refresh();
    }

    private void OnDaySelected(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not DayCell cell) return;
        _selectedDate = cell.Date;
        Refresh();
    }

    /// <summary>Same attribute-then-complete flow as the Home checklist.</summary>
    private async void OnTaskToggled(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox box || box.BindingContext is not TaskItem task) return;
        if (e.Value == task.IsDone) return;

        if (!e.Value)
        {
            _state.SetTaskCompletion(task, false, null);
            Refresh();
            return;
        }

        var employee = await DisplayActionSheetAsync($"Completed by — {task.Title}", "Cancel", null, [.. _state.StaffMembers]);
        if (string.IsNullOrEmpty(employee) || employee == "Cancel")
        {
            box.IsChecked = false;
            return;
        }

        _state.SetTaskCompletion(task, true, employee);
        Refresh();
    }

    private async void OnNewTask(object? sender, EventArgs e)
    {
        var result = await CreateTaskPage.ShowAsync(Navigation);
        if (result is null) return;

        var task = _state.AddTask(result.Title, result.Date, result.Time, result.IsAllDay, result.Category, result.Priority);

        // Jump to whatever day the new task landed on, same as the Compose port.
        _selectedDate = task.ScheduledStart.Date;
        _weekStart = StartOfWeek(_selectedDate);
        Refresh();
    }
}
