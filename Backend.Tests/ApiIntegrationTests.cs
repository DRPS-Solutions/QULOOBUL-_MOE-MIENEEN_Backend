using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Task;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using QULOOBUL__MOE_MIENEEN_Shared.Notifications;
using QULOOBUL__MOE_MIENEEN_Shared.Requests;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace Backend.Tests;

/// <summary>
/// The real pipeline (routing, JWT, policies, EF Core) against a real SQL Server database that is
/// migrated from empty and dropped afterwards. Locally that is LocalDB; CI points TEST_SQL_CONNECTION
/// at a SQL Server service container. The database name is always generated, never the dev one.
/// Reference: Microsoft, "Integration tests in ASP.NET Core." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0
/// Reference: xUnit.net, "Shared context between tests." Accessed: Oct. 04, 2026. [Online]. Available: https://xunit.net/docs/shared-context
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local", StaffEmail = "staff@test.local", Password = "Passw0rd!Passw0rd";
//----------------------------------------------------------------------------------------------------------------------//
    public string ConnectionString { get; } = new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION")
        ?? @"Server=(localdb)\mssqllocaldb;Trusted_Connection=True;TrustServerCertificate=True")
    { InitialCatalog = $"QM_Test_{Guid.NewGuid():N}" }.ConnectionString;
//----------------------------------------------------------------------------------------------------------------------//
    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        // The ' in the project folder name breaks the MSBuild expression Mvc.Testing writes into
        // MvcTestingAppManifest.json, so find the API's content root (for appsettings.json) ourselves.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "QULOOBUL _MOE'MIENEEN_Backend", "appsettings.json"))) dir = dir.Parent;
        b.UseContentRoot(Path.Combine(dir.FullName, "QULOOBUL _MOE'MIENEEN_Backend"));

        b.UseSetting("ConnectionStrings:Default", ConnectionString);
        b.UseSetting("Jwt:SigningKey", "integration-test-signing-key-that-is-long-enough-for-hs256");
        b.UseSetting("Seed:AdminEmail", AdminEmail);
        b.UseSetting("Seed:AdminPassword", Password);
        b.UseSetting("RateLimit:AuthPerMinute", "1000");   // every test signs in; one test lowers it
        // The clock-driven jobs would race the assertions; MaintenanceJobTests covers them.
        b.ConfigureServices(s => s.RemoveAll<IHostedService>());
    }
//----------------------------------------------------------------------------------------------------------------------//
    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();                      // sync: DbSeeder is wired to UseSeeding, not UseAsyncSeeding

        var staff = new User
        {
            RoleId = await db.Roles.Where(r => r.RoleName == Roles.Staff).Select(r => r.RoleId).SingleAsync(),
            FirstName = "Sam", Surname = "Staff", Email = StaffEmail,
        };
        staff.PasswordHash = new PasswordHasher<User>().HashPassword(staff, Password);
        db.Users.Add(staff);
        await db.SaveChangesAsync();
    }
//----------------------------------------------------------------------------------------------------------------------//
    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
//----------------------------------------------------------------------------------------------------------------------//
    public async Task<(HttpClient Client, AuthResponse Me)> SignInAsync(string email)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = Password });
        res.EnsureSuccessStatusCode();
        var me = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", me.Token);
        return (client, me);
    }
//----------------------------------------------------------------------------------------------------------------------//
    public AppDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();
}
//----------------------------------------------------------------------------------------------------------------------//
//----------------------------------------New integration tests go here, not in LifecycleTests.cs----------------------------------------//
//----------------------------------------------------------------------------------------------------------------------//

