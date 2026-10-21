using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using DoingIt.Core;

namespace DoingIt.AndroidNotifications;

internal record NotificationOptions(bool Enabled, bool Sticky);

internal static class DailyNotifications
{
    internal const string ChannelId = "daily_tasks";
    private const int NotificationId = 100;
    private const string MidnightAction = "com.doingit.app.DAILY_MIDNIGHT";
    internal const string DismissAction = "com.doingit.app.DISMISS_DAILY";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    internal static event EventHandler? Updated;
    private static Context Context => Android.App.Application.Context;
    private static ISharedPreferences Preferences => Context.GetSharedPreferences("daily_notifications", FileCreationMode.Private)!;
    internal static NotificationOptions Options => new(Preferences.GetBoolean("enabled", false), Preferences.GetBoolean("sticky", false));

    internal static bool CanNotify
    {
        get
        {
            EnsureChannel(Context);
            if (!NotificationManagerCompat.From(Context)!.AreNotificationsEnabled()) return false;
            return !OperatingSystem.IsAndroidVersionAtLeast(26) || Manager(Context).GetNotificationChannel(ChannelId)?.Importance != NotificationImportance.None;
        }
    }

    internal static bool CanScheduleExactly => !OperatingSystem.IsAndroidVersionAtLeast(31) || Alarms(Context).CanScheduleExactAlarms();

    internal static async Task SetOptionsAsync(NotificationOptions options)
    {
        await Gate.WaitAsync();
        try
        {
            // A fresh generation prevents an old notification's delayed delete
            // intent from dismissing a newly enabled summary.
            using var edit = Preferences.Edit()!;
            if (!edit.PutBoolean("enabled", options.Enabled)!.PutBoolean("sticky", options.Sticky)!
                .PutString("generation", Guid.NewGuid().ToString("N"))!.Remove("dismissedDate")!.Commit())
                throw new IOException("Notification settings couldn't be saved. Please try again.");
            await RefreshLockedAsync(Context);
        }
        finally { Gate.Release(); Updated?.Invoke(null, EventArgs.Empty); }
    }

    internal static void Refresh() => _ = RefreshAsync(Context);

    internal static async Task RefreshAsync(Context context, Intent? dismissed = null)
    {
        await Gate.WaitAsync();
        try
        {
            if (dismissed?.Action == DismissAction && !Options.Sticky &&
                dismissed.GetStringExtra("generation") == Preferences.GetString("generation", ""))
            {
                // Persist normal dismissal across process death and task edits.
                using var edit = Preferences.Edit()!;
                edit.PutString("dismissedDate", dismissed.GetStringExtra("date"))!.Commit();
            }
            await RefreshLockedAsync(context);
        }
        catch (Exception exception) { Android.Util.Log.Warn("DoingItNotifications", exception.ToString()); }
        finally { Gate.Release(); Updated?.Invoke(null, EventArgs.Empty); }
    }

    private static async Task RefreshLockedAsync(Context context)
    {
        var options = Options;
        ScheduleNextDay(context, options.Enabled);
        if (!options.Enabled || !CanNotify)
        {
            Manager(context).Cancel(NotificationId);
            return;
        }
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!options.Sticky && Preferences.GetString("dismissedDate", "") == today.ToString("yyyy-MM-dd"))
        {
            Manager(context).Cancel(NotificationId);
            return;
        }

        DailyNotificationSummary? summary = null;
        try
        {
            // The activity remains the only writer of planner.json. Read under
            // the gate so a stale snapshot cannot replace a later task update.
            var store = new PlannerStore(Path.Combine(FileSystem.AppDataDirectory, "planner.json"), TaskTitleLayout.Fits);
            await store.LoadAsync();
            summary = DailyNotificationSummary.ForDate(store.Data, today);
        }
        catch (Exception exception) { Android.Util.Log.Warn("DoingItNotifications", exception.ToString()); }

