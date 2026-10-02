using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Frontend_Mobile.Models;
using Microsoft.AspNetCore.SignalR.Client;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using QULOOBUL__MOE_MIENEEN_Shared.Files;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;
using QULOOBUL__MOE_MIENEEN_Shared.Reports;
using QULOOBUL__MOE_MIENEEN_Shared.Requests;
using QULOOBUL__MOE_MIENEEN_Shared.Search;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;
// MAUI's own Microsoft.Maui.ApplicationModel.Permissions (camera/location prompts) is brought
// into every file by a project-wide global using, and collides by name with the one Common.
// Permissions above resolves via "using static". This alias wins over both, unambiguously.
using Permissions = QULOOBUL__MOE_MIENEEN_Shared.Classes.Common.Permissions;

namespace Frontend_Mobile.Data;

/// <summary>
/// The mobile client's data layer: the same job as the web client's AppDataService.
/// It signs in against the API, keeps the signed-in user plus the lists several
/// pages share, and listens on the API's SignalR hub so those lists refresh when
/// something changes server-side. Every HTTP call goes through Send, so failures
/// look the same on every page (an alert) and an expired token signs the user out.
/// Pages subscribe to <see cref="Changed"/> while they're on screen.
/// </summary>
public sealed class AppState : Observable
{
    // Debug: the API's https launch profile (Android's emulator reaches the host's localhost at 10.0.2.2).
    // Release: the production App Service, so a side-loaded APK works on a real phone.
    // Declared before Current: Current's own initializer constructs an AppState instance (whose
    // _http field reads ApiBase), and static fields initialize in declaration order - ApiBase has
    // to exist first, or _http.BaseAddress silently ends up null.
#if DEBUG
    private static readonly Uri ApiBase = new(DeviceInfo.Platform == DevicePlatform.Android
        ? "https://10.0.2.2:7272/"
        : "https://localhost:7272/");
#else
    private static readonly Uri ApiBase = new("https://xbcad-api-prod-e2fef0gxejcbhtd2.southafricanorth-01.azurewebsites.net/");
#endif

    public static AppState Current { get; } = new();

    private AppState() { }

    private const string SessionKey = "session";

    private readonly HttpClient _http = new(CreateHandler()) { BaseAddress = ApiBase };
    private HubConnection? _hub;

    /// <summary>Raised on the main thread whenever a shared list is reloaded.</summary>
    public event Action? Changed;

    // ----- Session (UC-01) -----

    private AuthResponse? _currentUser;

    public AuthResponse? CurrentUser
    {
        get => _currentUser;
        private set
        {
            if (!Set(ref _currentUser, value)) return;
            Raise(nameof(IsLoggedIn));
            Raise(nameof(CanSeeTasks));
            Raise(nameof(CanSeeRequests));
            Raise(nameof(CanSeeMeetings));
            Raise(nameof(CanSeeEvents));
            Raise(nameof(CanSeeUserManagement));
            Raise(nameof(CanSeeReports));
        }
    }

    public bool IsLoggedIn => CurrentUser is not null;
    public string? Role => CurrentUser?.Role;

    // The spec's screen/role matrix, same rules as the web client. Home and
    // Notifications are open to everyone; Staff and Imaamat get only those two.
    public bool CanSeeTasks => Role is Roles.FacilityManager or Roles.SecondaryAdmin or Roles.Admin;
    public bool CanSeeRequests => CanSeeTasks;
    public bool CanSeeMeetings => Role is Roles.SecondaryAdmin or Roles.Admin;
    public bool CanSeeEvents => CanSeeMeetings;
    public bool CanSeeUserManagement => Role is Roles.Admin;

    public bool CanReviewRequests => CurrentUser?.Permissions.Contains(QULOOBUL__MOE_MIENEEN_Shared.Classes.Common.Permissions.ReviewRequests) == true;

    // ReportsController is gated on the GenerateReports policy, not a role, so the drawer entry follows the same permission.
    public bool CanSeeReports => CurrentUser?.Permissions.Contains(QULOOBUL__MOE_MIENEEN_Shared.Classes.Common.Permissions.GenerateReports) == true;

    /// <summary>Generic permission check, for one-off gates (Calendar's organiser-or-admin rule)
    /// that don't warrant their own named CanSee* property.</summary>
    public bool HasPermission(string permission) => CurrentUser?.Permissions.Contains(permission) == true;

