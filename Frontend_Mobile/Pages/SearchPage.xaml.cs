using Frontend_Mobile.Data;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages;

/// <summary>Global search - the mobile equivalent of Search.razor.</summary>
public partial class SearchPage : ContentPage
{
    private readonly AppState _state = AppState.Current;
    private CancellationTokenSource? _debounce;
    // A slow earlier search must not overwrite the results of a later one.
    private int _version;

    public SearchPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SearchEntry.Focus();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        // 300ms debounce, matching Search.razor's DebounceInterval.
        _debounce?.Cancel();
        var cts = new CancellationTokenSource();
        _debounce = cts;
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); }
        catch (TaskCanceledException) { return; }
        if (!token.IsCancellationRequested) await Search();
    }

    private async Task Search()
    {
        var q = SearchEntry.Text?.Trim() ?? "";
        var mine = ++_version;
        if (q.Length < 2)
        {
            Show([], "Results appear as you type.");
            return;
        }

        Busy.IsRunning = Busy.IsVisible = true;
        var hits = await _state.SearchAsync(q) ?? [];
        if (mine != _version) return;
        Busy.IsRunning = Busy.IsVisible = false;

        var rows = hits.Where(h => _state.LinkTo(h.Kind, h.Id) is not null).Select(h => new SearchHit(h)).ToList();
        Show(rows, rows.Count == 0 ? $"Nothing matches \"{q}\"." : null);
    }

    private void Show(List<SearchHit> rows, string? status)
    {
        Busy.IsRunning = Busy.IsVisible = false;
        BindableLayout.SetItemsSource(ResultList, rows);
        StatusLabel.Text = status;
        StatusLabel.IsVisible = status is not null;
    }

    private async void OnHitTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not SearchHit hit) return;
        if (_state.LinkTo(hit.Dto.Kind, hit.Dto.Id) is { } route) await Shell.Current.GoToAsync(route);
    }
}
