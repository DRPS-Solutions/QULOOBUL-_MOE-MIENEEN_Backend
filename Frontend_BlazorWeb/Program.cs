using Frontend_BlazorWeb;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

// Blazor WebAssembly: the app runs in the browser and talks to the API over HTTP.
// Reference: Microsoft, "ASP.NET Core Blazor hosting models." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-10.0
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API is its own origin (wwwroot/appsettings.json); its CORS policy lists this app's URLs.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Set ApiBaseUrl in wwwroot/appsettings.json.");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
// Reference: MudBlazor, "Installation - MudBlazor." Accessed: Oct. 04, 2026. [Online]. Available: https://mudblazor.com/getting-started/installation
builder.Services.AddMudServices();
builder.Services.AddScoped<AppDataService>();

await builder.Build().RunAsync();
