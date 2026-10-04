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
// Reference: Microsoft (2026) Implementing the infrastructure persistence layer with Entity Framework Core. Available at: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core (Accessed: 8 August 2026).
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AppDbContext>((sp, o) => o
    // Retries ride out an Azure SQL serverless database resuming from auto-pause (error 40613).
    // Reference: Microsoft (2026) Connection resiliency - EF Core. Available at: https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency (Accessed: 4 October 2026).
    // Reference: Microsoft (2026) Serverless compute tier - Azure SQL Database. Available at: https://learn.microsoft.com/en-us/azure/azure-sql/database/serverless-tier-overview?view=azuresql (Accessed: 8 August 2026).
    .UseSqlServer(builder.Configuration.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure())
    .AddInterceptors(new SessionContextInterceptor(sp.GetRequiredService<IHttpContextAccessor>()))
    .UseSeeding((db, _) => DbSeeder.Seed((AppDbContext)db, builder.Configuration)));

// Azure SignalR Service is the backplane in Azure (infra/main.bicep sets the connection string);
// locally and in tests there is none, so the hub runs in-process.
// Reference: Microsoft (2026) Overview of ASP.NET Core SignalR. Available at: https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction?view=aspnetcore-10.0 (Accessed: 8 August 2026).
var signalR = builder.Services.AddSignalR();
if (builder.Configuration["Azure:SignalR:ConnectionString"] is not null) signalR.AddAzureSignalR();
// Application Insights: traces, dependencies and exceptions, only where App Service provides the key.
// Reference: Microsoft (2026) Enable Azure Monitor OpenTelemetry for .NET applications. Available at: https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable (Accessed: 4 October 2026).
if (builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] is not null)
    builder.Services.AddOpenTelemetry().UseAzureMonitor();

// UC-07 needs this: BlobStorageService cannot resolve without it. Locally it points at Azurite
// ("UseDevelopmentStorage=true"); in production the connection string comes from Key Vault.
// Reference: Microsoft (2026) Azure Key Vault configuration provider in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-10.0 (Accessed: 8 August 2026).
builder.Services.AddSingleton(_ => new BlobServiceClient(
    builder.Configuration.GetConnectionString("BlobStorage") ?? "UseDevelopmentStorage=true"));
// ------------------------------------------------------- Auth ---------------------------------------------------------------
// Reference: Jones, M., Bradley, J. and Sakimura, N. (2015) RFC 7519: JSON Web Token (JWT). Internet Engineering Task Force. Available at: https://www.rfc-editor.org/info/rfc7519 (Accessed: 8 August 2026).
// Reference: Microsoft (2026) Configure JWT bearer authentication in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication (Accessed: 4 October 2026).
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
        // Reference: Microsoft (2026) Authentication and authorization in ASP.NET Core SignalR. Available at: https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz (Accessed: 4 October 2026).
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
                var user = await db.Users.Where(u => u.UserId == id && u.IsActive)
                    .Select(u => new { u.MustChangePassword }).FirstOrDefaultAsync();
                if (user is null)
                    c.Fail("This account is no longer active.");
                // A temporary password unlocks only the change-password route.
                else if (user.MustChangePassword && c.Request.Path != "/api/users/me/password")
                    c.Fail("Change your temporary password first.");
            },
        };
    });

// Permission claims checked as named policies (OWASP A01 Broken Access Control).
// Reference: Microsoft (2026) Policy-based authorization in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies (Accessed: 4 October 2026).
// Reference: OWASP Foundation (2025) OWASP Top 10:2025. Available at: https://owasp.org/Top10/2025/ (Accessed: 8 August 2026).
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Permissions.ReviewRequests, p => p.RequireClaim("perm", Permissions.ReviewRequests));
    o.AddPolicy(Permissions.ManageUsers, p => p.RequireClaim("perm", Permissions.ManageUsers));
    o.AddPolicy(Permissions.GenerateReports, p => p.RequireClaim("perm", Permissions.GenerateReports));
    o.AddPolicy(Permissions.ManageFiles, p => p.RequireClaim("perm", Permissions.ManageFiles));
});

// Reference: Microsoft (2026) Enable Cross-Origin Requests (CORS) in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/cors (Accessed: 4 October 2026).
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// Denial-of-service resilience: a per-caller budget (user id, else IP) for the whole API, and a much
// tighter one on the anonymous credential routes so passwords and set-password tokens cannot be sprayed.
// Reference: Microsoft (2026) Rate limiting middleware in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit (Accessed: 4 October 2026).
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
// Request-scoped, like the DbContext they share, so one request is one Unit of Work.
// Reference: Microsoft (2026) Dependency injection in ASP.NET Core. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0 (Accessed: 8 August 2026).
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
builder.Services.AddScoped<SearchService>();

// SM-02's 48-hour escalation, SM-01's deadline reminders, and Task_Schedule's recurring-task
// generation: the transitions the doc drives from a clock rather than from a user action.
// Off in the staging slot (a slot-sticky Jobs__Enabled=false): it shares the production database,
// and after a swap it runs the previous build, which must not sweep a migrated schema.
// Reference: Microsoft (2026) Set up staging environments in Azure App Service. Available at: https://learn.microsoft.com/en-us/azure/app-service/deploy-staging-slots (Accessed: 8 August 2026).
if (builder.Configuration.GetValue("Jobs:Enabled", true))
    builder.Services.AddHostedService<MaintenanceJobs>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();
// The API only ever returns JSON, so it may load nothing and be framed by nobody. The web client's
// own CSP lives in Frontend_BlazorWeb/wwwroot/staticwebapp.config.json.
// Reference: MDN Web Docs (2026) Content-Security-Policy. Available at: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy (Accessed: 4 October 2026).
app.Use((c, next) =>
{
    c.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    c.Response.Headers.XContentTypeOptions = "nosniff";
    return next(c);
});
// Middleware order matters: each component sees only what the ones before it let through.
// Reference: Microsoft (2026) ASP.NET Core middleware. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0 (Accessed: 8 August 2026).
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