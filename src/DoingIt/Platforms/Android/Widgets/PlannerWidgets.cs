using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using DoingIt.Core;

namespace DoingIt.AndroidWidgets;

// Three picker entries provide the two requested widgets in their fixed sizes.
[BroadcastReceiver(Name = "com.doingit.app.RemainingWidget", Label = "@string/widget_remaining_name", Exported = false)]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_remaining_info")]
public sealed class RemainingWidget : PlannerWidgetProvider;

[BroadcastReceiver(Name = "com.doingit.app.ProgressWidget", Label = "@string/widget_progress_name", Exported = false)]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_progress_info")]
public sealed class ProgressWidget : PlannerWidgetProvider;

[BroadcastReceiver(Name = "com.doingit.app.CreateTaskWidget", Label = "@string/widget_create_name", Exported = false)]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_create_info")]
public sealed class CreateTaskWidget : PlannerWidgetProvider;

public abstract class PlannerWidgetProvider : AppWidgetProvider
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);
        if (context is null) return;
        // Keep the process alive until the persisted snapshot has been read and
        // published, even when the app has no activity/window.
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try { await PlannerWidgets.RefreshAsync(context); }
            finally { pending?.Finish(); }
        });
    }
}

[BroadcastReceiver(Name = "com.doingit.app.WidgetRefreshReceiver", Exported = false)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced,
    Intent.ActionDateChanged, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged])]
public sealed class WidgetRefreshReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        if (intent?.Action == Intent.ActionTimezoneChanged) TimeZoneInfo.ClearCachedData();
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try { await PlannerWidgets.RefreshAsync(context); }
            finally { pending?.Finish(); }
        });
    }
}

internal static class PlannerWidgets
{
    internal const string OpenTodayAction = "com.doingit.app.OPEN_TODAY";
    internal const string CreateTaskAction = "com.doingit.app.CREATE_TASK";
    private const string RefreshAction = "com.doingit.app.REFRESH_WIDGETS";
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    internal static void Refresh() => _ = Task.Run(() => RefreshAsync(Android.App.Application.Context));

    internal static async Task RefreshAsync(Context context)
    {
        // Load inside the gate so an older read cannot overwrite newer counts.
        await RefreshGate.WaitAsync();
        try
        {
            var manager = AppWidgetManager.GetInstance(context)!;
            var remainingIds = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(RemainingWidget)))) ?? [];
            var progressIds = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(ProgressWidget)))) ?? [];
            var createIds = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(CreateTaskWidget)))) ?? [];
            foreach (var id in createIds)
            {
                var views = new RemoteViews(context.PackageName, Resource.Layout.widget_create);
                views.SetOnClickPendingIntent(Resource.Id.widget_root, OpenApp(context, true));
                manager.UpdateAppWidget(id, views);
            }

            var hasCounts = remainingIds.Length + progressIds.Length > 0;
            if (hasCounts)
            {
                TodayProgress? progress = null;
                try
                {
                    // Read-only store: the activity remains the only writer.
                    var store = new PlannerStore(Path.Combine(FileSystem.AppDataDirectory, "planner.json"), TaskTitleLayout.Fits);
                    await store.LoadAsync();
                    progress = TodayProgress.ForDate(store.Data, DateOnly.FromDateTime(DateTime.Today));
                }
                catch (Exception exception)
                {
                    // Do not turn unreadable saved data into a false "All done".
                    Android.Util.Log.Warn("DoingItWidgets", exception.ToString());
                }
                foreach (var id in remainingIds) UpdateCount(context, manager, id, progress, false);
                foreach (var id in progressIds) UpdateCount(context, manager, id, progress, true);
            }
            ScheduleNextDay(context, hasCounts);
        }
        catch (Exception exception)
        {
            // Widget-host failures must not turn a successful task save into an
            // error or crash a background broadcast. Periodic updates retry.
            Android.Util.Log.Warn("DoingItWidgets", exception.ToString());
        }
        finally { RefreshGate.Release(); }
    }

    private static void UpdateCount(Context context, AppWidgetManager manager, int id, TodayProgress? progress, bool wide)
    {
        var views = new RemoteViews(context.PackageName, wide ? Resource.Layout.widget_progress : Resource.Layout.widget_remaining);
        string number, label, description;
        if (progress is null)
        {
            number = "—";
            label = "Open app";
            description = "Couldn't read today's tasks. Open Doing it to retry.";
        }
        else if (progress.Total == 0)
        {
            number = "—";
            label = "No tasks today";
            description = "No tasks scheduled today. Open today's tasks.";
        }
        else if (progress.IsAllDone)
        {
            number = "✓";
            label = "All done";
            description = $"All {progress.Total} tasks done today. Open today's tasks.";
        }
        else
        {
            number = wide ? $"{progress.Completed}/{progress.Total}" : progress.Remaining.ToString();
            label = wide ? "done today" : "left today";
            description = $"{progress.Completed} of {progress.Total} tasks done today, {progress.Remaining} remaining. Open today's tasks.";
        }
        views.SetTextViewText(Resource.Id.widget_number, number);
        views.SetTextViewText(Resource.Id.widget_label, label);
        views.SetContentDescription(Resource.Id.widget_root, description);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, OpenApp(context, false));
        manager.UpdateAppWidget(id, views);
    }

    private static PendingIntent OpenApp(Context context, bool createTask)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetAction(createTask ? CreateTaskAction : OpenTodayAction);
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        return PendingIntent.GetActivity(context, createTask ? 1 : 0, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private static void ScheduleNextDay(Context context, bool enabled)
    {
        var alarm = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var intent = new Intent(context, typeof(WidgetRefreshReceiver)).SetAction(RefreshAction);
        var pending = PendingIntent.GetBroadcast(context, 2, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        if (!enabled)
        {
            alarm.Cancel(pending);
            return;
        }
        var nextDay = new DateTimeOffset(DateTime.Today.AddDays(1)).ToUnixTimeMilliseconds();
        // No exact-alarm permission or wakeup: Android may defer this while idle.
        // The provider's 30-minute refresh also recovers after process death.
        alarm.Set(AlarmType.Rtc, nextDay, pending);
    }
}
