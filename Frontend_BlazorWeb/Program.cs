using Frontend_BlazorWeb;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API is its own origin (wwwroot/appsettings.json); its CORS policy lists this app's URLs.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("Set ApiBaseUrl in wwwroot/appsettings.json.");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddMudServices();
builder.Services.AddScoped<AppDataService>();

await builder.Build().RunAsync();
