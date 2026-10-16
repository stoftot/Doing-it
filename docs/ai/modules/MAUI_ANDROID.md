# MAUI / Android project facts

| Fact | Value |
| --- | --- |
| Project | `src/DoingIt/DoingIt.csproj` |
| TFM | `net10.0-android` |
| Target/minimum API | 36 / 23 (Android 6.0+) |
| Package ID | `com.doingit.app` |
| Display name/version | Doing it / 0.1.0, version code 1 |
| Entry | `Platforms/Android/MainActivity.cs` -> MainApplication -> MauiProgram -> App |
| Native UI | C# MAUI controls, modal editors and shared calendar; no WebView/Blazor |
| SDK | `global.json`: .NET 10.0.402, latestPatch roll forward |
| Shared emulator route | Explicit opt-in in root AGENTS.md; [integration guide](../../android-testing.md) |
| Project UI harness/device config | None: no Appium test project/suite or `android-testing.json` |

## Build and deployment

Build with `./tooling/build-android.ps1` from PowerShell 7. The script imports SDK/JDK environment discovery and aliases a non-ASCII Windows checkout path for AAPT2. It does not move source or install tools. Output is `src/DoingIt/bin/Debug/net10.0-android/com.doingit.app-Signed.apk`; Release uses the corresponding Release directory.

Assemblies are embedded in the APK. There is no explicit project RID; the existing Debug signed APK contains `arm64-v8a` and `x86_64` native libraries. Verify the fresh output/ABI for each test run rather than assuming the central README's `android-x64` output subdirectory. This is a development-signed package; store distribution and release signing have not been configured.

For emulator testing use the shared `Invoke-AndroidTests.ps1` runner with the existing `MAUI_Shared_Android` AVD and a real Doing it suite, as described in the integration guide. The runner reserves devices/ports, deploys and resolves the launcher activity. No Doing it-owned device is registered; do not borrow the tool's validation device or create one automatically. No project UI suite exists yet, so runner/app validation remains pending.

For an authorized physical phone, discover it with `adb devices -l` and choose the intended serial before deployment. Device serials are machine-local, not project configuration. The shared runner/reservations currently accept emulators only. The following manual commands are for that separately coordinated phone, not a way to bypass an emulator reservation:

```powershell
adb -s <serial> install -r src/DoingIt/bin/Debug/net10.0-android/com.doingit.app-Signed.apk
adb -s <serial> shell monkey -p com.doingit.app -c android.intent.category.LAUNCHER 1
adb -s <serial> shell pidof com.doingit.app
```

No backend, forwarding or account is required. A physical device remains an alternative to the shared emulator. Existing app data must be preserved on reinstall. `planner.json` and its previous-save `.bak` live in FileSystem.AppDataDirectory. Fresh app state has General and no sample tasks; existing shared installations must not be cleared to obtain it.

## Checks and optional tools

Core tests run independently of a device. [Manual planner cases](../../../tests/manual/planner.md) cover actual native interactions, date/list separation, recurrence, persistence, accessibility and keyboard behavior. adb UI hierarchy, input and screenshots can supply device evidence; build/test success alone does not.

Source hooks include `TabHome`, `TabRecurring`, `TabLists`, `TaskDescription`, `TaskListPicker`, `TaskDatePicker`, `MonthView` and `WeekView`, plus task completion semantic descriptions. Actual Appium locator exposure is unverified. Core tests target `net10.0` and do not create an AndroidDriver or consume runner context.

DevFlow and Android C# debugger instrumentation are not enabled. The shared API 36 Google APIs emulator approximates a Nothing Phone (3a) display; it does not provide Nothing OS, measured physical-phone logical DPI, minimum API 23 coverage or OEM/hardware validation. Physical phone availability and live shared reservations must be discovered during actual device work. See [verification](../../verification.md#android-project-verification) for the remaining harness and runtime checks.