    /// <summary>The Shell route that opens one record, by the entity type name the API uses on
    /// notifications and search hits; null when this role has no page for it. Only Tasks and
    /// Calendar take a deep-link id; the rest open their list page.</summary>
    public string? LinkTo(string? entityType, int? id) => (entityType, id) switch
    {
        ("ProjectTask", { } t) when CanSeeTasks => $"//tasks?task={t}",
        ("ResourceRequest", _) when CanSeeRequests => "//requests",
        ("Project", _) when CanSeeEvents => "//events",
        ("Event", { } e) => $"//calendar?event={e}",
        ("Meeting", _) when CanSeeMeetings => "//meetings",
        ("File", _) => "//files",
        ("User", _) when CanSeeUserManagement => "//users",
        _ => null
    };

    public Task<List<SearchResultDto>?> SearchAsync(string q) =>
        GetAsync<List<SearchResultDto>>($"api/search?q={Uri.EscapeDataString(q.Trim())}");

    /// <summary>Returns null on success, otherwise the message for the login form.</summary>
    public async Task<string?> LoginAsync(string email, string password)
    {
        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsJsonAsync("api/auth/login",
                new LoginRequest { Email = email.Trim(), Password = password });
        }
        catch (HttpRequestException)
        {
            return $"Can't reach the server at {ApiBase}.";
        }
        if (!res.IsSuccessStatusCode) return await ErrorMessage(res);

        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        // The API refuses a temporary password everywhere but its change route, which only the website offers.
        if (auth.MustChangePassword) return "You have a temporary password. Sign in on the website to choose your own first.";
        await StartSessionAsync(auth);

