using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Auth;
using QULOOBUL__MOE_MIENEEN_Backend.Controllers;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Hubs;
using QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Middleware;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

// Local-only: lift KEY=VALUE lines from .env into the environment before the builder snapshots it,
// so ConnectionStrings__Default etc. override appsettings.json. Real env vars (CI, App Service) win.
if (File.Exists(".env"))
    foreach (var line in File.ReadAllLines(".env"))
    {
        var t = line.Trim();
        var eq = t.IndexOf('=');
        if (t.Length == 0 || t[0] == '#' || eq < 1) continue;
        var key = t[..eq].Trim();
        if (Environment.GetEnvironmentVariable(key) is null)
            Environment.SetEnvironmentVariable(key, t[(eq + 1)..].Trim().Trim('"'));
    }

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// UseSeeding runs after `dotnet ef database update` (and Migrate/EnsureCreated).
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AppDbContext>((sp, o) => o
    .UseSqlServer(builder.Configuration.GetConnectionString("Default"))
    .AddInterceptors(new SessionContextInterceptor(sp.GetRequiredService<IHttpContextAccessor>()))
    .UseSeeding((db, _) => DbSeeder.Seed((AppDbContext)db, builder.Configuration)));

// Azure SignalR Service is the backplane in Azure (infra/main.bicep sets the connection string);
// locally and in tests there is none, so the hub runs in-process.
var signalR = builder.Services.AddSignalR();
if (builder.Configuration["Azure:SignalR:ConnectionString"] is not null) signalR.AddAzureSignalR();
// Application Insights: traces, dependencies and exceptions, only where App Service provides the key.
if (builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] is not null)
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
builder.Services.AddHttpClient();     // EmailService talks to the Resend REST API

// UC-07 needs this: BlobStorageService cannot resolve without it. Locally it points at Azurite
// ("UseDevelopmentStorage=true"); in production the connection string comes from Key Vault.
builder.Services.AddSingleton(_ => new BlobServiceClient(
    builder.Configuration.GetConnectionString("BlobStorage") ?? "UseDevelopmentStorage=true"));
// ------------------------------------------------------- Auth ---------------------------------------------------------------
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
            ValidateLifetime = true,
        };
        // let SignalR read the token from the query string
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = c =>
            {
                var t = c.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(t) && c.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    c.Token = t;
                return Task.CompletedTask;
            },
            // SM-03 deactivate() "revoke sessions" (and Locked / reset): a JWT is only good while its
            // user is still Active. One PK lookup per request, cheaper than a token blocklist.
            OnTokenValidated = async c =>
            {
                var id = int.Parse(c.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var db = c.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                if (!await db.Users.AnyAsync(u => u.UserId == id && u.IsActive))
                    c.Fail("This account is no longer active.");
            },
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Permissions.ReviewRequests, p => p.RequireClaim("perm", Permissions.ReviewRequests));
    o.AddPolicy(Permissions.ManageUsers, p => p.RequireClaim("perm", Permissions.ManageUsers));
    o.AddPolicy(Permissions.GenerateReports, p => p.RequireClaim("perm", Permissions.GenerateReports));
    o.AddPolicy(Permissions.ManageFiles, p => p.RequireClaim("perm", Permissions.ManageFiles));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// Denial-of-service resilience: a per-caller budget (user id, else IP) for the whole API, and a much
// tighter one on the anonymous credential routes so passwords and set-password tokens cannot be sprayed.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c => RateLimitPartition.GetFixedWindowLimiter(
        c.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? c.Connection.RemoteIpAddress?.ToString() ?? "",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimit:PerMinute", 300),
            Window = TimeSpan.FromMinutes(1),
        }));
    o.AddPolicy(AuthController.RateLimitPolicy, c => RateLimitPartition.GetFixedWindowLimiter(
        c.Connection.RemoteIpAddress?.ToString() ?? "",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimit:AuthPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
        }));
});

// ------------------------------------------------------ Use-case services -------------------------------------------------
builder.Services.AddScoped<JwtTokenFactory>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AccessGuard>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<BlobStorageService>();
builder.Services.AddScoped<FileUploadService>();
builder.Services.AddScoped<FileManagementService>    ();
builder.Services.AddScoped<TaskAssignmentService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<RequestReviewService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MeetingMinutesService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<StaffOverviewService>();
builder.Services.AddScoped<NotificationQueryService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<TaskScheduleService>();

// SM-02's 48-hour escalation, SM-01's deadline reminders, and Task_Schedule's recurring-task
// generation: the transitions the doc drives from a clock rather than from a user action.
// Off in the staging slot (a slot-sticky Jobs__Enabled=false): it shares the production database,
// and after a swap it runs the previous build, which must not sweep a migrated schema.
if (builder.Configuration.GetValue("Jobs:Enabled", true))
    builder.Services.AddHostedService<MaintenanceJobs>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();
// The API only ever returns JSON, so it may load nothing and be framed by nobody. The web client's
// own CSP lives in Frontend_BlazorWeb/wwwroot/staticwebapp.config.json.
app.Use((c, next) =>
{
    c.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    c.Response.Headers.XContentTypeOptions = "nosniff";
    return next(c);
});
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();              // after authentication, so the global budget is per user
app.UseMiddleware<AuditMiddleware>();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

public partial class Program;       // WebApplicationFactory<Program> in Backend.Tests
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//