        // A load error must never masquerade as an empty day or all tasks done.
        var title = summary?.Title ?? "Today's tasks are unavailable";
        var text = summary?.Text ?? "Open Doing it to retry. Your saved tasks have been kept.";
        var body = summary?.Body ?? text;
        var open = new Intent(context, typeof(MainActivity)).SetAction(AndroidWidgets.PlannerWidgets.OpenTodayAction);
        open.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        var tap = PendingIntent.GetActivity(context, 100, open, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        var generation = Preferences.GetString("generation", "")!;
        var date = today.ToString("yyyy-MM-dd");
        var deletion = new Intent(context, typeof(DailyNotificationReceiver)).SetAction(DismissAction)
            .SetData(Android.Net.Uri.Parse($"doingit://daily/{generation}/{date}"))
            .PutExtra("date", date).PutExtra("generation", generation);
        var delete = PendingIntent.GetBroadcast(context, 101, deletion, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        using var brand = BrandIcon(context);
        using var style = new NotificationCompat.BigTextStyle();
        style.BigText(body);
        using var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetSmallIcon(Resource.Drawable.notification_small);
        builder.SetLargeIcon(brand);
        builder.SetColor(Android.Graphics.Color.ParseColor("#1D4C5E"));
        builder.SetContentTitle(title);
        builder.SetContentText(text);
        builder.SetSubText(today.ToString("ddd, d MMM"));
        builder.SetStyle(style);
        builder.SetContentIntent(tap);
        builder.SetDeleteIntent(delete);
        builder.SetVisibility(NotificationCompat.VisibilityPublic);
        builder.SetCategory(NotificationCompat.CategoryReminder);
        builder.SetPriority(NotificationCompat.PriorityDefault);
        builder.SetSilent(true);
        builder.SetOnlyAlertOnce(true);
        builder.SetShowWhen(false);
        builder.SetOngoing(options.Sticky);
        builder.SetAutoCancel(false);
        Manager(context).Notify(NotificationId, builder.Build());
    }

    private static Android.Graphics.Bitmap BrandIcon(Context context)
    {
        var size = (int)(64 * context.Resources!.DisplayMetrics!.Density);
        var bitmap = Android.Graphics.Bitmap.CreateBitmap(size, size, Android.Graphics.Bitmap.Config.Argb8888!)!;
        using var canvas = new Android.Graphics.Canvas(bitmap);
        using var drawable = context.GetDrawable(Resource.Drawable.notification_brand)!;
        drawable.SetBounds(0, 0, size, size);
        drawable.Draw(canvas);
        return bitmap;
    }

    private static NotificationManager Manager(Context context) => (NotificationManager)context.GetSystemService(Context.NotificationService)!;
    private static AlarmManager Alarms(Context context) => (AlarmManager)context.GetSystemService(Context.AlarmService)!;

    private static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        // Default importance allows lock-screen display; silence is explicit.
        // Android retains the user's channel preferences on subsequent calls.
        using var channel = new NotificationChannel(ChannelId, "Daily tasks", NotificationImportance.Default)
        {
            Description = "A silent summary of today's remaining tasks.",
            LockscreenVisibility = NotificationVisibility.Public
        };
        channel.SetSound(null, null);
        channel.EnableVibration(false);
        channel.EnableLights(false);
        channel.SetShowBadge(false);
        Manager(context).CreateNotificationChannel(channel);
    }

    private static void ScheduleNextDay(Context context, bool enabled)
    {
        var pending = PendingIntent.GetBroadcast(context, 102,
            new Intent(context, typeof(DailyNotificationReceiver)).SetAction(MidnightAction),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        var alarm = Alarms(context);
        if (!enabled) { alarm.Cancel(pending); return; }
        var when = DailyNotificationSchedule.NextMidnight(DateTimeOffset.Now, TimeZoneInfo.Local).ToUnixTimeMilliseconds();
        if (CanScheduleExactly)
        {
            try
            {
                alarm.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, when, pending);
                return;
            }
            catch (Java.Lang.SecurityException) { /* Permission can change between check and scheduling. */ }
        }
        alarm.SetAndAllowWhileIdle(AlarmType.RtcWakeup, when, pending);
    }

    internal static void OpenNotificationSettings()
    {
        EnsureChannel(Context);
        var intent = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Intent(Android.Provider.Settings.ActionChannelNotificationSettings)
                .PutExtra(Android.Provider.Settings.ExtraAppPackage, Context.PackageName)
                .PutExtra(Android.Provider.Settings.ExtraChannelId, ChannelId)
            : new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings, Android.Net.Uri.Parse("package:" + Context.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        Context.StartActivity(intent);
    }

    internal static void OpenAlarmSettings()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31)) return;
        var intent = new Intent(Android.Provider.Settings.ActionRequestScheduleExactAlarm,
            Android.Net.Uri.Parse("package:" + Context.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        Context.StartActivity(intent);
    }
}

[BroadcastReceiver(Name = "com.doingit.app.DailyNotificationReceiver", Exported = false)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced, Intent.ActionDateChanged,
    Intent.ActionTimeChanged, Intent.ActionTimezoneChanged, AlarmManager.ActionScheduleExactAlarmPermissionStateChanged])]
public sealed class DailyNotificationReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        if (intent?.Action is Intent.ActionTimezoneChanged or Intent.ActionTimeChanged) TimeZoneInfo.ClearCachedData();
        // AlarmManager's wake lock covers OnReceive only. Bound our asynchronous
        // disk read/post so a sleeping phone can finish the silent refresh.
        var power = (PowerManager)context.GetSystemService(Context.PowerService)!;
        var wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "DoingIt:DailyNotification")!;
        wakeLock.SetReferenceCounted(false);
        wakeLock.Acquire(10_000);
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try { await DailyNotifications.RefreshAsync(context, intent); }
            finally
            {
                if (wakeLock.IsHeld) wakeLock.Release();
                wakeLock.Dispose();
                pending?.Finish();
            }
        });
    }
}