        // The token lives in the platform keystore (Android Keystore / iOS Keychain), never a plain file.
        try { await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(auth)); }
        catch (Exception) { /* no secure store on this platform: the session just isn't remembered */ }
        return null;
    }

    /// <summary>Resumes the last session from SecureStorage if its token hasn't expired.</summary>
    public async Task<bool> TryRestoreAsync()
    {
        string? json = null;
        try { json = await SecureStorage.Default.GetAsync(SessionKey); }
        catch (Exception) { /* keystore unavailable or reset: sign in again */ }

        if (json is null
            || JsonSerializer.Deserialize<AuthResponse>(json) is not { } auth
            || auth.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(1))
            return false;

        await StartSessionAsync(auth);
        return IsLoggedIn;
    }

    private async Task StartSessionAsync(AuthResponse auth)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        // Load names/projects first, so nothing shows as "User #3" on the first screen.
        await Task.WhenAll(RefreshUsersAsync(), RefreshProjectsAsync(), RefreshNotificationsAsync());
        CurrentUser = auth;
        await ConnectHubAsync();
    }

    public async Task LogoutAsync()
    {
        CurrentUser = null;
        _http.DefaultRequestHeaders.Authorization = null;
        try { SecureStorage.Default.Remove(SessionKey); }
        catch (Exception) { /* nothing was stored */ }
        Users = [];
        Projects = [];
        Tasks = [];
        Requests = [];
        Notifications = [];
        _taskWindowUrl = null;
        RefreshNotificationCounts();
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    // ----- Shared lists -----

    public IReadOnlyList<UserSummaryDto> Users { get; private set; } = [];
    public IReadOnlyList<ProjectSummaryDto> Projects { get; private set; } = [];
    public IReadOnlyList<TaskCardDto> Tasks { get; private set; } = [];
    public IReadOnlyList<ResourceRequestDto> Requests { get; private set; } = [];
    public IReadOnlyList<NotificationDto> Notifications { get; private set; } = [];

    // Tasks holds one date window at a time: whichever page is showing tasks asked for it.
    private string? _taskWindowUrl;

    public Task RefreshUsersAsync() => Refresh<UserSummaryDto>("api/users", l => Users = l);
    public Task RefreshProjectsAsync() => Refresh<ProjectSummaryDto>("api/projects", l => Projects = l);
    public Task RefreshRequestsAsync() => Refresh<ResourceRequestDto>("api/requests", l => Requests = l);

    public Task RefreshNotificationsAsync() => Refresh<NotificationDto>("api/notifications", l =>
    {
        Notifications = l;
        RefreshNotificationCounts();
    });

    public Task RefreshTasksAsync() =>
        _taskWindowUrl is null ? Task.CompletedTask : Refresh<TaskCardDto>(_taskWindowUrl, l => Tasks = l);

    /// <summary>Loads the tasks due in [from, to) into <see cref="Tasks"/>.</summary>
    public Task LoadTasksAsync(DateTime from, DateTime to)
    {
        _taskWindowUrl = $"api/tasks?from={from:s}&to={to:s}";
        return RefreshTasksAsync();
    }

    // Applied on the main thread: the Shell binds to the unread badge, and hub
    // callbacks arrive on a background thread.
    private async Task Refresh<T>(string url, Action<List<T>> apply)
    {
        if (await GetAsync<List<T>>(url) is not { } list) return;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            apply(list);
            Changed?.Invoke();
        });
    }

    public string UserName(int? id) =>
        id is null ? "" : Users.FirstOrDefault(u => u.UserId == id)?.FullName ?? $"User #{id}";

    public int? DailyProjectId => Projects.FirstOrDefault(p => p.ProjectName == DailyTasksProject)?.ProjectId;

    // The UI's "events" are every project except the seeded daily-tasks one.
    public IEnumerable<ProjectSummaryDto> EventProjects => Projects.Where(p => p.ProjectName != DailyTasksProject);

    public string? ProjectName(int? id) => Projects.FirstOrDefault(p => p.ProjectId == id)?.ProjectName;

    /// <summary>The event a task is linked to, or null for a daily task.</summary>
    public string? EventName(TaskCardDto task) => task.ProjectId == DailyProjectId ? null : ProjectName(task.ProjectId);

    public TaskItem ToItem(TaskCardDto card) => new(card, EventName(card), card.CompletedBy is null ? null : UserName(card.CompletedBy));

    // ----- Notifications (UC-08) -----

    public int UnreadNotificationCount => Notifications.Count(n => !n.IsRead);

    public string UnreadBadge => UnreadNotificationCount switch
    {
        0 => string.Empty,
        > 9 => "9+",
        var n => n.ToString()
    };

    public bool HasUnread => UnreadNotificationCount > 0;

    private void RefreshNotificationCounts()
    {
        Raise(nameof(UnreadNotificationCount));
        Raise(nameof(UnreadBadge));
        Raise(nameof(HasUnread));
    }

    public async Task MarkReadAsync(IReadOnlyList<int> ids)
    {
        if (ids.Count > 0 && await SendAsync(HttpMethod.Post, "api/notifications/read",
                new MarkReadRequest { NotificationIds = ids }))
            await RefreshNotificationsAsync();
    }

    // ----- Tasks (UC-02 / UC-04) -----

    public async Task<bool> CreateTaskAsync(string title, DateTime start, string priority)
    {
        if (DailyProjectId is not { } projectId)
        {
            await Alert($"The server has no \"{DailyTasksProject}\" project to hold daily tasks.");
            return false;
        }
        var card = await SendAsync<TaskCardDto>(HttpMethod.Post, "api/tasks", new CreateTaskRequest
        {
            ProjectId = projectId,
            Title = title,
            Priority = priority,
            Deadline = Wall(start),
        });
        await RefreshTasksAsync();
        return card is not null;
    }

    /// <summary>
    /// Ticking completes the task as the signed-in user (the API records who);
    /// unticking reopens it. SM-01 has no To Do/Blocked → Done edge, so a tick walks
    /// through In Progress first - two legal moves, each guarded by the API.
    /// </summary>
    public async Task SetTaskDoneAsync(TaskCardDto task, bool done)
    {
        var ok = true;
        if (done && !IsStatus(task, "In Progress"))
            ok = await Move(task.TaskId, "In Progress");
        if (ok) await Move(task.TaskId, done ? "Done" : "In Progress");
        await RefreshTasksAsync();
    }

    private static bool IsStatus(TaskCardDto task, string status) =>
        string.Equals(task.Status, status, StringComparison.OrdinalIgnoreCase);

    private Task<bool> Move(int taskId, string status) =>
        SendAsync(HttpMethod.Post, $"api/tasks/{taskId}/move", new MoveTaskRequest { NewStatus = status });

    // ----- Resource requests (UC-05) -----

    public async Task<bool> SubmitRequestAsync(int categoryId, string detail)
    {
        var id = await SendAsync<int?>(HttpMethod.Post, "api/requests",
            new CreateResourceRequest { CategoryId = categoryId, Detail = detail });
        await RefreshRequestsAsync();
        return id is not null;
    }

    public async Task ReviewRequestAsync(int requestId, bool approve, string? note)
    {
        if (await SendAsync(HttpMethod.Post, $"api/requests/{requestId}/review",
                new ReviewRequestRequest { Approve = approve, Note = note }))
            await RefreshRequestsAsync();
    }

    /// <summary>Reviewers act on requests still awaiting a decision, never their own.</summary>
    public bool CanReview(ResourceRequestDto r) =>
        CanReviewRequests
        && r.Status is "Pending" or "Escalated"
        && r.RequestedBy != CurrentUser?.UserId;

    // ----- Reports (UC-09) -----

    // The API recomputes the figures on every run and logs who ran it; there's nothing to cache client-side.
    public Task<ReportDto?> BuildReportAsync(ReportRequest request) =>
        SendAsync<ReportDto>(HttpMethod.Post, "api/reports", request);

    /// <summary>Same figures as a CSV in Blob Storage; the API returns a short-lived download link.</summary>
    public Task<ReportExportResponse?> ExportReportAsync(ReportRequest request) =>
        SendAsync<ReportExportResponse>(HttpMethod.Post, "api/reports/export", request);

    // ----- Calendar (dated organisational events, UC-06) -----

    // Open to every role - unlike Requests/Reports there's no CanSeeCalendar gate. Not a shared
    // list on AppState: nothing else in the app reads events, so CalendarPage fetches directly.
    public Task<List<EventDto>?> ListEventsAsync(bool includePast) =>
        GetAsync<List<EventDto>>(includePast ? "api/events" : $"api/events?from={Wall(DateTime.Today):s}");

    public Task<EventDto?> GetEventAsync(int eventId) => GetAsync<EventDto>($"api/events/{eventId}");

    public Task<List<EventAttendeeDto>?> GetEventAttendeesAsync(int eventId) =>
        GetAsync<List<EventAttendeeDto>>($"api/events/{eventId}/attendees");

    public Task<EventDto?> CreateEventAsync(CreateEventRequest request) =>
        SendAsync<EventDto>(HttpMethod.Post, "api/events", request);

    /// <summary>RSVP for yourself; there is no route for answering on someone else's behalf.</summary>
    public Task<bool> RespondToEventAsync(int eventId, string status) =>
        SendAsync(HttpMethod.Post, $"api/events/{eventId}/response", new RespondToEventRequest { Status = status });

    public Task<bool> InviteToEventAsync(int eventId, int userId) =>
        SendAsync(HttpMethod.Post, $"api/events/{eventId}/attendees", new AddEventAttendeeRequest { UserId = userId });

    public Task<bool> RemoveEventAttendeeAsync(int eventId, int userId) =>
        SendAsync(HttpMethod.Delete, $"api/events/{eventId}/attendees/{userId}");

    // ----- Files (UC-07 / SD-04) -----

    // Open to every role - like Calendar, no CanSeeFiles gate; only Recategorise/Archive/Delete
    // are gated server-side on ManageFiles, so FilesPage checks HasPermission per-action instead.
    public Task<List<FileCategoryDto>?> GetFileCategoriesAsync() => GetAsync<List<FileCategoryDto>>("api/files/categories");

    public Task<List<FileMetadataDto>?> ListFilesAsync(bool includeArchived, string? search, int? categoryId)
    {
        var url = $"api/files?includeArchived={includeArchived}"
            + (string.IsNullOrWhiteSpace(search) ? "" : $"&q={Uri.EscapeDataString(search.Trim())}")
            + (categoryId is { } c ? $"&categoryId={c}" : "");
        return GetAsync<List<FileMetadataDto>>(url);
    }

    /// <summary>Records the metadata row and returns a short-lived upload SAS; the caller PUTs
    /// the actual bytes straight to that URL, never through this client's authenticated HttpClient.</summary>
    public Task<FileUploadResponse?> InitiateFileUploadAsync(FileUploadInitRequest request) =>
        SendAsync<FileUploadResponse>(HttpMethod.Post, "api/files", request);

    public Task<FileDownloadResponse?> GetFileDownloadLinkAsync(int fileId) =>
        GetAsync<FileDownloadResponse>($"api/files/{fileId}/download");

    public Task<bool> RecategoriseFileAsync(int fileId, int categoryId) =>
        SendAsync(HttpMethod.Patch, $"api/files/{fileId}/category", new RecategoriseFileRequest { CategoryId = categoryId });

    public Task<bool> SetFileArchivedAsync(int fileId, bool archived) =>
        SendAsync(archived ? HttpMethod.Post : HttpMethod.Delete, $"api/files/{fileId}/archive");

    public Task<bool> DeleteFileAsync(int fileId) => SendAsync(HttpMethod.Delete, $"api/files/{fileId}");

    // ----- Live updates: the SignalR Observer from the architecture doc. The
    // server says what changed; refetching that list keeps one data path. -----

    private async Task ConnectHubAsync()
    {
        _hub = new HubConnectionBuilder()
            .WithUrl(new Uri(ApiBase, "hubs/notifications"), o =>
            {
                o.AccessTokenProvider = () => Task.FromResult(CurrentUser?.Token);
                o.HttpMessageHandlerFactory = _ => CreateHandler();
#if DEBUG
                o.WebSocketConfiguration = ws => ws.RemoteCertificateValidationCallback =
                    (_, cert, _, errors) => errors == System.Net.Security.SslPolicyErrors.None || IsDevCert(cert);
#endif
            })
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
            await _hub.StartAsync();
        }
        catch (Exception)
        {
            await Alert("Live updates are unavailable; other people's changes appear when you reopen a page.");
        }
    }

    private void On(string eventName, params Func<Task>[] refreshes) =>
        _hub!.On<JsonElement>(eventName, _ => Task.WhenAll(refreshes.Select(refresh => refresh())));

    // ----- HTTP. A failed call shows an alert and returns null/false. -----

    public async Task<T?> GetAsync<T>(string url) =>
        await Send(HttpMethod.Get, url, null) is { } res ? await res.Content.ReadFromJsonAsync<T>() : default;

    public async Task<T?> SendAsync<T>(HttpMethod method, string url, object body) =>
        await Send(method, url, body) is { } res ? await res.Content.ReadFromJsonAsync<T>() : default;

    public async Task<bool> SendAsync(HttpMethod method, string url, object? body = null) =>
        await Send(method, url, body) is not null;

    private async Task<HttpResponseMessage?> Send(HttpMethod method, string url, object? body)
    {
        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(new HttpRequestMessage(method, url)
            {
                Content = body is null ? null : JsonContent.Create(body),
            });
        }
        catch (HttpRequestException)
        {
            await Alert($"Can't reach the server at {ApiBase}.");
            return null;
        }
        if (res.IsSuccessStatusCode) return res;

        if (res.StatusCode != HttpStatusCode.Unauthorized)
        {
            await Alert(await ErrorMessage(res));
        }
        else if (CurrentUser is not null)
        {
            await MainThread.InvokeOnMainThreadAsync(AppShell.SignOutAsync);
            await Alert("Your session has expired. Please sign in again.");
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

    private static Task Alert(string message) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current?.DisplayAlertAsync("Something went wrong", message, "OK") ?? Task.CompletedTask);

    private static HttpMessageHandler CreateHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        // The ASP.NET dev certificate is issued to localhost, so the emulator's 10.0.2.2
        // fails hostname validation. Debug builds trust that one cert; release builds trust nothing extra.
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
            errors == System.Net.Security.SslPolicyErrors.None || IsDevCert(cert);
#endif
        return handler;
    }

#if DEBUG
    private static bool IsDevCert(System.Security.Cryptography.X509Certificates.X509Certificate? cert) =>
        cert?.Issuer == "CN=localhost";
#endif

    // Deadlines are wall-clock times: send them without a UTC offset.
    public static DateTime Wall(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    // Times the server stamps itself (submitted, reviewed) are UTC.
    public static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    /// <summary>
    /// A server-issued link (Blob Storage SAS for a file download/upload, a report export) can
    /// point at "localhost" or "127.0.0.1" - meaningful on the machine running the API, but
    /// inside the Android emulator that means the emulator itself, not the host. Same problem
    /// ApiBase already solves for direct API calls; this covers links the server hands back.
    /// No-op on every other platform, and on a physical device (nothing to rewrite it to).
    /// </summary>
    public static Uri ForThisDevice(Uri serverUri)
    {
        if (DeviceInfo.Platform != DevicePlatform.Android) return serverUri;
        if (serverUri.Host is not ("localhost" or "127.0.0.1")) return serverUri;
        return new UriBuilder(serverUri) { Host = "10.0.2.2" }.Uri;
    }
}
