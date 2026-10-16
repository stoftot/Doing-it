# Optional modules

These modules have no entries in the default MCP configuration or setup script.
Resolve current vendor releases when opting in; record the actual versions then.

## Diagnostics

One independent module: `dotnet-trace`, `dotnet-dump`, `dotnet-counters` for tracing,
postmortem analysis and runtime counters. Install only when explicitly selected:

```powershell
dotnet tool install dotnet-trace --tool-path .tooling/diagnostics
dotnet tool install dotnet-dump --tool-path .tooling/diagnostics
dotnet tool install dotnet-counters --tool-path .tooling/diagnostics
```

Use each executable's `--help`; validate against a scratch process. Update with
`dotnet tool update <name> --tool-path .tooling/diagnostics`. Uninstall with
`dotnet tool uninstall <name> --tool-path .tooling/diagnostics`. No MCP is required.
Sources: [trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace),
[dump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump),
[counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters).

## Headless static analysis

[InspectCode](https://www.jetbrains.com/help/resharper/InspectCode.html) is optional
because Rider and compiler analysis already cover ordinary development.

```powershell
dotnet tool install JetBrains.ReSharper.GlobalTools --tool-path .tooling/inspectcode
.tooling/inspectcode/jb.exe inspectcode YourSolution.sln --output=results.sarif
```

This vendor package also contains other commands; use InspectCode only, and keep
`dotnet format` as the formatter. Update/uninstall using `dotnet tool` with the same
tool path. No MCP registration or dedicated security tooling is added.

## MAUI DevFlow (experimental)

Follow the current [Microsoft DevFlow documentation](https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/devflow/mcp-server?view=net-maui-10.0)
and [official source](https://github.com/dotnet/maui-labs/tree/main/src/DevFlow).
Enabling it may require application instrumentation, which is outside this task.
Create its own module fragment, discover actual tools, allowlist only the UI/tree,
screenshot or logging calls actually needed, set output caps, then verify on a
device. It does not replace source debugging with NetCoreDbg. Remove its MCP entry
before uninstalling its CLI and any explicitly added application instrumentation.

## Shared Android emulator and Appium

This project opts into the already installed AndroidTesting environment; see
[integration](android-testing.md) for discovery, review and reserved runner usage.
Reuse `MAUI_Shared_Android` unless existing project-owned devices are explicitly
configured later. Do not create devices automatically or install another SDK,
emulator, system image, Appium server or UiAutomator2 driver. Device creation,
reset/deletion and shared-tool maintenance are not project startup/cleanup actions.
Report external setup gaps without repairing the shared installation here.

The runner currently supports emulator serials only. A separately coordinated
physical phone remains valid for manual tests and actual OEM coverage. The API 36
Google APIs image with Nothing Phone display settings does not run Nothing OS.

## Rejected additions

Do not add OpenHands, Prompt Master, Android Studio, Selenium, duplicate Appium tooling, dotnet-monitor,
dedicated security suites, another formatter stack, or another debugger. No Headroom
proxy/wrapper/interception/learning is enabled by this template.

Existing user-wide Codex plugins and IDE-bundled facilities belong to the user;
they are not silently uninstalled by this template. See the verification report
for the checks to perform on the target machine.
