using Microsoft.Extensions.Logging;

namespace Frontend_Mobile;

/// <summary>
/// App entry point for every platform: one .NET MAUI project builds the Android, iOS,
/// Mac Catalyst and Windows apps.
/// Reference: Microsoft, ".NET Multi-platform App UI documentation." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/dotnet/maui/?view=net-maui-10.0
/// Reference: Microsoft, ".NET MAUI app startup." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/app-startup
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

#if ANDROID
		TapAsButton.Register();
#endif
#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//