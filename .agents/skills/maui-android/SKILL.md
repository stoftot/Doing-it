---
name: maui-android
description: Build, deploy and investigate MAUI Android apps using the opted-in shared AndroidTesting runner, explicit device ownership, app assertions, adb evidence and compatible C# source debugging. Use for Android-specific behavior or explicit MAUI toolchain work, not ordinary shared .NET logic alone.
---

# MAUI / Android adapter

Use with the relevant core workflow and [.NET adapter](../dotnet/SKILL.md). Read
[MAUI_ANDROID facts](../../../docs/ai/modules/MAUI_ANDROID.md) for projects, TFMs,
package IDs, build/run targets, deployment and device assumptions. Do not invent
identifiers or use prior scratch probes as application projects.

This repository opts into shared AndroidTesting in [AGENTS.md](../../../AGENTS.md#shared-android-testing-this-repository).
Read [project integration](../../../docs/android-testing.md), resolve the central
tool, read its README/version/relevant changelog and apply the single root review
marker's rules. Review improvements even when existing commands work. Only edit
project-owned integration; report shared-tool problems without modifying or copying
its implementation. For documentation work, use static checks and the read-only
revision checker, not services, device mutations or live environment tests.

## Testing support

Use the .NET adapter for existing shared-logic/unit/integration tests. Support
`plan-tests` / `create-tests` with device feasibility and existing UI test patterns;
build or shared-logic results do not prove navigation or device UI behavior.
When stable device automation is impractical, `create-tests` maintains reusable
manual cases. Execute cases selected by `validate-change` through the reserved
runner on shared emulators, or the separate physical-device route. Use the project
suites listed in the Android facts; do not substitute the
shared Settings/counter suites or claim a UI migration passed. Optional DevFlow
requires intentional instrumentation and does not bypass emulator reservations.
Coverage design stays in the core testing skills.

## Keep tool responsibilities separate

| Mechanism | Responsibility |
| --- | --- |
| `dotnet` / MAUI workload | Build/restore using actual project targets |
| Android SDK / `adb` | Device, package, process, log and platform operations |
| Shared AndroidTesting / Appium | Reserved emulator workers, sessions/ports, native UI assertions and diagnostics |
| `netcoredbg-mcp` | C# source debugger, subject to actual runtime compatibility |
| Rider MCP / MAUI integration | IDE semantics, project/configuration information |
| DevFlow, if enabled | Running MAUI application/UI interaction |
| Android Emulator, if installed | Optional target; a physical device also works |

OpenJDK 21 is build infrastructure. Interact with Java/JDK tooling directly only
to diagnose toolchain configuration; its presence is not a reason to add Java code.

## Build and discover targets

1. Detect the SDK, installed MAUI Android workload, JDK and Android SDK configuration.
   Read installed help and actual target configuration as needed. Use the existing
   [environment helper](../../../tooling/enter-env.ps1) and
   [tooling guide](../../../docs/tooling.md) for local path/setup details. Do not run
   setup/update installers or repair the shared environment as project maintenance.
2. Build once using the canonical project wrapper/TFM/configuration. Preserve the
   Windows Unicode-path workaround and inspect APK ABI compatibility. Check
   output/exit status and actual deployment artifact. A successful build establishes
   compilation; it says nothing yet about device execution.
3. For actual device work, inspect shared device/resource/reservation listings and
   `adb devices -l`. Inspect authorization/state and reuse existing targets; do not
   assume availability from an old report. These are not documentation-only checks.
4. With multiple targets, select by explicit user preference or documented criteria;
   otherwise resolve the ambiguity before deployment. Use the chosen serial with
   `adb -s` on subsequent target-specific operations. Report offline/unauthorized
   targets as unavailable until resolved.
5. Use `Invoke-AndroidTests.ps1` in PowerShell 7 to reserve the selected existing
   AVDs and start/reuse services for authorized tests. No device config means the
   shared fallback; no suite means a Settings smoke, not a Doing it test. Always
   supply a real project suite. Never auto-create devices/install images, override
   reservations or memory safeguards, or reset/delete data to remove a test gap.

## Deploy and collect platform/runtime evidence

1. Identify the actual package/application ID and entry activity from project,
   manifest and built artifact. Shared emulator tests deploy through the runner;
   it resolves the launcher activity and serial. Physical phones use the separate
   explicit-serial route in the project facts; the shared runner does not support them.
2. Consider existing app data before reinstalling/resetting/uninstalling. Do not
   clear data as a speculative first fix. Package/process control must target the
   intended app and support the authorized reproduction.
3. Launch through the actual project/platform entry and verify the expected package
   and process are running. Reproduce the affected behavior on the selected target.
4. Gather bounded `adb logcat` output, preferably scoped by package process, tags,
   priority or time interval. Preserve the relevant exception/context instead of
   clearing the entire log. Inspect process/package/device state with supported
   `adb shell` operations when it answers the current hypothesis.
5. Use file transfer, port forwarding/reverse, package/process control, or connection/
   reconnection operations only when the scenario requires them. Record task-owned
   changes transiently so they can be undone; do not disrupt other targets.
6. Feed platform evidence into `debug-problem`. Separate Android lifecycle/network/
   permission/deployment issues from C# state or UI behavior before choosing a fix.

## Reserved UI suite contract

- Managed suites read `ContextPath` and use the worker's `SessionUrl`; do not create
  another session or delete the worker's session. Throw on assertion failure. External
  harnesses explicitly read `ANDROID_TEST_CONTEXT`, use full context capabilities
  and `AppiumUrl`, own one driver and dispose it in teardown. Disable framework
  parallelism inside a worker; the runner owns device concurrency.
- Keep the allocated serial and all system/MJPEG/Chromedriver/WebView ports. Pass
  delegated `AvdName`, `Serial` and `ReservationId` to supporting utilities and use
  `adb -s` for any necessary native calls. Never use raw ADB to bypass a busy device.
- Assert Doing it state transitions from the existing manual cases, not just taps
  or package presence. Use run-unique fixtures, baseline-aware counts and actual
  page-source locators. Preserve unrelated app data; skip unmet fresh-state cases.
- Capture screenshot, page source, Appium/session logs, unfiltered crash logcat and
  per-worker results under its `OutputDirectory`. External harnesses capture before
  disposing their session. Follow [integration details](../../../docs/android-testing.md)
  for ownership, diagnostics and recovery; do not copy central worker scripts.

## C# source debugging and IDE information

Use the .NET adapter's NetCoreDbg build/launch-or-attach/breakpoint/inspection flow
when source-level state is uncertain. **First establish compatibility with the
actual Android runtime and transport.** Desktop CoreCLR acceptance does not prove
MAUI Android support; do not promise that an Android process can be attached to by
this controller. If unsupported, state that source debugging is unavailable and
use targeted tests/platform logs or request the missing compatible capability.

Rider supplies semantic/project/configuration information when useful; do not
assign debugger control to it by assumption. Do not treat either IDE build success
or device process presence as proof of a C# debug session.

## Optional DevFlow UI interaction

Detect an intentionally enabled Microsoft .NET MAUI DevFlow MCP connection and
required running-app instrumentation; do not assume installation or instrument the
app just because this skill is loaded. Inspect the exposed capabilities first.

When supported, inspect UI trees/state, identify elements, tap/click, enter text,
capture screenshots, inspect app logs, and examine selected app/runtime/network
state. Confirm expected app/UI outcomes after interaction.

DevFlow is not the C# source debugger. It does not substitute for source breakpoints,
C# stepping, stack-frame inspection, arbitrary local-variable inspection, or
debugger expression evaluation. Missing DevFlow does not prevent build/adb work;
report any UI verification that could not be performed.

## Complete the evidence handoff

After a fix, rerun the original device scenario plus relevant regression tests
through `validate-change`. Report build outcome, selected target/runtime, deployment,
launch, actual reproduced/verified behavior and any unperformed checks separately.
With no usable target, perform meaningful build/shared-logic checks and state
device/runtime verification unavailable; do not label the behavior verified.

Detach/stop task-owned debug sessions, remove task-owned forwarding and clean only
owned fixtures/processes. Let the runner verify session cleanup/release reservations;
retain blocked reservations on uncertainty. Do not delete locks, auto-repair leases,
stop shared services, reset data or run `adb kill-server`. Preserve unrelated app
data and user-owned devices/emulators/sessions. Report physical/OEM/API coverage
limits and any missing harness execution. Recheck tool revision before advancing
the root marker; retain its prior value if required validation is incomplete.
Return evidence to the core workflow for context and release assessment.
