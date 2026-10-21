using DoingIt.AndroidNotifications;

namespace DoingIt.Pages;

public sealed class NotificationSettingsPage : ContentPage
{
    private readonly Switch _daily = new() { AutomationId = "DailyNotification", OnColor = Ui.Teal, ThumbColor = Ui.Teal };
    private readonly Switch _sticky = new() { AutomationId = "StickyNotification", OnColor = Ui.Teal, ThumbColor = Ui.Teal };
    private readonly Label _status = Ui.Text("", 13, Ui.Muted);
    private readonly Button _permissions;
    private readonly Button _timing;
    private bool _refreshing;
    private bool _saving;

    public NotificationSettingsPage()
    {
        Ui.ConfigurePage(this);
        _status.AutomationId = "NotificationStatus";
        SemanticProperties.SetDescription(_daily, "Daily task notification");
        SemanticProperties.SetDescription(_sticky, "Keep notification all day");
        _permissions = Ui.Button("Notification & lock-screen settings", () => OpenSettings(DailyNotifications.OpenNotificationSettings), false, "NotificationPermissions");
        _timing = Ui.Button("Allow delivery at midnight", () => OpenSettings(DailyNotifications.OpenAlarmSettings), false, "NotificationTiming");
        var body = Ui.Stack(
            Ui.Card(Ui.Stack(Ui.Eyebrow("A LITTLE HELP FOR YOUR DAY"), Ui.Text("Today's tasks,\nat a glance.", 27, Ui.Teal, true),
                Ui.Text("A silent summary on your lock screen. Finish a task here and it leaves your notification too.", 14, Ui.Teal)), Ui.Blush),
            Ui.Card(Ui.Stack(
                Ui.Between(Ui.Stack(Ui.Text("Daily notification", 18, bold: true), Ui.Text("Today's remaining tasks, starting now and refreshing at 00:00 local time.", 13, Ui.Muted)), _daily),
                Ui.Rule(),
                Ui.Between(Ui.Stack(Ui.Text("Keep it all day", 18, bold: true), Ui.Text("Sticky mode brings it back if dismissed, and keeps an All done message until the next day.", 13, Ui.Muted)), _sticky))),
            _status, _timing, _permissions,
            Ui.Text("Task titles can appear on your lock screen. Your phone's notification and privacy settings control what is visible. Force-stopping the app pauses notifications until you open it again.", 12, Ui.Muted));
        body.Padding = new Thickness(24, 8, 24, 28);
        body.Spacing = 22;
        var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        var header = Ui.ModalHeader("Notifications", () => _saving ? Task.CompletedTask : ModalNavigation.CloseAsync(this));
        header.Margin = new Thickness(24, 12, 16, 12);
        root.Add(header);
        root.Add(new ScrollView { Content = body }, 0, 1);
        Content = root;
        RefreshState();
        _daily.Toggled += async (_, _) => await Save(true);
        _sticky.Toggled += async (_, _) => await Save(false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DailyNotifications.Updated += OnUpdated;
        RefreshState();
    }

    protected override void OnDisappearing()
    {
        DailyNotifications.Updated -= OnUpdated;
        base.OnDisappearing();
    }

    private void OnUpdated(object? sender, EventArgs args) => Dispatcher.Dispatch(() => { if (!_saving) RefreshState(); });

    private void RefreshState()
    {
        _refreshing = true;
        try
        {
            var options = DailyNotifications.Options;
            _daily.IsToggled = options.Enabled;
            _sticky.IsToggled = options.Sticky;
            _daily.IsEnabled = !_saving;
            _sticky.IsEnabled = options.Enabled && !_saving;
            _timing.IsVisible = options.Enabled && !DailyNotifications.CanScheduleExactly;
            _status.Text = !options.Enabled ? "Off. Turn on daily notifications to see today's tasks immediately."
                : !DailyNotifications.CanNotify ? "Notifications are blocked by Android. Allow Daily tasks in notification settings to show your summary."
                : !DailyNotifications.CanScheduleExactly ? "Your summary is on. Allow alarms & reminders for 00:00 delivery; without it, Android may deliver later."
                : "On · Silent · Refreshes at 00:00 local time";
        }
        finally { _refreshing = false; }
    }

    private async Task Save(bool requestPermissions)
    {
        if (_refreshing || _saving) return;
        var options = new NotificationOptions(_daily.IsToggled, _sticky.IsToggled);
        _saving = true;
        _daily.IsEnabled = _sticky.IsEnabled = false;
        try
        {
            if (requestPermissions && options.Enabled && OperatingSystem.IsAndroidVersionAtLeast(33))
                await Permissions.RequestAsync<Permissions.PostNotifications>();
            await DailyNotifications.SetOptionsAsync(options);
            if (requestPermissions && options.Enabled && DailyNotifications.CanNotify && !DailyNotifications.CanScheduleExactly)
                DailyNotifications.OpenAlarmSettings();
        }
        catch (Exception exception) { await Ui.Error(this, exception); }
        finally { _saving = false; RefreshState(); }
    }

    private async Task OpenSettings(Action open)
    {
        try { open(); }
        catch (Exception) { await DisplayAlertAsync("Open phone settings", "Open your phone's Settings, then Apps → Doing it, to change notification or alarm permissions.", "OK"); }
    }

    protected override bool OnBackButtonPressed() => _saving || ModalNavigation.HandleBack(this);
}
