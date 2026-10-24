using Android.App;
using Android.Runtime;
using Frontend_Mobile.Platforms.Android;

namespace Frontend_Mobile
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();

            Firebase.FirebaseApp.InitializeApp(this);

            // Before any push can arrive, including one that starts the app from closed
            PushSetup.EnsureChannel(this);
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