public class ApiIntegrationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_is_anonymous_and_every_other_route_needs_a_token()
    {
        var client = api.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Health_is_unavailable_when_the_database_is_unreachable()
    {
        var missingDb = new SqlConnectionStringBuilder(api.ConnectionString) { InitialCatalog = $"QM_Missing_{Guid.NewGuid():N}" };
        var client = api.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", missingDb.ConnectionString)).CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health")).StatusCode);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Responses_carry_the_security_headers()
    {
        var res = await api.CreateClient().GetAsync("/health");
        Assert.Contains("default-src 'none'", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task The_directory_is_read_through_the_view_and_hides_the_system_account()
    {
        var (client, _) = await api.SignInAsync(ApiFactory.StaffEmail);
        var users = await client.GetFromJsonAsync<List<UserSummaryDto>>("/api/users");

        Assert.Contains(users!, u => u.Email == ApiFactory.AdminEmail && u.Role == Roles.Admin);
        Assert.DoesNotContain(users!, u => u.Email == SystemAccountEmail);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_user_reads_and_edits_their_own_profile_but_not_their_role()
    {
        var (client, me) = await api.SignInAsync(ApiFactory.StaffEmail);
        var profile = await client.GetFromJsonAsync<UserDto>("/api/users/me");
        Assert.Equal(ApiFactory.StaffEmail, profile!.Email);

        var res = await client.PatchAsJsonAsync($"/api/users/{me.UserId}", new UpdateUserRequest { CellNo = "082 123 4567" });
        res.EnsureSuccessStatusCode();
        Assert.Equal("082 123 4567", (await client.GetFromJsonAsync<UserDto>("/api/users/me"))!.CellNo);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/api/users/{me.UserId}",
            new UpdateUserRequest { RoleId = 1 })).StatusCode);

        // Email is the sign-in name: another account's address is refused.
        Assert.False((await client.PatchAsJsonAsync($"/api/users/{me.UserId}",
            new UpdateUserRequest { Email = ApiFactory.AdminEmail })).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/users/me/password-link", null)).StatusCode);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Staff_cannot_reach_admin_routes()
    {
        var (client, _) = await api.SignInAsync(ApiFactory.StaffEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/notifications/send",
            new { userIds = new[] { 1 }, message = "hi" })).StatusCode);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Only_the_admin_reads_a_users_audit_trail()
    {
        var (staff, staffMe) = await api.SignInAsync(ApiFactory.StaffEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/users/{staffMe.UserId}/audit")).StatusCode);

        var (admin, _) = await api.SignInAsync(ApiFactory.AdminEmail);
        var trail = await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/users/{staffMe.UserId}/audit");
        Assert.Contains(trail!, e => e.Action == "auth.login");
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Only_the_admin_deletes_a_user_and_their_details_are_erased()
    {
        var (admin, adminMe) = await api.SignInAsync(ApiFactory.AdminEmail);
        var created = await (await admin.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            FirstName = "Del", Surname = "Me", Email = "delete-me@test.local", CellNo = "082 000 0000",
            RoleId = (await admin.GetFromJsonAsync<List<RoleDto>>("/api/roles"))!.First(r => r.RoleName == Roles.Staff).RoleId,
        })).Content.ReadFromJsonAsync<UserDto>();

        var (staff, _) = await api.SignInAsync(ApiFactory.StaffEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync($"/api/users/{created!.UserId}")).StatusCode);
        Assert.False((await admin.DeleteAsync($"/api/users/{adminMe.UserId}")).IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/users/{created.UserId}")).StatusCode);
        var gone = (await admin.GetFromJsonAsync<List<UserSummaryDto>>("/api/users"))!.Single(u => u.UserId == created.UserId);
        Assert.Equal("Anonymised", gone.Status);
        Assert.DoesNotContain("delete-me", gone.Email);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Only_the_organiser_or_an_admin_changes_an_events_attendees()
    {
        var (admin, adminMe) = await api.SignInAsync(ApiFactory.AdminEmail);
        var (staff, staffMe) = await api.SignInAsync(ApiFactory.StaffEmail);
        var ev = await (await admin.PostAsJsonAsync("/api/events", new CreateEventRequest
        {
            Title = "Fete", EventDate = new DateTime(2031, 1, Random.Shared.Next(1, 28), 10, 0, 0), Location = $"Hall {Guid.NewGuid():N}",
        })).Content.ReadFromJsonAsync<EventDto>();

        var sneak = await staff.PostAsJsonAsync($"/api/events/{ev!.EventId}/attendees", new AddEventAttendeeRequest { UserId = staffMe.UserId });
        Assert.Equal(HttpStatusCode.Forbidden, sneak.StatusCode);

        var invite = await admin.PostAsJsonAsync($"/api/events/{ev.EventId}/attendees", new AddEventAttendeeRequest { UserId = staffMe.UserId });
        Assert.Equal(HttpStatusCode.NoContent, invite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync($"/api/events/{ev.EventId}/attendees/{adminMe.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync($"/api/events/{ev.EventId}/attendees/{staffMe.UserId}")).StatusCode);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Row_level_security_hides_other_users_notifications_even_from_a_raw_query()
    {
        int adminId, staffId;
        using (var scope = api.Services.CreateScope())
        {
            var db = api.Db(scope);
            adminId = await db.Users.Where(u => u.Email == ApiFactory.AdminEmail).Select(u => u.UserId).SingleAsync();
            staffId = await db.Users.Where(u => u.Email == ApiFactory.StaffEmail).Select(u => u.UserId).SingleAsync();
            var type = await db.NotificationTypes.FirstAsync();
            db.Notifications.AddRange(
                new Notification { UserId = adminId, TypeId = type.TypeId, Message = "for admin" },
                new Notification { UserId = staffId, TypeId = type.TypeId, Message = "for staff" });
            await db.SaveChangesAsync();
        }

        await using var conn = new SqlConnection(api.ConnectionString);
        await conn.OpenAsync();
        await using (var set = new SqlCommand("EXEC sp_set_session_context N'UserId', @id, @read_only = 1", conn))
        {
            set.Parameters.AddWithValue("@id", staffId);
            await set.ExecuteNonQueryAsync();
        }
        await using var count = new SqlCommand("SELECT COUNT(*) FROM Notification WHERE UserId <> @id", conn);
        count.Parameters.AddWithValue("@id", staffId);
        Assert.Equal(0, (int)(await count.ExecuteScalarAsync())!);

        // And the application path, with the interceptor setting the context, still works.
        var (staff, _) = await api.SignInAsync(ApiFactory.StaffEmail);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/notifications")).StatusCode);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Deleting_a_task_cascades_its_comments_and_unlinks_its_files()
    {
        var (admin, me) = await api.SignInAsync(ApiFactory.AdminEmail);
        var projects = await admin.GetFromJsonAsync<List<ProjectSummaryDto>>("/api/projects");
        var card = await (await admin.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
        {
            ProjectId = projects!.Single(p => p.ProjectName == DailyTasksProject).ProjectId,
            Title = "Sweep", Deadline = DateTime.UtcNow.AddDays(3),
        })).Content.ReadFromJsonAsync<TaskCardDto>();
        (await admin.PostAsJsonAsync($"/api/tasks/{card!.TaskId}/comments", new AddTaskCommentRequest { Comment = "on it" }))
            .EnsureSuccessStatusCode();

        using var scope = api.Services.CreateScope();
        var db = api.Db(scope);
        var file = new FileUpload
        {
            CategoryId = await db.FileCategories.Select(c => c.CategoryId).FirstAsync(),
            UploadedBy = me.UserId, TaskId = card.TaskId, FileName = "a.pdf", FileType = "application/pdf", FilePath = "x/a.pdf",
        };
        db.FileUploads.Add(file);
        await db.SaveChangesAsync();

        await db.TaskStatusHistory.Where(h => h.TaskId == card.TaskId).ExecuteDeleteAsync();   // history is NO ACTION by design
        await db.Tasks.Where(t => t.TaskId == card.TaskId).ExecuteDeleteAsync();

        Assert.False(await db.Set<TaskComment>().AnyAsync(c => c.TaskId == card.TaskId));
        Assert.Null(await db.FileUploads.Where(f => f.FileId == file.FileId).Select(f => f.TaskId).SingleAsync());
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Anyone_creates_daily_tasks_and_records_who_completed_one_not_assigned_to_them()
    {
        var (admin, adminMe) = await api.SignInAsync(ApiFactory.AdminEmail);
        var (staff, staffMe) = await api.SignInAsync(ApiFactory.StaffEmail);
        var daily = (await staff.GetFromJsonAsync<List<ProjectSummaryDto>>("/api/projects"))!
            .Single(p => p.ProjectName == DailyTasksProject).ProjectId;
        Assert.True((await staff.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
            { ProjectId = daily, Title = "Mop", Deadline = DateTime.UtcNow.AddDays(1) })).IsSuccessStatusCode);

        // Delegated strictly down: not to a peer (here, the admin themselves), but to a colleague of
        // staff's, so staff neither created it nor is assigned to it.
        var card = await (await admin.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
            { ProjectId = daily, Title = "Lock up", Deadline = DateTime.UtcNow.AddDays(1) })).Content.ReadFromJsonAsync<TaskCardDto>();
        Assert.False((await admin.PostAsJsonAsync($"/api/tasks/{card!.TaskId}/assignments",
            new AssignTaskRequest { AssigneeId = adminMe.UserId })).IsSuccessStatusCode);
        (await admin.PostAsJsonAsync($"/api/tasks/{card.TaskId}/assignments", new AssignTaskRequest { AssigneeId = await AddStaffAsync() }))
            .EnsureSuccessStatusCode();

        Assert.Contains((await staff.GetFromJsonAsync<List<TaskCardDto>>("/api/tasks"))!, t => t.TaskId == card.TaskId);
        Assert.False((await staff.PostAsJsonAsync($"/api/tasks/{card.TaskId}/move",
            new MoveTaskRequest { NewStatus = "Done", CompletedBy = SystemUserId() })).IsSuccessStatusCode); // inactive
        (await staff.PostAsJsonAsync($"/api/tasks/{card.TaskId}/move", new MoveTaskRequest { NewStatus = "Done", CompletedBy = staffMe.UserId }))
            .EnsureSuccessStatusCode();

        Assert.Equal(staffMe.UserId, (await admin.GetFromJsonAsync<TaskDetailDto>($"/api/tasks/{card.TaskId}"))!.CompletedBy);
    }

//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task An_admin_changes_a_role_and_issues_a_temporary_password_the_user_must_replace()
    {
        var (admin, adminMe) = await api.SignInAsync(ApiFactory.AdminEmail);
        var userId = await AddStaffAsync();
        var roles = (await admin.GetFromJsonAsync<List<RoleDto>>("/api/roles"))!;
        var fm = roles.First(r => r.RoleName == Roles.FacilityManager).RoleId;

        var promoted = await admin.PatchAsJsonAsync($"/api/users/{userId}", new UpdateUserRequest { RoleId = fm });
        Assert.Equal(Roles.FacilityManager, (await promoted.Content.ReadFromJsonAsync<UserDto>())!.Role);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/users/{adminMe.UserId}",
            new UpdateUserRequest { RoleId = fm })).StatusCode); // never demote yourself

        var temp = (await (await admin.PostAsync($"/api/users/{userId}/reset-password", null))
            .Content.ReadFromJsonAsync<TemporaryPasswordDto>())!.TemporaryPassword;
        string email;
        using (var scope = api.Services.CreateScope()) email = (await api.Db(scope).Users.FindAsync(userId))!.Email;

        var client = api.CreateClient();
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = temp }))
            .Content.ReadFromJsonAsync<AuthResponse>();
        Assert.True(login!.MustChangePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tasks")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users/me/password",
            new ChangePasswordRequest { CurrentPassword = "wrong", NewPassword = "brandnew99" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/users/me/password",
            new ChangePasswordRequest { CurrentPassword = temp, NewPassword = "brandnew99" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tasks")).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_notifies_the_admin_who_is_asking()
    {
        var (admin, _) = await api.SignInAsync(ApiFactory.AdminEmail);
        var userId = await AddStaffAsync();
        string email;
        using (var scope = api.Services.CreateScope()) email = (await api.Db(scope).Users.FindAsync(userId))!.Email;

        Assert.Equal(HttpStatusCode.NoContent, (await api.CreateClient().PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { FirstName = "Forgetful", Surname = "Person", Email = email })).StatusCode);

        var notes = (await admin.GetFromJsonAsync<List<NotificationDto>>("/api/notifications"))!;
        // The account's own name is shown; the typed one only as a mismatch warning (test plan A4).
        Assert.Contains(notes, n => n.Message.StartsWith($"Other Staff ({email})") && n.Message.Contains("Reset Password")
            && n.Message.Contains("\"Forgetful Person\", which doesn't match"));
    }

    [Fact]
    public async Task Cancelling_an_event_cancels_its_open_tasks_and_tells_its_staff_why()
    {
        var (admin, _) = await api.SignInAsync(ApiFactory.AdminEmail);
        var (staff, staffMe) = await api.SignInAsync(ApiFactory.StaffEmail);
        var project = await (await admin.PostAsJsonAsync("/api/projects", new CreateProjectRequest { ProjectName = $"Fete {Guid.NewGuid():N}" }))
            .Content.ReadFromJsonAsync<ProjectDto>();
        (await admin.PostAsJsonAsync($"/api/projects/{project!.ProjectId}/members", new AddProjectMemberRequest { UserId = staffMe.UserId }))
            .EnsureSuccessStatusCode();
        var card = await (await admin.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
        {
            ProjectId = project.ProjectId, Title = "Hire chairs", Deadline = DateTime.UtcNow.AddDays(5),
        })).Content.ReadFromJsonAsync<TaskCardDto>();

        // Staff aren't the owner: refused.
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/projects/{project.ProjectId}/cancel",
            new CancelProjectRequest())).StatusCode);

        var cancelled = await (await admin.PostAsJsonAsync($"/api/projects/{project.ProjectId}/cancel",
            new CancelProjectRequest { Reason = "Venue flooded" })).Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal("Cancelled", (await admin.GetFromJsonAsync<TaskDetailDto>($"/api/tasks/{card!.TaskId}"))!.Status);
        Assert.Contains(await staff.GetFromJsonAsync<List<NotificationDto>>("/api/notifications") ?? [],
            n => n.Message.Contains(project.ProjectName) && n.Message.Contains("Venue flooded"));

        // Twice is refused rather than re-notifying everyone.
        Assert.False((await admin.PostAsJsonAsync($"/api/projects/{project.ProjectId}/cancel", new CancelProjectRequest()))
            .IsSuccessStatusCode);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_decline_reason_is_kept_on_the_request_for_the_requester()
    {
        var (admin, _) = await api.SignInAsync(ApiFactory.AdminEmail);
        var (staff, _) = await api.SignInAsync(ApiFactory.StaffEmail);
        var category = (await staff.GetFromJsonAsync<List<RequestCategoryDto>>("/api/requests/categories"))!.First();
        var id = await (await staff.PostAsJsonAsync("/api/requests", new CreateResourceRequest { CategoryId = category.CategoryId, Detail = "Projector" }))
            .Content.ReadFromJsonAsync<int>();

        (await admin.PostAsJsonAsync($"/api/requests/{id}/review", new ReviewRequestRequest { Approve = false, Note = "Borrow the hall's one" }))
            .EnsureSuccessStatusCode();

        var mine = (await staff.GetFromJsonAsync<List<ResourceRequestDto>>("/api/requests"))!.Single(r => r.RequestId == id);
        Assert.Equal("Declined", mine.Status);
        Assert.Equal("Borrow the hall's one", mine.History.Single(h => h.NewStatus == "Declined").Note);
    }
//----------------------------------------------------------------------------------------------------------------------//
    private async Task<int> AddStaffAsync()
    {
        using var scope = api.Services.CreateScope();
        var db = api.Db(scope);
        var user = new User
        {
            RoleId = await db.Roles.Where(r => r.RoleName == Roles.Staff).Select(r => r.RoleId).SingleAsync(),
            FirstName = "Other", Surname = "Staff", Email = $"{Guid.NewGuid():N}@test.local",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.UserId;
    }

    private int SystemUserId()
    {
        using var scope = api.Services.CreateScope();
        return api.Db(scope).Users.Where(u => u.Email == SystemAccountEmail).Select(u => u.UserId).Single();
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task The_login_route_is_rate_limited_per_client()
    {
        var client = api.WithWebHostBuilder(b => b.UseSetting("RateLimit:AuthPerMinute", "2")).CreateClient();
        var wrong = new LoginRequest { Email = "nobody@test.local", Password = "wrong" };

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++) codes.Add((await client.PostAsJsonAsync("/api/auth/login", wrong)).StatusCode);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, codes[0]);
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[2]);
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//