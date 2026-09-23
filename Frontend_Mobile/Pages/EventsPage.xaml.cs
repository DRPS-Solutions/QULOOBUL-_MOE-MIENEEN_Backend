using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;

namespace Frontend_Mobile.Pages;

/// <summary>
/// Event planning. As on the web client, an "event" is an API project: the
/// coordinator is its owner and the staff helping are its members.
/// </summary>
public partial class EventsPage : ContentPage
{
    private enum FormMode { None, Create, Edit }

    private readonly AppState _state = AppState.Current;
    private List<StaffPick> _staffPicks = [];

    // The project being viewed, with its members and linked tasks.
    private ProjectDto? _selected;
    private IReadOnlyList<ProjectMemberDto> _members = [];
    private IReadOnlyList<TaskCardDto> _linkedTasks = [];
    private FormMode _mode = FormMode.None;
    private bool _saving;

    public EventsPage()
    {
        InitializeComponent();
        CoordinatorPicker.ItemDisplayBinding = new Binding(nameof(UserSummaryDto.FullName));
        Refresh();
    }

    // A "projectAssigned" push reloads the project list; Changed re-renders the roster.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _state.Changed -= Refresh;
        _state.Changed += Refresh;
        Refresh();
        await _state.RefreshProjectsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _state.Changed -= Refresh;
    }

    private void Refresh()
    {
        var roster = _state.EventProjects
            .OrderBy(p => p.ProjectName)
            .Select(p => new EventRow(p.ProjectId, p.ProjectName,
                $"{p.Status} · {p.MemberCount} staff · {p.OpenTaskCount} open tasks"))
            .ToList();
        BindableLayout.SetItemsSource(RosterList, roster);
        RosterList.IsVisible = roster.Count > 0;
        NoEventsLabel.IsVisible = roster.Count == 0;

        FormPanel.IsVisible = _mode != FormMode.None;
        DetailPanel.IsVisible = _mode == FormMode.None && _selected is not null;
        EmptyPanel.IsVisible = _mode == FormMode.None && _selected is null;

        if (DetailPanel.IsVisible) ShowDetail(_selected!);
    }

    private void ShowDetail(ProjectDto project)
    {
        DetailTitle.Text = project.ProjectName;
        DetailStatus.Text = $"\U0001F4CC {project.Status}";
        DetailCoordinator.Text = $"\U0001F464 Lead: {_state.UserName(project.OwnerId)}";

        DescriptionSection.IsVisible = !string.IsNullOrWhiteSpace(project.Description);
        DetailDescription.Text = project.Description;

        var staff = _members.Select(m => m.FullName).ToList();
        StaffSectionLabel.Text = $"STAFF HELPING ({staff.Count})";
        BindableLayout.SetItemsSource(StaffTags, staff);
        StaffTags.IsVisible = staff.Count > 0;
        NoStaffLabel.IsVisible = staff.Count == 0;

        var tasks = _linkedTasks.Select(t => t.Title).ToList();
        BindableLayout.SetItemsSource(TaskTags, tasks);
        TaskTags.IsVisible = tasks.Count > 0;
        NoTasksLabel.IsVisible = tasks.Count == 0;
    }

    private async void OnEventSelected(object? sender, TappedEventArgs e)
    {
        if ((sender as Element)?.BindingContext is not EventRow row) return;
        _mode = FormMode.None;
        await SelectAsync(row.ProjectId);
    }

    private async Task SelectAsync(int projectId)
    {
        var project = _state.GetAsync<ProjectDto>($"api/projects/{projectId}");
        var staff = _state.GetAsync<List<ProjectMemberDto>>($"api/projects/{projectId}/members");
        var board = _state.GetAsync<List<TaskCardDto>>($"api/projects/{projectId}/board");
        await Task.WhenAll(project, staff, board);
        _selected = await project;
        _members = await staff ?? [];
        _linkedTasks = await board ?? [];
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
    /// Fills the form from the selected project, or clears it for a new one.
    /// Nothing reaches the server until Save, so Cancel just discards the form.
    /// </summary>
    private void LoadForm(ProjectDto? project)
    {
        FormHeading.Text = project is null ? "Create New Event" : "Edit Event";
        SaveButton.Text = project is null ? "Schedule Event" : "Save Changes";

        TitleEntry.Text = project?.ProjectName ?? string.Empty;
        DescriptionEditor.Text = project?.Description ?? string.Empty;

        var people = _state.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
        CoordinatorPicker.ItemsSource = people;
        CoordinatorPicker.SelectedItem = people.FirstOrDefault(u => u.UserId == project?.OwnerId);

        HashSet<int> assigned = project is null ? [] : _members.Select(m => m.UserId).ToHashSet();
        _staffPicks = people
            .Select(u => new StaffPick { UserId = u.UserId, Name = u.FullName, IsSelected = assigned.Contains(u.UserId) })
            .ToList();
        BindableLayout.SetItemsSource(StaffPickList, _staffPicks);

        UpdateSaveState();
    }

    private void OnFormChanged(object? sender, EventArgs e) => UpdateSaveState();

    private void UpdateSaveState() =>
        SaveButton.IsEnabled = !_saving && !string.IsNullOrWhiteSpace(TitleEntry.Text);

    private async void OnSave(object? sender, EventArgs e)
    {
        _saving = true;
        UpdateSaveState();

        var name = TitleEntry.Text!.Trim();
        var description = string.IsNullOrWhiteSpace(DescriptionEditor.Text) ? null : DescriptionEditor.Text.Trim();
        var ownerId = (CoordinatorPicker.SelectedItem as UserSummaryDto)?.UserId;

        var project = _mode == FormMode.Create
            ? await _state.SendAsync<ProjectDto>(HttpMethod.Post, "api/projects", new CreateProjectRequest
            {
                ProjectName = name,
                Description = description,
                OwnerId = ownerId,
            })
            : await _state.SendAsync<ProjectDto>(HttpMethod.Patch, $"api/projects/{_selected!.ProjectId}", new UpdateProjectRequest
            {
                ProjectName = name,
                Description = description ?? "",
                OwnerId = ownerId,
            });

        if (project is not null)
        {
            // Staff are project memberships: add the newly ticked, remove the
            // unticked. The API notifies each person added.
            HashSet<int> current = _mode == FormMode.Create ? [] : _members.Select(m => m.UserId).ToHashSet();
            var picked = _staffPicks.Where(p => p.IsSelected).Select(p => p.UserId).ToHashSet();
            foreach (var userId in picked.Except(current))
                await _state.SendAsync(HttpMethod.Post, $"api/projects/{project.ProjectId}/members",
                    new AddProjectMemberRequest { UserId = userId });
            foreach (var userId in current.Except(picked))
                await _state.SendAsync(HttpMethod.Delete, $"api/projects/{project.ProjectId}/members/{userId}");

            _mode = FormMode.None;
            await _state.RefreshProjectsAsync();
            await SelectAsync(project.ProjectId);
        }

        _saving = false;
        UpdateSaveState();
    }

    private void OnCancelForm(object? sender, EventArgs e)
    {
        _mode = FormMode.None;
        Refresh();
    }
}
