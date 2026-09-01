using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Hubs;
using QULOOBUL__MOE_MIENEEN_Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSignalR();

// Connection string carries the account key locally (and in Azure until managed identity is wired).
builder.Services.AddSingleton(_ => new BlobServiceClient(
    builder.Configuration.GetConnectionString("BlobStorage") ?? "UseDevelopmentStorage=true"));

// Use-case services — constructor-injected, request-scoped (architecture doc, "Dependency injection").
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<BlobStorageService>();
builder.Services.AddScoped<FileUploadService>();
builder.Services.AddScoped<TaskAssignmentService>();
builder.Services.AddScoped<RequestReviewService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
