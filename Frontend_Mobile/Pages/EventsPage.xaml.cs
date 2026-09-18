using System.Collections.ObjectModel;
using Frontend_Mobile.Data;
using Frontend_Mobile.Models;

namespace Frontend_Mobile.Pages;

public partial class EventsPage : ContentPage
{
    private enum FormMode { None, Create, Edit }

    private readonly AppState _state = AppState.Current;
    private readonly ObservableCollection<string> _formResources = [];
    private readonly List<StaffPick> _staffPicks;

    private EventModel? _selected;
    private FormMode _mode = FormMode.None;

    public EventsPage()
    {
        InitializeComponent();

        _staffPicks = _state.StaffMembers.Select(name => new StaffPick { Name = name }).ToList();
        BindableLayout.SetItemsSource(StaffPickList, _staffPicks);
        BindableLayout.SetItemsSource(FormResourceTags, _formResources);
        CoordinatorPicker.ItemsSource = (System.Collections.IList)_state.StaffMembers;

        _formResources.CollectionChanged += (_, _) =>
            NoFormResourcesLabel.IsVisible = _formResources.Count == 0;

        Refresh();
    }

    private void Refresh()
    {
        var roster = _state.PlannedEvents.OrderBy(e => e.Date).ToList();
        BindableLayout.SetItemsSource(RosterList, roster);
        RosterList.IsVisible = roster.Count > 0;
        NoEventsLabel.IsVisible = roster.Count == 0;

        FormPanel.IsVisible = _mode != FormMode.None;
        DetailPanel.IsVisible = _mode == FormMode.None && _selected is not null;
        EmptyPanel.IsVisible = _mode == FormMode.None && _selected is null;

        if (DetailPanel.IsVisible) ShowDetail(_selected!);
    }

    private void ShowDetail(EventModel evt)
    {
        DetailTitle.Text = evt.Title;
        DetailDate.Text = $"\U0001F4C5 {evt.DateTimeLabel}";
        DetailLocation.Text = $"\U0001F4CD {evt.LocationLabel}";
        DetailCoordinator.Text = $"\U0001F464 {evt.CoordinatorLabel}";

        DescriptionSection.IsVisible = evt.HasDescription;
        DetailDescription.Text = evt.Description;

        DetailBudget.Text = evt.BudgetLabel;
        DetailCapacity.Text = evt.CapacityLabel;

        StaffSectionLabel.Text = evt.StaffSectionLabel;
        BindableLayout.SetItemsSource(StaffTags, evt.StaffMembers);
        StaffTags.IsVisible = evt.HasStaff;
        NoStaffLabel.IsVisible = !evt.HasStaff;

        BindableLayout.SetItemsSource(ResourceTags, evt.RequiredResources);
        ResourceTags.IsVisible = evt.HasResources;
        NoResourcesLabel.IsVisible = !evt.HasResources;
    }

    private void OnEventSelected(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not EventModel evt) return;
        _selected = evt;
        _mode = FormMode.None;
        Refresh();
    }

    private void OnNewEvent(object? sender, EventArgs e)
    {
        _selected = null;
        LoadForm(null);
        _mode = FormMode.Create;
        Refresh();
    }

    private void OnEditEvent(object? sender, EventArgs e)
    {
        if (_selected is null) return;
        LoadForm(_selected);
        _mode = FormMode.Edit;
        Refresh();
    }

    /// <summary>
    /// Fills the form from an existing event, or clears it for a new one. The
    /// roster entry isn't touched until Save, which is the job Clone/CopyFrom
    /// did in the Blazor original.
    /// </summary>
    private void LoadForm(EventModel? evt)
    {
        FormHeading.Text = evt is null ? "Create New Event" : "Edit Event";
        SaveButton.Text = evt is null ? "Schedule Event" : "Save Changes";

        TitleEntry.Text = evt?.Title ?? string.Empty;
        DescriptionEditor.Text = evt?.Description ?? string.Empty;
        LocationEntry.Text = evt?.Location ?? string.Empty;
        DateField.Date = evt?.Date ?? DateTime.Today;
        TimeField.Time = evt?.Time ?? new TimeSpan(9, 0, 0);
        CoordinatorPicker.SelectedItem = string.IsNullOrWhiteSpace(evt?.Coordinator) ? null : evt!.Coordinator;
        BudgetEntry.Text = evt?.EstimatedBudget?.ToString("0.##") ?? string.Empty;
        CapacityEntry.Text = evt?.EstimatedCapacity?.ToString() ?? string.Empty;

        var assigned = evt?.StaffMembers ?? [];
        foreach (var pick in _staffPicks) pick.IsSelected = assigned.Contains(pick.Name);

        _formResources.Clear();
        foreach (var resource in evt?.RequiredResources ?? []) _formResources.Add(resource);
        NoFormResourcesLabel.IsVisible = _formResources.Count == 0;
        ResourceEntry.Text = string.Empty;

        UpdateSaveState();
    }

    private void OnFormChanged(object? sender, EventArgs e) => UpdateSaveState();

    private void UpdateSaveState() => SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(TitleEntry.Text);

    private void OnAddResource(object? sender, EventArgs e)
    {
        var value = ResourceEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(value)
            && !_formResources.Any(r => r.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            _formResources.Add(value);
        }
        ResourceEntry.Text = string.Empty;
    }

    private void OnRemoveResource(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is string resource) _formResources.Remove(resource);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var edited = new EventModel
        {
            Title = TitleEntry.Text?.Trim() ?? string.Empty,
            Description = DescriptionEditor.Text ?? string.Empty,
            Location = LocationEntry.Text ?? string.Empty,
            Date = DateField.Date,
            Time = TimeField.Time,
            Coordinator = CoordinatorPicker.SelectedItem as string ?? string.Empty,
            EstimatedBudget = double.TryParse(BudgetEntry.Text, out var budget) ? budget : null,
            EstimatedCapacity = int.TryParse(CapacityEntry.Text, out var capacity) ? capacity : null,
            StaffMembers = _staffPicks.Where(p => p.IsSelected).Select(p => p.Name).ToList(),
            RequiredResources = [.. _formResources]
        };

        if (_mode == FormMode.Create)
        {
            _state.AddEvent(edited);
            _selected = edited;
        }
        else if (_selected is not null)
        {
            _state.UpdateEvent(_selected, edited);
        }

        _mode = FormMode.None;
        Refresh();
    }

    private void OnCancelForm(object? sender, EventArgs e)
    {
        _mode = FormMode.None;
        Refresh();
    }
}
