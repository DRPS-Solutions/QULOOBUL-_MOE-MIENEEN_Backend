using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Middleware;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;

namespace Backend.Tests;

/// <summary>The exception and audit middleware, and the anonymous auth routes, which no other test file exercises.</summary>
public class MiddlewareTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    //----------------------------------------------------------------------------------------------------------------------//
    public static TheoryData<Exception, int> Mappings => new()
    {
        { new KeyNotFoundException("gone"), 404 },
        { new UnauthorizedAccessException("no"), 403 },
        { new InvalidOperationException("bad"), 400 },
        { new ArgumentException("bad"), 400 },
        { new DbUpdateConcurrencyException("stale"), 409 },
        { new DbUpdateException("dup", new Exception("inner")), 409 },
        { new NotSupportedException("boom"), 500 },
    };

    [Theory, MemberData(nameof(Mappings))]
    public async Task A_service_exception_becomes_the_matching_status_and_one_error_shape(Exception thrown, int status)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        await new ExceptionHandlingMiddleware(_ => throw thrown, NullLogger<ExceptionHandlingMiddleware>.Instance).Invoke(ctx);

        Assert.Equal(status, ctx.Response.StatusCode);
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("\"message\"", body, StringComparison.OrdinalIgnoreCase);
        // A 500 never leaks the exception text, only a reference support can find in the log.
        if (status == 500) { Assert.DoesNotContain("boom", body); Assert.Contains("Reference:", body); }
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_client_that_disconnects_mid_request_is_not_answered_or_logged_as_an_error()
    {
        using var cts = new CancellationTokenSource();
        var ctx = new DefaultHttpContext { RequestAborted = cts.Token };
        cts.Cancel();
        await new ExceptionHandlingMiddleware(_ => throw new OperationCanceledException(), NullLogger<ExceptionHandlingMiddleware>.Instance).Invoke(ctx);
        Assert.Equal(200, ctx.Response.StatusCode);   // untouched: nobody is left to tell
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Only_successful_state_changes_are_audited_and_reads_and_rejections_are_not()
    {
        var (staff, me) = await api.SignInAsync(ApiFactory.StaffEmail);
        var (admin, _) = await api.SignInAsync(ApiFactory.AdminEmail);
        async Task<int> Rows() =>
            (await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/users/{me.UserId}/audit"))!.Count(e => e.EntityType == "http");

        var before = await Rows();
        await staff.GetAsync("/api/users/me");                                                                  // a read
        var rejected = await staff.PatchAsJsonAsync($"/api/users/{me.UserId}", new UpdateUserRequest { RoleId = 1 });   // staff cannot change their role
        Assert.False(rejected.IsSuccessStatusCode);
        Assert.Equal(before, await Rows());

        (await staff.PatchAsJsonAsync($"/api/users/{me.UserId}", new UpdateUserRequest { CellNo = "082 555 0000" })).EnsureSuccessStatusCode();
        var trail = (await admin.GetFromJsonAsync<List<AuditEntryDto>>($"/api/users/{me.UserId}/audit"))!;
        Assert.Equal(before + 1, trail.Count(e => e.EntityType == "http"));
        Assert.Contains(trail, e => e.Action == $"PATCH /api/users/{me.UserId}");
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task An_unknown_set_password_token_is_refused_with_403_not_401()
    {
        var res = await api.CreateClient().PostAsJsonAsync("/api/auth/set-password",
            new SetPasswordRequest { Token = "not-a-real-token", Password = "Passw0rd!Passw0rd" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);   // 401 would read as an expired session to the client
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_set_password_request_that_breaks_the_DTO_rules_is_a_400()
    {
        var res = await api.CreateClient().PostAsJsonAsync("/api/auth/set-password", new SetPasswordRequest { Token = "x", Password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
