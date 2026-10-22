using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Frontend_Mobile.Platforms.Android;

namespace Frontend_Mobile
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // App not running, or not on screen: a tapped push notification starts the activity here
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            PushSetup.HandleIntent(Intent);
        }

        // App already open: the tap arrives as a new intent instead
        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            PushSetup.HandleIntent(intent);
        }
    }
}
