using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages;

/// <summary>What the form hands back, equivalent to CreateTaskDialog.kt's NewTaskResult.</summary>
public sealed record NewTaskResult(
    string Title,
    DateTime Date,
    TimeSpan? Time,
    bool IsAllDay,
    Priority Priority);

public partial class CreateTaskPage : ContentPage
{
    private readonly TaskCompletionSource<NewTaskResult?> _result = new();

    public CreateTaskPage()
    {
        InitializeComponent();
        PriorityPicker.ItemsSource = (System.Collections.IList)TaskEnums.Priorities;
        PriorityPicker.SelectedIndex = 0;
        DateField.Date = DateTime.Today;
        TimeField.Time = new TimeSpan(9, 0, 0);
    }

    /// <summary>Pushes the form and resolves once it's dismissed; null means cancelled.</summary>
    public static async Task<NewTaskResult?> ShowAsync(INavigation navigation)
    {
        var page = new CreateTaskPage();
        await navigation.PushModalAsync(page);
        return await page._result.Task;
    }

    private void OnTitleChanged(object? sender, TextChangedEventArgs e) =>
        CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);

    private void OnAllDayChanged(object? sender, CheckedChangedEventArgs e) =>
        TimeSection.IsVisible = !e.Value;

    private async void OnCancel(object? sender, EventArgs e) => await CloseAsync(null);

    private async void OnCreate(object? sender, EventArgs e)
    {
        var isAllDay = AllDayBox.IsChecked;
        await CloseAsync(new NewTaskResult(
            Title: TitleEntry.Text?.Trim() ?? string.Empty,
            Date: DateField.Date ?? DateTime.Today,
            Time: isAllDay ? null : TimeField.Time,
            IsAllDay: isAllDay,
            Priority: ((Option<Priority>)PriorityPicker.SelectedItem).Value));
    }

    // Also covers the hardware back button / swipe-away, which must resolve the
    // awaiting caller rather than leaving it hanging forever.
    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private async Task CloseAsync(NewTaskResult? result)
    {
        if (_result.Task.IsCompleted) return;
        await Navigation.PopModalAsync();
        _result.SetResult(result);
    }
}
