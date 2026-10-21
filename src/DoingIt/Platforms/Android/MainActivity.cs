using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.OS;
using Android.Views;

namespace DoingIt;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = SoftInput.AdjustResize, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        OpenWidgetDestination(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
#if DIAGNOSTIC_TEST_HOOKS
        // Compiled only in an explicitly requested test APK, never ordinary builds.
        if (intent?.Action == "com.doingit.app.TEST_MANAGED_CRASH")
            throw new InvalidOperationException("PRIVATE-TEST-MESSAGE");
#endif
        Intent = intent;
        OpenWidgetDestination(intent);
    }

    private static void OpenWidgetDestination(Intent? intent)
    {
        var action = intent?.Action;
        if (action is not (AndroidWidgets.PlannerWidgets.OpenTodayAction or AndroidWidgets.PlannerWidgets.CreateTaskAction)) return;
        intent!.SetAction(null); // Recreation must not reopen a consumed editor.
        if (Microsoft.Maui.Controls.Application.Current is App app)
            app.OpenFromWidget(action == AndroidWidgets.PlannerWidgets.CreateTaskAction);
    }

    protected override void OnResume()
    {
        base.OnResume();
        CrashDiagnostics.Breadcrumb("Activity resumed");
    }

    protected override void OnStop()
    {
        CrashDiagnostics.Breadcrumb("Activity stopped");
        base.OnStop();
    }
}
