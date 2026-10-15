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

## Build and deployment

Build with `./tooling/build-android.ps1` from PowerShell 7. The script imports SDK/JDK environment discovery and aliases a non-ASCII Windows checkout path for AAPT2. It does not move source or install tools. Output is `src/DoingIt/bin/Debug/net10.0-android/com.doingit.app-Signed.apk`; Release uses the corresponding Release directory.

Debug assemblies are embedded in the APK, enabling direct adb installation. This is a development-signed package; store distribution and release signing have not been configured.

Discover the device before every workflow: `adb devices -l`. Select the authorized connected physical phone; if several are available, explicitly choose the intended serial. Device serials are machine-local, not project configuration.

```powershell
adb -s <serial> install -r src/DoingIt/bin/Debug/net10.0-android/com.doingit.app-Signed.apk
adb -s <serial> shell monkey -p com.doingit.app -c android.intent.category.LAUNCHER 1
adb -s <serial> shell pidof com.doingit.app
```

No backend, forwarding, account, or emulator is required. Existing app data must be preserved on reinstall. `planner.json` and its previous-save `.bak` live in FileSystem.AppDataDirectory. Fresh app state has General and no sample tasks.

## Checks and optional tools

Core tests run independently of a device. [Manual planner cases](../../../tests/manual/planner.md) cover actual native interactions, date/list separation, recurrence, persistence, accessibility and keyboard behavior. adb UI hierarchy, input and screenshots can supply device evidence; build/test success alone does not.

DevFlow and Android C# debugger instrumentation are not enabled. No emulator is part of the app's configuration. Detect compatible optional tooling before use.
