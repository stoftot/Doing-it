# Doing it

A native .NET MAUI Android to-do app. Teal (`#1D4C5E`) and blush (`#D5BBB1`), a quiet place for today's tasks, everyday lists, and recurring routines.

- **Home:** today's dated tasks and active routines, followed by General and your custom lists. Completed tasks are hidden; each section's **done** button opens a popup with Undo.
- **Recurring:** create, edit, pause, and resume daily, weekly, and monthly routines. Weekly schedules select weekdays; monthly schedules select days and months. Nonexistent dates are skipped.
- **Lists:** create tasks and named lists. General is permanent. Removing a custom list moves its tasks into General.
- **Calendar:** shared month/week view from Home and Lists, with task creation, editing, completion, and undo for the selected date.
- **Task details:** tasks and routines have a required title that fits up to two lines, plus an optional description with no length limit. Rows show the title. Tap it to read the title, description and date/list or recurrence schedule in a popup, with the app still visible behind it. Tap outside, use its close button or press Back to dismiss it.
- **To-do list tasks:** choose **To-do list** in a new task or routine form, enter a title and subtasks, then save. Tap the list icon or progress control to open its checklist popup. Each check saves immediately; the row shows how many items are done and completes automatically when all are checked. Open a completed checklist and uncheck an item to reopen it. **Edit to-do list** changes its title, description and items; close the editor to cancel unsaved edits.
- **Android home-screen widgets:** long-press your launcher’s home screen, open Widgets and choose Doing it. **Tasks left today** (1×1) shows the remaining count; **Today’s progress** (2×1) shows completed/total. Both show a checkmark and **All done** when finished, or **No tasks today** on an empty day. Tap either to open Home. **Create task** (1×1) uses the app’s pencil icon and opens the task form with today selected; you can choose another date or a list.
- **Daily notification:** tap the bell on Home, then enable **Daily notification**. A silent summary appears immediately and refreshes at local midnight. Expand it to read remaining task titles and checklist progress; saved changes update it. **Keep it all day** restores swipe-dismissed notifications and retains an All done message until tomorrow. Tap the notification to open Home. With sticky mode off, dismissal lasts for that date.

A task belongs to either a list or one calendar date. List tasks stay in that list; dated tasks appear only on their date. Each recurring occurrence has its own completion state. Routines start on their creation date. Editing a routine changes its schedule from that start date; completion records are retained for matching dates.

Everything is stored locally: tasks in the app's private `planner.json`, with an atomic replacement and previous-save `.bak`, and notification preferences in Android private settings. No account, server, cloud sync or web UI is needed. Uninstalling the app can remove local data.

Daily notifications include the same dated tasks and active routines as Home, excluding list-only and completed tasks. They use the app's teal accent and blush checkmark within Android's expandable notification layout. Android limits visible text; long summaries indicate omitted tasks and open the full list on tap. Allow notifications and **Alarms & reminders** when prompted for exact 00:00 delivery, including while idle. Without alarm access, delivery can be delayed. Local midnight is recalculated after each delivery and time-zone change, including daylight saving. Reboot recovery starts after the phone is unlocked. Lock-screen privacy, disabled channels, force-stop, device shutdown and manufacturer restrictions remain under Android's control; sticky mode cannot override them. The app requests public lock-screen visibility and silence, while the phone's settings control final presentation and sound.

Widget totals match Home’s **Today’s tasks** section: dated tasks and active routines, with each checklist counted once. List-only tasks are excluded. All placed copies refresh after saved changes and when the app resumes. Background updates use a periodic refresh and a local-midnight alarm; Android may defer them during sleep or battery restrictions. No exact-alarm permission is required. Launcher grids determine the physical dimensions of each widget.

Recurring checklists have separate progress for each date. Renaming a subtask preserves its checkmarks; adding an item reopens completed occurrences until that item is checked. Removing an item removes its progress from the routine's history. The task type is chosen at creation.

Existing version 1 and 2 planner files load without losing tasks, subtasks or completions; the next save uses schema version 3. Short old descriptions become title-only tasks. Longer text stays intact in the description, with as many complete words as fit copied into the title. For a single word too long to fit, the title uses a prefix without splitting Unicode characters. The original file remains untouched during loading and is kept as the backup at the next save.

## Build and run

Requires the .NET 10 SDK selected by `global.json`, `maui-android` workload, JDK 21 and Android SDK. See [tooling setup](docs/tooling.md) for the retained native tooling. No iOS, Windows, ASP.NET, or Blazor targets are included.

From PowerShell 7 at the repository root:

```powershell
./tooling/build-android.ps1

dotnet test tests/DoingIt.Core.Tests/DoingIt.Core.Tests.csproj
```

For shared emulator tests, follow [AndroidTesting integration](docs/android-testing.md).
It uses the existing environment and reserved runner. The project-owned
`tests/android/TaskDetails.ps1` Managed suite covers task text and read-only popups;
`tests/android/DailyNotifications.ps1` covers notification settings and live updates.
Select the intended suite explicitly. The default Settings smoke is not an app regression test.

For a separately coordinated, authorized physical phone (not a reserved emulator):

```powershell
adb devices -l
adb -s <device-serial> install -r src/DoingIt/bin/Debug/net10.0-android/com.doingit.app-Signed.apk
adb -s <device-serial> shell monkey -p com.doingit.app -c android.intent.category.LAUNCHER 1
```

`build-android.ps1` imports the local SDK environment and temporarily maps a free drive letter when Windows paths contain non-ASCII characters. This avoids Android AAPT2's Unicode-path limitation without moving source. The mapping is removed after the build. `-Configuration Release` builds the release configuration; distribution signing is separate.

The app targets Android API 36 with Android 6.0 (API 23) as its minimum. Debug APKs embed their assemblies so they can be installed directly with adb. The application ID is `com.doingit.app`.

## Source and checks

- [Project map](docs/ai/PROJECT_MAP.md) and [architecture](docs/ai/ARCHITECTURE.md).
- `src/DoingIt`: native MAUI pages and Android entry point.
- `src/DoingIt.Core`: scheduling, validation, and local persistence, independent of MAUI.
- `tests/DoingIt.Core.Tests`: deterministic scheduling and storage tests.
- [Device regression cases](tests/manual/planner.md).
- [Shared Android test integration](docs/android-testing.md) and [verification scope](docs/verification.md#android-project-verification).
- [Pending release](releases/0.1.0.md).

The repository retains the [.NET and MAUI development workflows](docs/ai/README.md). Shared machine tools are managed separately from this project.
