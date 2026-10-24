using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Shared.Auth;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace Backend.Tests;

/// <summary>The mobile app's 30-day sign-in: rotation, replay detection and every way it is revoked.</summary>
public class RefreshTokenTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    /// <summary>Each test gets its own account, since several of them end its sessions.</summary>
    private async Task<string> NewUserAsync()
    {
        var email = $"refresh-{Guid.NewGuid():N}@test.local";
        using var scope = api.Services.CreateScope();
        var db = api.Db(scope);
        var user = new User
        {
            RoleId = await db.Roles.Where(r => r.RoleName == Roles.Staff).Select(r => r.RoleId).SingleAsync(),
            FirstName = "Rae", Surname = "Fresh", Email = email,
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, ApiFactory.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return email;
    }

    private async Task<AuthResponse> LoginAsync(string email, bool refresh = true) =>
        (await (await api.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = ApiFactory.Password, IssueRefreshToken = refresh }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<AuthResponse>())!;

    private Task<HttpResponseMessage> RefreshAsync(string? token) =>
        api.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = token! });

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Only_a_sign_in_that_asks_gets_a_refresh_token_and_it_lasts_30_days()
    {
        var email = await NewUserAsync();
        Assert.Null((await LoginAsync(email, refresh: false)).RefreshToken);

        var mobile = await LoginAsync(email);
        Assert.NotNull(mobile.RefreshToken);
        Assert.InRange(mobile.RefreshExpiresAtUtc!.Value, DateTime.UtcNow.AddDays(29.9), DateTime.UtcNow.AddDays(30.1));
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_refresh_gives_a_working_token_and_a_new_refresh_token_and_spends_the_old_one()
    {
        var first = await LoginAsync(await NewUserAsync());
        var res = await RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var second = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(second.RefreshToken)).StatusCode);
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Replaying_a_spent_refresh_token_revokes_every_refresh_token_of_that_user()
    {
        var first = await LoginAsync(await NewUserAsync());
        var second = (await (await RefreshAsync(first.RefreshToken)).Content.ReadFromJsonAsync<AuthResponse>())!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(first.RefreshToken)).StatusCode);   // the copy
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(second.RefreshToken)).StatusCode);  // and the real one
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Signing_out_revokes_the_refresh_token()
    {
        var auth = await LoginAsync(await NewUserAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await api.CreateClient().PostAsJsonAsync("/api/auth/logout",
            new RefreshRequest { RefreshToken = auth.RefreshToken! })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Changing_the_password_revokes_older_refresh_tokens_and_can_issue_a_new_one()
    {
        var email = await NewUserAsync();
        var phone = await LoginAsync(email);
        var other = await LoginAsync(email);

        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", phone.Token);
        var changed = (await (await client.PostAsJsonAsync("/api/users/me/password", new ChangePasswordRequest
        {
            CurrentPassword = ApiFactory.Password, NewPassword = "Brand-new-pass1", IssueRefreshToken = true,
        })).EnsureSuccessStatusCode().Content.ReadFromJsonAsync<AuthResponse>())!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(phone.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(other.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(changed.RefreshToken)).StatusCode);
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_deactivated_account_cannot_refresh()
    {
        var email = await NewUserAsync();
        var auth = await LoginAsync(email);
        using (var scope = api.Services.CreateScope())
        {
            var db = api.Db(scope);
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Lifecycle.MoveUser(user, Lifecycle.UserInactive);
            user.DeactivatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
    }

    //----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task An_unknown_refresh_token_is_a_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(new string('A', 64))).StatusCode);
    }
}
