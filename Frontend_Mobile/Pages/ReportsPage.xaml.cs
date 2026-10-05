using Frontend_Mobile.Controls;
using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using QULOOBUL__MOE_MIENEEN_Shared.Reports;
// Frontend_Mobile.Models.Colors and Microsoft.Maui.Graphics.Colors (a project-wide global
// using) share a name - same ambiguity as the Permissions fix elsewhere. Alias instead of
// hitting the same compile error here once real color references show up below.
using AppColors = Frontend_Mobile.Models.Colors;

namespace Frontend_Mobile.Pages;

/// <summary>UC-09 admin reporting dashboard - the mobile equivalent of Reports.razor.</summary>
public partial class ReportsPage : ContentPage
{
    private readonly AppState _state = AppState.Current;
    private bool _busy;

    public ReportsPage()
    {
        InitializeComponent();

        ReportTypePicker.ItemsSource = (System.Collections.IList)ReportEnums.Types;
        ReportTypePicker.ItemDisplayBinding = new Binding(nameof(Option<ReportType>.Label));
        ReportTypePicker.SelectedIndex = 0;

        FromPicker.Date = DateTime.Today.AddDays(-30);
        ToPicker.Date = DateTime.Today;

        UpdateFormState();
    }

    private ReportType SelectedType => (ReportTypePicker.SelectedItem as Option<ReportType>)?.Value ?? ReportType.TaskCompletion;

    private bool Valid => FromPicker.Date is not null && ToPicker.Date is not null && ToPicker.Date >= FromPicker.Date;

    // The end date is inclusive: a report "to 23 Sep" covers all of that day - same rule as the web client.
    private ReportRequest Request => new()
    {
        PeriodStart = AppState.Wall(FromPicker.Date!.Value.Date),
        PeriodEnd = AppState.Wall(ToPicker.Date!.Value.Date.AddDays(1).AddTicks(-1)),
        ReportType = SelectedType.ToString(),
    };

    private void OnFormChanged(object? sender, EventArgs e) => UpdateFormState();

    private void OnDateChanged(object? sender, DateChangedEventArgs e) => UpdateFormState();

    private void UpdateFormState()
    {
        ValidationLabel.IsVisible = !Valid;
        RunButton.IsEnabled = !_busy && Valid;
        ExportButton.IsEnabled = !_busy && Valid;
    }

    private async void OnRun(object? sender, EventArgs e)
    {
        if (!Valid) return;
        await SetBusy(true);

        var report = await _state.BuildReportAsync(Request);
        if (report is not null) BindReport(report);

        await SetBusy(false);
    }

    private async void OnExport(object? sender, EventArgs e)
    {
        if (!Valid) return;
        await SetBusy(true);

        if (await _state.ExportReportAsync(Request) is { } file)
        {
            try { await Launcher.Default.OpenAsync(AppState.ForThisDevice(new Uri(file.DownloadUrl))); }
            catch (Exception) { await DisplayAlertAsync("Export ready", "The download link couldn't be opened automatically.", "OK"); }
        }

        await SetBusy(false);
    }

    private async Task SetBusy(bool busy)
    {
        _busy = busy;
        Busy.IsVisible = busy;
        Busy.IsRunning = busy;
        UpdateFormState();
        await Task.CompletedTask;
    }

    private void BindReport(ReportDto r)
    {
        EmptyLabel.IsVisible = false;
        ResultsSection.IsVisible = true;

        // Full resolution, not thinned: the line itself stays accurate, LineChartDrawable
        // only thins which x-axis labels it prints, so nothing is lost by passing it all.
        var trendLabels = r.Trend.Select(p => p.BucketStart.ToString("d MMM")).ToList();
        var trendValues = r.Trend.Select(p => (double)p.CumulativeCompleted).ToList();
        TrendChart.Drawable = new LineChartDrawable
        {
            Labels = trendLabels,
            Values = trendValues,
            LineColor = AppColors.AccentBlue,
        };
        TrendChart.Invalidate();
        TrendChart.IsVisible = r.Trend.Count > 0;
        TrendEmptyLabel.IsVisible = r.Trend.Count == 0;

        CompletionChart.Drawable = new StackedBarChartDrawable
        {
            Categories = r.Completion.Select(c => c.Bucket).ToList(),
            Series =
            [
                ("Completed", AppColors.StatusApproved, r.Completion.Select(c => (double)c.Completed).ToList()),
                ("Outstanding", AppColors.StatusPending, r.Completion.Select(c => (double)c.Outstanding).ToList()),
            ],
        };
        CompletionChart.Invalidate();
        CompletionChart.IsVisible = r.Completion.Count > 0;
        CompletionEmptyLabel.IsVisible = r.Completion.Count == 0;

        // Busiest first, capped to 8 - same as the web client's chart.
        var busiest = r.Workload.OrderByDescending(w => w.Assigned).Take(8).ToList();
        WorkloadChart.Drawable = new GroupedBarChartDrawable
        {
            // First names only: full names would overlap at 8 side-by-side groups on a phone width.
            Categories = busiest.Select(w => w.FullName.Split(' ')[0]).ToList(),
            Series =
            [
                ("Assigned", AppColors.AccentBlue, busiest.Select(w => (double)w.Assigned).ToList()),
                ("Completed", AppColors.StatusApproved, busiest.Select(w => (double)w.Completed).ToList()),
                ("Overdue", AppColors.StatusDenied, busiest.Select(w => (double)w.Overdue).ToList()),
            ],
        };
        WorkloadChart.Invalidate();
        WorkloadChart.IsVisible = busiest.Count > 0;
        WorkloadEmptyLabel.IsVisible = busiest.Count == 0;

        var progressRows = r.Progress.Select(p => new ProgressRow(p)).ToList();
        BindableLayout.SetItemsSource(ProgressList, progressRows);
        ProgressList.IsVisible = progressRows.Count > 0;
        ProgressEmptyLabel.IsVisible = progressRows.Count == 0;
    }
}
