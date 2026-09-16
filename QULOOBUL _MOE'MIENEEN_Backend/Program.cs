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

builder.Services.AddSingleton(_ => new BlobServiceClient(
    builder.Configuration.GetConnectionString("BlobStorage") ?? "UseDevelopmentStorage=true"));

// --- Auth ---
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
            }
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

// --- Use-case services ---
builder.Services.AddScoped<JwtTokenFactory>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<NotificationService>();
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