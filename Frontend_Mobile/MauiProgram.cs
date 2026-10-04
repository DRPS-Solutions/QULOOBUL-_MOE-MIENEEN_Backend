using Microsoft.Extensions.Logging;

namespace Frontend_Mobile;

/// <summary>
/// App entry point for every platform: one .NET MAUI project builds the Android, iOS,
/// Mac Catalyst and Windows apps.
/// Reference: Microsoft (2026) .NET Multi-platform App UI documentation. Available at: https://learn.microsoft.com/en-us/dotnet/maui/?view=net-maui-10.0 (Accessed: 8 August 2026).
/// Reference: Microsoft (2026) .NET MAUI app startup. Available at: https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/app-startup (Accessed: 4 October 2026).
/// </summary>
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
