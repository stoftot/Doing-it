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
}
