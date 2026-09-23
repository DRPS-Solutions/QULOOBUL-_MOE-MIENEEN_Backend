using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Auth;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Hubs;
using QULOOBUL__MOE_MIENEEN_Backend.SharedClasses.Middleware;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared;
using System.Security.Claims;
using System.Text;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// UseSeeding runs after `dotnet ef database update` (and Migrate/EnsureCreated).
builder.Services.AddDbContext<AppDbContext>(o => o
    .UseSqlServer(builder.Configuration.GetConnectionString("Default"))
    .UseSeeding((db, _) => DbSeeder.Seed((AppDbContext)db, builder.Configuration)));

builder.Services.AddSignalR();
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

// SM-02's 48-hour escalation, SM-01's deadline reminders, and Task_Schedule's recurring-task
// generation: the transitions the doc drives from a clock rather than from a user action.
builder.Services.AddHostedService<MaintenanceJobs>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//