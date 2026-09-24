using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using QULOOBUL__MOE_MIENEEN_Shared.Reports;

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
            try { await Launcher.Default.OpenAsync(new Uri(file.DownloadUrl)); }
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

        // Trend: thin to a legible number of points, always keeping the first and last.
        var trendPoints = Thin(r.Trend, 12);
        var maxCumulative = r.Trend.Count == 0 ? 0 : r.Trend.Max(p => p.CumulativeCompleted);
        var trendRows = trendPoints.Select(p => new TrendRow(p, maxCumulative)).ToList();
        BindableLayout.SetItemsSource(TrendList, trendRows);
        TrendList.IsVisible = trendRows.Count > 0;
        TrendEmptyLabel.IsVisible = trendRows.Count == 0;

        var completionRows = r.Completion.Select(c => new CompletionRow(c)).ToList();
        BindableLayout.SetItemsSource(CompletionList, completionRows);
        CompletionList.IsVisible = completionRows.Count > 0;
        CompletionEmptyLabel.IsVisible = completionRows.Count == 0;

        // Busiest first, capped to 8 - same as the web client's chart.
        var workloadRows = r.Workload
            .OrderByDescending(w => w.Assigned)
            .Take(8)
            .Select(w => new WorkloadRow(w))
            .ToList();
        BindableLayout.SetItemsSource(WorkloadList, workloadRows);
        WorkloadList.IsVisible = workloadRows.Count > 0;
        WorkloadEmptyLabel.IsVisible = workloadRows.Count == 0;

        var progressRows = r.Progress.Select(p => new ProgressRow(p)).ToList();
        BindableLayout.SetItemsSource(ProgressList, progressRows);
        ProgressList.IsVisible = progressRows.Count > 0;
        ProgressEmptyLabel.IsVisible = progressRows.Count == 0;
    }

    /// <summary>Evenly-spaced subset of at most <paramref name="max"/> items, keeping the first and last.</summary>
    private static List<T> Thin<T>(IReadOnlyList<T> items, int max)
    {
        if (items.Count <= max) return items.ToList();
        var step = (double)(items.Count - 1) / (max - 1);
        return Enumerable.Range(0, max)
            .Select(i => items[(int)Math.Round(i * step)])
            .Distinct()
            .ToList();
    }
}
