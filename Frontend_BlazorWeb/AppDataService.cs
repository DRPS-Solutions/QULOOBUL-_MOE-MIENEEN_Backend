// AppDataService.cs
//
// The web client's data layer. It signs in against the API, keeps the
// signed-in user plus the lists several pages share (staff directory,
// projects, the tasks of the date window on screen, resource requests,
// notifications), and listens on the API's SignalR hub so those lists
// refresh when something changes server-side. Every HTTP call goes
// through GetAsync/SendAsync, so failures look the same on every page
// (a snackbar) and an expired token signs the user out.
//
// Scoped - in Blazor WebAssembly that means one instance per browser
// tab. Pages subscribe to OnChange and call StateHasChanged.
//
// No namespace declared here on purpose - same convention as
// NewTaskResult.cs - so every page sees this without an extra @using.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using MudBlazor;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;
using QULOOBUL__MOE_MIENEEN_Shared.Requests;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

public class AppDataService(HttpClient http, ISnackbar snackbar, IDialogService dialogs,
    Microsoft.AspNetCore.Components.NavigationManager nav, IJSRuntime js)
{
    // sessionStorage, not localStorage: survives a reload but not closing the tab,
    // so a shared NPO computer doesn't stay signed in.
    private const string SessionKey = "session";
    public event Action? OnChange;
    private void Changed() => OnChange?.Invoke();

    public AuthResponse? CurrentUser { get; private set; }
    public IReadOnlyList<UserSummaryDto> Users { get; private set; } = [];
    public IReadOnlyList<ProjectSummaryDto> Projects { get; private set; } = [];
    public IReadOnlyList<TaskCardDto> Tasks { get; private set; } = [];
    public IReadOnlyList<ResourceRequestDto> Requests { get; private set; } = [];
    public IReadOnlyList<NotificationDto> Notifications { get; private set; } = [];

    private HubConnection? hub;

    // Tasks holds one date window at a time: whichever page is showing tasks asked for it.
    private string? taskWindowUrl;

    // ------------------------------------------------------------------
    // Session
    // ------------------------------------------------------------------

    /// <summary>Signs in (UC-01). Returns null on success, otherwise the message for the login form.</summary>
    public async Task<string?> LoginAsync(string email, string password)
    {
        HttpResponseMessage res;
        try
        {
            res = await http.PostAsJsonAsync("api/auth/login",
                new LoginRequest { Email = email.Trim(), Password = password });
        }
        catch (HttpRequestException)
        {
            return $"Can't reach the server at {http.BaseAddress}.";
        }
        if (!res.IsSuccessStatusCode) return await ErrorMessage(res);

        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        // A temporary password unlocks only api/users/me/password: MainLayout shows the change form,
        // and the session starts once ChangePasswordAsync succeeds.
        if (!auth.MustChangePassword) await StartSessionAsync();
        CurrentUser = auth;
        await SaveSessionAsync();
        Changed();
        return null;
    }

    /// <summary>Picks the session back up after a page reload, if the token hasn't expired.</summary>
    public async Task RestoreSessionAsync()
    {
        var json = await js.InvokeAsync<string?>("sessionStorage.getItem", SessionKey);
        var auth = json is null ? null : JsonSerializer.Deserialize<AuthResponse>(json);
        if (auth is null || auth.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await js.InvokeVoidAsync("sessionStorage.removeItem", SessionKey);
            return;
        }
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        if (!auth.MustChangePassword) await StartSessionAsync();
        CurrentUser = auth;
        Changed();
    }

    private Task SaveSessionAsync() =>
        js.InvokeVoidAsync("sessionStorage.setItem", SessionKey, JsonSerializer.Serialize(CurrentUser)).AsTask();

    private async Task StartSessionAsync()
    {
        // Load names/projects before the shell renders, so nothing shows as "User #3" first.
        await Task.WhenAll(RefreshUsersAsync(), RefreshProjectsAsync(), RefreshNotificationsAsync(),
            Refresh<RoleDto>("api/roles", l => roles = l));
        await ConnectHubAsync();
    }

    /// <summary>Replaces the signed-in user's password; after a temporary one, this starts the session.</summary>
    public async Task<bool> ChangePasswordAsync(string current, string next)
    {
        if (!await SendAsync(HttpMethod.Post, "api/users/me/password",
                new ChangePasswordRequest { CurrentPassword = current, NewPassword = next })) return false;
        if (CurrentUser is { MustChangePassword: true })
        {
            await StartSessionAsync();
            CurrentUser = CurrentUser with { MustChangePassword = false };
            await SaveSessionAsync();
            Changed();
        }
        return true;
    }

    public async Task LogoutAsync()
    {
        CurrentUser = null;
        http.DefaultRequestHeaders.Authorization = null;
        await js.InvokeVoidAsync("sessionStorage.removeItem", SessionKey);
        Users = [];
        Projects = [];
        Tasks = [];
        Requests = [];
        Notifications = [];
        taskWindowUrl = null;
        if (hub is not null)
        {
            await hub.DisposeAsync();
            hub = null;
        }
        // Don't leave the next account on a page its role can't see (e.g. /users).
        nav.NavigateTo("/");
        Changed();
    }

    /// <summary>SM-03 updateProfile on the signed-in user; keeps the navbar name and directory in step.</summary>
    public async Task<UserDto?> UpdateProfileAsync(UpdateUserRequest req)
    {
        var user = await SendAsync<UserDto>(HttpMethod.Patch, $"api/users/{CurrentUser!.UserId}", req);
        if (user is null) return null;
        CurrentUser = CurrentUser with { FullName = $"{user.FirstName} {user.Surname}" };
        await SaveSessionAsync();
        await RefreshUsersAsync(); // calls Changed()
        return user;
    }

    public string? Role => CurrentUser?.Role;

    public bool HasPermission(string permission) => CurrentUser?.Permissions.Contains(permission) == true;

    // Page visibility, per the spec's screen/role matrix. Home and
    // Notifications are open to everyone; roles not listed here (Staff,
    // Imaamat) get only those two.
    public bool CanSeeTasks => Role is Roles.FacilityManager or Roles.SecondaryAdmin or Roles.Admin;
    public bool CanSeeRequests => CanSeeTasks;
    public bool CanSeeMeetings => Role is Roles.SecondaryAdmin or Roles.Admin;
    public bool CanSeeEvents => CanSeeMeetings;
    public bool CanSeeUserManagement => Role is Roles.Admin;
    public bool CanSeeReports => HasPermission(Permissions.GenerateReports);

    /// <summary>Whether the signed-in role may open this base-relative path (e.g. "users?x=1").
    /// MainLayout enforces it, so a typed URL can't reach a page the menu hides.</summary>
    public bool CanSee(string relativePath) => relativePath.Split('/', '?', '#')[0].ToLowerInvariant() switch
    {
        "tasks" => CanSeeTasks,
        "requests" => CanSeeRequests,
        "meetings" => CanSeeMeetings,
        "events" => CanSeeEvents,
        "users" => CanSeeUserManagement,
        "reports" => CanSeeReports,
        _ => true,
    };

    /// <summary>The page that opens one record, by the entity type name the API uses on
    /// notifications and search hits; null when this role has no page for it.</summary>
    public string? LinkTo(string? entityType, int? id) => (entityType, id) switch
    {
        ("ProjectTask", { } t) when CanSeeTasks => $"tasks?task={t}",
        ("ResourceRequest", { } r) when CanSeeRequests => $"requests?id={r}",
        ("Project", { } p) when CanSeeEvents => $"events?project={p}",
        ("Event", { } e) => $"calendar?event={e}",
        ("Meeting", _) when CanSeeMeetings => "meetings",
        ("File", _) => "files",
        ("User", _) when CanSeeUserManagement => "users",
        _ => null
    };

    // ------------------------------------------------------------------
    // Shared lists and lookups
    // ------------------------------------------------------------------

    public Task RefreshUsersAsync() => Refresh<UserSummaryDto>("api/users", l => Users = l);
    public Task RefreshProjectsAsync() => Refresh<ProjectSummaryDto>("api/projects", l => Projects = l);
    public Task RefreshRequestsAsync() => Refresh<ResourceRequestDto>("api/requests", l => Requests = l);
    public Task RefreshNotificationsAsync() => Refresh<NotificationDto>("api/notifications", l => Notifications = l);

    public Task RefreshTasksAsync() =>
        taskWindowUrl is null ? Task.CompletedTask : Refresh<TaskCardDto>(taskWindowUrl, l => Tasks = l);

    /// <summary>Loads the tasks due in [from, to) into <see cref="Tasks"/>.</summary>
    public Task LoadTasksAsync(DateTime from, DateTime to)
    {
        taskWindowUrl = $"api/tasks?from={from:s}&to={to:s}";
        return RefreshTasksAsync();
    }

    private async Task Refresh<T>(string url, Action<List<T>> apply)
    {
        if (await GetAsync<List<T>>(url) is { } list)
        {
            apply(list);
            Changed();
        }
    }

    public string UserName(int? id) =>
        id is null ? "" : Users.FirstOrDefault(u => u.UserId == id)?.FullName ?? $"User #{id}";

    public string AssigneeNames(TaskCardDto task) => string.Join(", ", task.AssigneeIds.Select(id => UserName(id)));

    // Admins aren't line staff, and the API refuses deactivated assignees.
    public IEnumerable<UserSummaryDto> AssignableUsers =>
        Users.Where(u => u.IsActive && u.Role != Roles.Admin).OrderBy(u => u.FullName);

    // Daily tasks are delegated strictly down the hierarchy (a facility manager → staff); the API enforces it.
    private IReadOnlyList<RoleDto> roles = [];
    private int Level(string? role) => roles.FirstOrDefault(r => r.RoleName == role)?.HierarchyLevel ?? 0;
    public IEnumerable<UserSummaryDto> DailyAssignees =>
        Users.Where(u => u.IsActive && Level(u.Role) > Level(Role)).OrderBy(u => u.FullName);

    public int? DailyProjectId => Projects.FirstOrDefault(p => p.ProjectName == DailyTasksProject)?.ProjectId;

    // The UI's "events" are every project except the seeded daily-tasks one.
    public IEnumerable<ProjectSummaryDto> EventProjects =>
        Projects.Where(p => p.ProjectName != DailyTasksProject);

    public string? ProjectName(int? id) => Projects.FirstOrDefault(p => p.ProjectId == id)?.ProjectName;

    /// <summary>The event a task is linked to, or null for a daily task.</summary>
    public string? EventName(TaskCardDto task) => task.ProjectId == DailyProjectId ? null : ProjectName(task.ProjectId);

    public int UnreadCount => Notifications.Count(n => !n.IsRead);

    public async Task MarkReadAsync(IReadOnlyList<int> ids)
    {
        if (ids.Count > 0 && await SendAsync(HttpMethod.Post, "api/notifications/read",
                new MarkReadRequest { NotificationIds = ids }))
            await RefreshNotificationsAsync();
    }

    // ------------------------------------------------------------------
    // Tasks (Home and Daily Tasks both change them)
    // ------------------------------------------------------------------

    /// <summary>Returns true once the task exists, even if assigning it then failed (already reported).</summary>
    public async Task<bool> CreateTaskAsync(NewTaskResult task)
    {
        if ((task.ProjectId ?? DailyProjectId) is not { } projectId)
        {
            snackbar.Add($"The server has no \"{DailyTasksProject}\" project to hold daily tasks.", Severity.Error);
            return false;
        }
        var card = await SendAsync<TaskCardDto>(HttpMethod.Post, "api/tasks", new CreateTaskRequest
        {
            ProjectId = projectId,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Deadline = Wall(task.ScheduledStart),
        });
        if (card is not null && task.AssigneeId is { } assigneeId)
            await Assign(card.TaskId, assigneeId);
        await RefreshTasksAsync();
        return card is not null;
    }

    public async Task<bool> UpdateTaskAsync(TaskCardDto task, NewTaskResult edited)
    {
        var ok = await SendAsync(HttpMethod.Patch, $"api/tasks/{task.TaskId}", new UpdateTaskRequest
        {
            Title = edited.Title,
            Description = edited.Description,
            Priority = edited.Priority,
            Deadline = Wall(edited.ScheduledStart),
            ProjectId = edited.ProjectId ?? DailyProjectId,
        });

        // The UI shows one assignee, so picking another is a reassign (UC-04).
        int? current = task.AssigneeIds.Count > 0 ? task.AssigneeIds[0] : null;
        if (ok && edited.AssigneeId != current)
        {
            foreach (var old in task.AssigneeIds)
                ok = ok && await SendAsync(HttpMethod.Delete, $"api/tasks/{task.TaskId}/assignments/{old}");
            if (ok && edited.AssigneeId is { } assigneeId)
                ok = await Assign(task.TaskId, assigneeId);
        }
        await RefreshTasksAsync();
        return ok;
    }

    /// <summary>Ticking asks who completed the task (anyone, not just the assignee) and completes
    /// it; unticking reopens it. SM-01 has no To Do/Blocked → Done edge, so a tick walks through
    /// In Progress first - two legal moves, each guarded by the API.</summary>
    public async Task SetTaskDoneAsync(TaskCardDto task, bool done)
    {
        int? completedBy = null;
        if (done)
        {
            var dialog = await dialogs.ShowAsync<Frontend_BlazorWeb.Pages.CompletedByDialog>("Complete task",
                new DialogParameters { ["TaskTitle"] = task.Title },
                new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true });
            if (await dialog.Result is not { Canceled: false, Data: int who }) return;
            completedBy = who;
        }

        var ok = true;
        if (done && !string.Equals(task.Status, "In Progress", StringComparison.OrdinalIgnoreCase))
            ok = await Move(task.TaskId, "In Progress");
        if (ok) await Move(task.TaskId, done ? "Done" : "In Progress", completedBy);
        await RefreshTasksAsync();
    }

    private Task<bool> Move(int taskId, string status, int? completedBy = null) =>
        SendAsync(HttpMethod.Post, $"api/tasks/{taskId}/move", new MoveTaskRequest { NewStatus = status, CompletedBy = completedBy });

    private Task<bool> Assign(int taskId, int userId) =>
        SendAsync(HttpMethod.Post, $"api/tasks/{taskId}/assignments", new AssignTaskRequest { AssigneeId = userId });

    // ------------------------------------------------------------------
    // Live updates: the SignalR Observer from the architecture doc. The
    // server says what changed; refetching that list keeps one data path.
    // ------------------------------------------------------------------

    private async Task ConnectHubAsync()
    {
        hub = new HubConnectionBuilder()
            .WithUrl(new Uri(http.BaseAddress!, "hubs/notifications"),
                // Not CurrentUser?.Token: CurrentUser is set only after the session (and this hub) starts.
                o => o.AccessTokenProvider = () => Task.FromResult(http.DefaultRequestHeaders.Authorization?.Parameter))
            .WithAutomaticReconnect()
            .Build();

        On("boardChanged", RefreshTasksAsync);
        On("taskAssigned", RefreshNotificationsAsync);
        On("projectAssigned", RefreshNotificationsAsync, RefreshProjectsAsync);
        On("requestSubmitted", RefreshNotificationsAsync, RefreshRequestsAsync);
        On("requestReviewed", RefreshNotificationsAsync, RefreshRequestsAsync);
        On("notification", RefreshNotificationsAsync);

        try
        {
            await hub.StartAsync();
        }
        catch (Exception)
        {
            snackbar.Add("Live updates are unavailable; other people's changes appear after a refresh.",
                Severity.Warning);
        }
    }

    private void On(string eventName, params Func<Task>[] refreshes) =>
        hub!.On<JsonElement>(eventName, _ => Task.WhenAll(refreshes.Select(refresh => refresh())));

    // ------------------------------------------------------------------
    // HTTP. A failed call shows a snackbar and returns null/false.
    // ------------------------------------------------------------------

    public Task<T?> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url, null);

    public async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body)
    {
        if (await Send(method, url, body) is not { } res) return default;
        try
        {
            return await res.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or TaskCanceledException)
        {
            // An empty or unexpected body (e.g. 204 No Content) - report it rather than crash the page.
            snackbar.Add("The server sent back a response this page couldn't read.", Severity.Error);
            return default;
        }
    }

    public async Task<bool> SendAsync(HttpMethod method, string url, object? body = null) =>
        await Send(method, url, body) is not null;

    private async Task<HttpResponseMessage?> Send(HttpMethod method, string url, object? body)
    {
        HttpResponseMessage res;
        try
        {
            res = await http.SendAsync(new HttpRequestMessage(method, url)
            {
                Content = body is null ? null : JsonContent.Create(body),
            });
        }
        catch (HttpRequestException)
        {
            snackbar.Add($"Can't reach the server at {http.BaseAddress}.", Severity.Error);
            return null;
        }
        catch (TaskCanceledException)
        {
            snackbar.Add("The server took too long to respond. Please try again.", Severity.Error);
            return null;
        }
        if (res.IsSuccessStatusCode) return res;

        if (res.StatusCode != HttpStatusCode.Unauthorized)
        {
            snackbar.Add(await ErrorMessage(res), Severity.Error);
        }
        else if (CurrentUser is not null)
        {
            await LogoutAsync();
            snackbar.Add("Your session has expired. Please sign in again.", Severity.Warning);
        }
        return null;
    }

    // The API's own errors are ErrorResponse { message }; model validation failures are ProblemDetails.
    private static async Task<string> ErrorMessage(HttpResponseMessage res)
    {
        try
        {
            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("message", out var message)) return message.GetString()!;
            if (json.TryGetProperty("errors", out var errors))
                return errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()).First().GetString()!;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            // No JSON body (e.g. a bare 403); fall through to the status line.
        }
        return $"The server returned {(int)res.StatusCode} {res.ReasonPhrase}.";
    }

    // Deadlines and meeting dates are wall-clock times: send them without a UTC offset.
    public static DateTime Wall(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    // Times the server stamps itself (submitted, reviewed) are UTC.
    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
}

public static class TaskCardExtensions
{
    public static bool IsDone(this TaskCardDto task) =>
        string.Equals(task.Status, "Done", StringComparison.OrdinalIgnoreCase);

    // ponytail: the ERD has no all-day flag, so a deadline at exactly midnight
    // means "any time that day". Add a column if midnight deadlines become real.
    public static bool IsAllDay(this TaskCardDto task) => task.Deadline.TimeOfDay == TimeSpan.Zero;
}
