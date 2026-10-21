# Daily notification regressions

These cases cover notification behavior on Android. Use a device whose existing
app data and Android settings can be preserved. Do not
clear planner data or change the device clock to run these cases. Restore every
changed notification option and Android permission to its recorded starting state.

## NOTIFY-001 — Enable the daily summary and handle permissions

### Purpose
Verify that enabling the feature requests Android notification access and starts
the summary immediately, while exact-alarm access remains an optional timing
improvement.

### Preconditions
- Record whether Daily notification and Keep it all day are enabled.
- Record app notification permission, the Daily tasks channel state, and exact
alarm access. Use a test device where app notification settings may be safely
changed and restored.
- Have one incomplete task scheduled for today with a distinctive title.

### Steps
1. In Doing it Home, open the bell notification settings and turn on Daily
   notification.
2. If Android 13 or later asks for notification permission, allow it. If Android
   opens Alarms & reminders settings, record the starting state and decide whether
   to allow exact alarms for this test.
3. Return to Doing it and inspect the notification shade and lock screen. Expand
   the summary to read the task list.
4. Open the notification settings page again and inspect its status and delivery
   controls.

### Expected result
- The notification appears immediately with today's remaining count and task
  title, including after returning from Android settings.
- The notification is silent and visible on the lock screen according to the
  device's privacy settings.
- If notifications are blocked, Doing it explains that Android is blocking them;
  it does not show an empty-day summary as if delivery succeeded.
- When exact alarms are unavailable, the app explains that midnight delivery
  might be delayed and offers the Android alarm settings entry point.
- Restore both app switches and every Android permission or channel setting to
  their recorded starting state.

## NOTIFY-002 — Task completion removes its line and Undo restores it

### Purpose
Protect the live relationship between today's planner state and the notification.

### Preconditions
- Daily notification is enabled and Android permits notifications for Doing it.
- Today has at least one incomplete task with a distinctive title; record other
  task counts so unrelated data can be recognized.

### Steps
1. Confirm the notification shows the task title and remaining count.
2. Complete the task in Doing it Home.
3. Inspect the notification, then open Completed and choose Undo for that task.
4. Inspect the notification again.

### Expected result
- Completing the task removes its title from the notification and updates the
  remaining count. If it was the only task, the summary says all tasks are done.
- Undo restores the task and its title to the notification.
- Other tasks, lists, routines, and their completion state remain unchanged.

## NOTIFY-003 — Sticky mode survives dismissal, taps, and an all-done day

### Purpose
Verify that sticky mode keeps an ongoing summary available after a user dismisses
it, while its tap opens Doing it and completion remains meaningful.

### Preconditions
- Daily notification is enabled, with at least one incomplete task for today.
- Record whether Keep it all day was initially enabled and any Android channel or
  lock-screen privacy settings that will be changed.

### Steps
1. Turn on Keep it all day and inspect the notification shade.
2. Swipe the notification away and inspect whether the system briefly removes
   the row before Doing it restores it.
3. Tap the notification and confirm Doing it opens to today's planner.
4. Complete all of today's tasks, then inspect the notification and dismiss it if
   Android permits the gesture.
5. Restore the initial app switches and any changed Android settings.

### Expected result
- The notification is marked ongoing. When Android allows a swipe dismissal while
  unlocked, the row disappears and Doing it restores it immediately through the
  notification delete action, without requiring an app refresh.
- Tapping it opens Doing it without duplicating or changing planner items.
- When all tasks are complete, the summary says All done for today and sticky mode
  keeps that state until the next local day.
- Lock-screen visibility follows Android channel and device privacy settings.

## NOTIFY-004 — Normal dismissal stays dismissed for the local day

### Purpose
Ensure that a normal notification's delete action is remembered across planner
refreshes and app process restart.

### Preconditions
- Daily notification is enabled and Keep it all day is off.
- At least one incomplete task is scheduled for today.

### Steps
1. Swipe the summary away in the Android notification shade.
2. Refresh Home by leaving and returning, then reopen the shade.
3. Close Doing it from Recents, launch it again, and inspect the shade once more.
4. Turn sticky mode on and off, then inspect the shade.

### Expected result
- The dismissed summary does not return during the current local date after Home
  refresh, process restart, or task editing.
- Changing notification options starts a new notification generation; after
  explicitly changing the setting, the summary may appear again.

## NOTIFY-005 — Midnight schedule follows local time, DST, reboot, and process death

### Purpose
Check that the next refresh is scheduled at the next local midnight and is
re-established after Android lifecycle and timezone events.

### Preconditions
- Use a dedicated test device and a time window near a naturally occurring
  timezone or daylight-saving transition, or inspect the scheduled alarm without
  altering the device clock.
- Record the device timezone and exact-alarm permission state.

### Steps
1. Enable Daily notification and inspect Android's scheduled alarm for Doing it.
2. Force-stop Doing it, relaunch it, and inspect that the summary and alarm remain
   correct after launch refresh.
3. Reboot the device, unlock it once, then inspect the summary and next alarm.
4. If an approved test device is available in a second timezone, change its
   timezone through Android settings, then restore it and repeat the inspection.
5. On Android 13 or later, revoke Doing it's notification permission in Android
   app settings, reopen the app, verify the blocked state, then restore the
   permission and verify delivery resumes.

### Expected result
- The alarm targets the next midnight in the device's current local timezone,
  accounting for a daylight-saving gap or repeated time.
- App launch, reboot, date/time changes, timezone changes, and restored exact
  alarm access refresh the notification and schedule the next midnight.
- Revoking notification permission prevents delivery and produces a clear blocked
  status; restoring permission allows the summary to resume.
- Force-stopping pauses notifications until Doing it is launched again.
- Do not change a shared device clock or timezone solely to execute this case.

## NOTIFY-006 — Long lists, load errors, channel settings, and visual styling

### Purpose
Protect the notification's expansion, truncation, data-loss safeguards, Android
channel behavior, and visual treatment.

### Preconditions
- Use disposable test data with enough today's tasks to exceed the notification
  body's display limit; keep unrelated planner data intact.
- Capture the starting Android channel sound, vibration, importance, visibility,
  and badge settings.

### Steps
1. Enable the notification and inspect the collapsed and expanded shade views.
2. Verify task lines, omitted-task count, tap behavior, lock-screen appearance,
   accent/icon styling, sound, vibration, and badge behavior.
3. On a disposable test installation, simulate an unreadable planner file only
   through a separately approved corruption/recovery procedure; inspect the
   resulting notification and confirm saved data is not overwritten.
4. Restore the planner and Android channel settings, disable notifications, and
   verify the active summary and midnight alarm are removed.

### Expected result
- The expanded body previews up to six remaining tasks and a clear omitted-items
  line for the remainder. Android may further restrict visible text at large font
  sizes; tapping opens Home with the complete list.
- The summary uses the Doing it icon and accent color, is silent, does not vibrate
  or create a badge, and honors Android's lock-screen privacy controls.
- A planner load failure shows an unavailable/retry message and preserves the
  saved planner instead of presenting a false empty or all-done day.
- Turning Daily notification off cancels the active notification and pending
  midnight alarm. Restore the original Android channel settings afterwards.
