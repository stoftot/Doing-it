# Shared Android testing for Doing it

The repository opts in through [AGENTS.md](../AGENTS.md#shared-android-testing-this-repository).
The shared tool's README owns its command contracts; this document maps them to
Doing it. [Android facts](ai/modules/MAUI_ANDROID.md) owns app/build identities,
and [verification](verification.md#android-project-verification) owns acceptance.
Do not run the runtime examples merely to verify documentation.

## Discover and review the shared tool

Use PowerShell 7 on Windows for the runner. Resolve the tool without copying
it or modifying persistent environment settings, from the repository root:

```powershell
$androidTestingRoot = $env:ANDROID_TESTING_HOME
if ([string]::IsNullOrWhiteSpace($androidTestingRoot)) {
    $androidTestingRoot = [Environment]::GetEnvironmentVariable('ANDROID_TESTING_HOME', 'User')
}
if ([string]::IsNullOrWhiteSpace($androidTestingRoot)) {
    $androidTestingRoot = 'C:\Users\chris\DevTools\AndroidTesting' # This host only.
}
foreach ($file in @('README.md', 'version.json', 'CHANGELOG.md',
                   'Get-AndroidTestingChanges.ps1', 'Invoke-AndroidTests.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $androidTestingRoot $file) -PathType Leaf)) {
        throw "AndroidTesting is unavailable at '$androidTestingRoot': missing $file. Report the setup gap."
    }
}
$androidTestingRoot = (Resolve-Path -LiteralPath $androidTestingRoot).Path
Get-Content -LiteralPath (Join-Path $androidTestingRoot 'README.md')
Get-Content -LiteralPath (Join-Path $androidTestingRoot 'version.json')
& "$androidTestingRoot\Get-AndroidTestingChanges.ps1" -AsObject
```

Read the one marker in root AGENTS.md. With `UNREVIEWED`, inspect the full
changelog using `Get-AndroidTestingChanges.ps1 -SinceRevision 0`; zero is a query
sentinel, not a reviewed revision. With a valid integer differing from current,
pass that exact integer to `-SinceRevision`. Inspect the selected notes and
referenced README sections, including useful workflow improvements. Invalid,
future or inconsistent metadata requires investigation of the path/record, never
a lowered marker. `Current` only describes metadata; it does not certify tests.
Recheck before recording completion and leave the marker unchanged if required
project validation is pending.

The revision 1 runner improves the documented manual ADB deployment workflow with
reservations, port allocation and per-device diagnostics. Revision 2's
`nothing-phone-3a` preset and optional refresh-rate setting do not require a new
device or any change to our existing fallback. Its OEM/display limitations apply
to our layout evidence. Shared revisions are independent of app releases.

## Existing targets and project configuration

There is currently no project `android-testing.json` or registered Doing it project
GUID/device. `tests/android/TaskDetails.ps1` is a project-owned Managed suite for
task titles, descriptions and read-only details popups. Use the existing `MAUI_Shared_Android` fallback
for authorized emulator work. Do not use the separate AndroidTestingValidation
device or claim its project GUID. Current live serials, reservations and physical
phone availability are unknown until runtime discovery; do not save a serial.

The shared AVD configuration uses API 36 Google APIs x86_64, 1080 x 2392,
387 DPI, 120 Hz VSYNC, 4096 MB RAM and four cores. These are configuration values,
not observed performance or a real Nothing OS environment. See coverage limits below.

For an actual test run, after reviewing the shared README, inspect existing
devices/resources/reservations using its `Get-AndroidDevices.ps1`,
`Get-AndroidResources.ps1` and `Get-AndroidReservations.ps1`. These runtime helpers
may initialize the environment or contact ADB; use file inspection for documentation
work. Reuse available targets and wait if busy. Never automatically register/create
an AVD, install an image, override memory safeguards or stop another project's device.

If independently owned devices and suites are added in later authorized work,
keep the optional config in this repository using the shared README's schema:

| Field | Project rule |
| --- | --- |
| `ProjectId` | Actual returned registration GUID; omit for shared-only use |
| `Devices` / `DefaultDevices` | Saved `AvdName` values and unique profile labels for existing devices |
| `Suites` / `DefaultSuites` | Real project suites with assertions, not placeholders or a Settings smoke test |
| `Application.PackageName` | `com.doingit.app` |
| `Application.ApkPath` | Actual current signed APK, relative to the config directory |

Do not add a runnable config with no app suite: the runner falls back to its
Settings smoke test when no suite is selected. That cannot validate Doing it.
Config selects devices and suites; it never creates devices. For multiple existing
targets, build once and use `-DeviceProfile`, `-SuiteName` and `-ThrottleLimit` as
documented centrally. Each selected device runs its suites sequentially; separate
invocations with disjoint devices share the reservation/port allocator.

## Build once, then use the reserved runner

Preserve `tooling/build-android.ps1` and its Unicode-path workaround. The app's
current project has no explicit RID; the existing Debug signed APK contains
`arm64-v8a` and `x86_64` native libraries. Inspect the fresh artifact on future
runs; the shared README's RID-specific example output path is not our build path.
The shared x86_64 emulator needs x86_64 libraries (`android-x64` when choosing a
single RID); a typical physical ARM64 phone needs `arm64-v8a` (`android-arm64`).
Use the existing embedded-assembly APK settings; stop on build failure and never
deploy an older artifact as the new build.

Use the task-details Managed suite explicitly; it creates run-unique fixtures and
removes only its own data through the app. The guard prevents falling through to
a misleading Settings run. Resolve `$androidTestingRoot` as above first.

```powershell
$ErrorActionPreference = 'Stop'
$plannerSuite = Join-Path (Get-Location).Path 'tests/android/TaskDetails.ps1'
if (-not (Test-Path -LiteralPath $plannerSuite -PathType Leaf)) {
    throw 'The Doing it task-details Managed suite is missing.'
}
& ./tooling/build-android.ps1
if ($LASTEXITCODE -ne 0) { throw 'Build failed; do not deploy an older APK.' }
$apk = (Resolve-Path -LiteralPath 'src/DoingIt/bin/Debug/net10.0-android/com.doingit.app-Signed.apk').Path
$runOutput = Join-Path (Get-Location).Path ('.tooling/android-tests/' + [guid]::NewGuid().ToString('N'))
& "$androidTestingRoot\Invoke-AndroidTests.ps1" -AvdName MAUI_Shared_Android `
    -ApkPath $apk -PackageName com.doingit.app -SuiteScript $plannerSuite `
    -SessionMode Managed -ThrottleLimit 1 -OutputDirectory $runOutput
```

The runner reserves before deployment, starts/reuses services, resolves the actual
serial and launcher activity, and installs the new APK. Do not hard-code MAUI's
generated activity name or launch/install via raw ADB on a reserved emulator.
Do not invoke internal workers directly. Never treat a default Settings or shared
sample-counter pass as a project test pass.

## Session and port ownership

| Mode | Contract for the project suite |
| --- | --- |
| `Managed` (preferred starting point) | PowerShell suite accepts `param([string]$ContextPath,[hashtable]$TestParameters=@{})`, reads JSON and uses the worker's `SessionUrl`. The worker creates and deletes the session. Do not create a second driver or delete the worker's session. Throw on failed assertions; do not call `exit` or leave background tests running. |
| `External` (future .NET UI harness) | Worker reserves device/ports but creates no session. Harness explicitly reads `ANDROID_TEST_CONTEXT`, `ANDROID_SERIAL`, `APPIUM_SERVER_URL`, and context `AppiumUrl`/full `Capabilities`. Create one driver per worker, capture diagnostics before disposal, and dispose it in `finally`/teardown. Disable in-worker framework parallelism. |

Use all allocated capabilities: `appium:udid`, `appium:systemPort`,
`appium:mjpegServerPort`, `appium:chromedriverPort`, `appium:webviewDevtoolsPort`.
Do not substitute fixed serials or ports. Pools are 20000..21999 (system),
22000..23999 (MJPEG), 24000..25999 (Chromedriver), 26000..27999 (WebView).
Appium uses the context's loopback root URL (normally port 4723, no `/wd/hub`).
Appium 3 session discovery is `/appium/sessions`. Preserve `noReset=true` and
`forceAppLaunch=true`. This native app has no WebView; allocated web ports do not
imply browser automation coverage.

Native ADB operations inside a suite must use `-s $context.Serial` and check native
exit status. Shared utilities supporting reservations receive `-AvdName`, `-Serial`
and delegated `-ReservationId` from context. Use `OutputDirectory` for worker results.
For a future .NET harness, the central `suites/DotnetTests.ps1` adapter accepts the
UI-test project, configuration and filter and uses `dotnet test --no-build --no-restore`;
build that harness once first. Resolve the adapter's absolute path from the tool
root rather than copying it. Our existing core xUnit project is not that harness.
The shared tool documents the External .NET adapter as untested.

## Assertions, diagnostics and cleanup

Implement cases from [planner regressions](../tests/manual/planner.md), with
run-unique `QA` names and cleanup of only the test's own items. Useful initial
checks are PLANNER-003 (edit/complete/Undo and count/row changes), PLANNER-004
(date/list separation), and PLANNER-009 (process restart preserves each item once
and its completion state). Then cover recurrence boundaries and per-date Undo
(006/008), invalid saves (007), navigation/calendar (001/005), and list migration
(002). Existing `TabHome`, `TabRecurring`, `TabLists`, `TaskDescription`,
`TaskListPicker`, `TaskDatePicker` and calendar action IDs are starting points;
inspect their actual Appium page-source exposure before choosing locators. IDs in
C# alone do not prove UI automation works. Assert state changes and absence of
duplicates, not just launch, a screenshot or successful taps.

Fresh-state case 001 requires an already empty dedicated test installation or
separate explicit reset authorization. Do not clear the shared app's `planner.json`,
its backup, app data or AVD data to satisfy a precondition. For existing-data cases,
account for baseline counts and preserve all unrelated lists/tasks/routines.

Keep run summary, per-device result/context, screenshot, page source, worker logs,
Appium session/log references and unfiltered logcat on failure. The Managed worker
collects diagnostics and closes its session; External harnesses must capture page
source/session diagnostics before disposing their own driver. Use the shared
capture/log utilities with delegated identity inside a suite; after a crash omit
the package filter so terminated-process evidence is retained. Do not clear logcat.
Project artifacts belong under ignored `.tooling/android-tests/`, never in commits.

Let the runner verify cleanup and release reservations. If cleanup is uncertain,
retain the blocked reservation and inspect the exact owner/process/session/logs;
never delete lock files, bypass the runner with raw ADB, or automatically invoke
reservation repair. Recovery is separate explicit work following the shared README.
Close only task-owned test/debug sessions and remove only task-owned forwarding
and fixtures. Leave shared Appium/ADB/emulators running; any deliberate later stop
must respect active reservations/sessions and ownership. Never kill all Node/ADB
processes, run `adb kill-server`, wipe, uninstall or reset as routine cleanup.

## Physical phones and coverage limits

The shared runner/reservation tools support emulator serials only. Keep physical
phones on a separate explicit-serial manual ADB/Appium route, with user authorization,
compatible APK ABI and device access coordinated before deployment. Do not pass a
phone to `Invoke-AndroidTests.ps1`. There is no physical-device Appium harness here.
The [root README](../README.md#build-and-run) retains manual phone installation.

Record the actual phone model, Android/OEM version, API, build fingerprint, security
patch, logical size/density (including overrides), navigation mode, font/display
scale and locale with test evidence. Nothing Phone (3a) display approximation is
not Nothing OS: physical 387 PPI is not measured Android logical DPI, and 120 Hz
VSYNC does not establish adaptive refresh/performance. Actual Nothing OS requires
a physical Phone (3a); no community image/port is provisioned or validated here.

API 36 emulator results cannot prove API 23 minimum support, other OEMs, physical
keyboard/back/insets/accessibility behavior or all screen sizes. Case 010 needs
real layout/interaction assessment and restoration of any deliberately changed
device settings. Launcher widgets need separate background date/boot/idle checks
and OEM launcher coverage; see PLANNER-012 through PLANNER-015. The app has no
notifications or OEM hardware features. Report every unexecuted platform/case explicitly.
