# .NET project facts

| Fact | Value |
| --- | --- |
| Solution | `DoingIt.slnx` |
| App | `src/DoingIt/DoingIt.csproj`, native Android executable |
| Core | `src/DoingIt.Core/DoingIt.Core.csproj`, plain .NET class library |
| Tests | `tests/DoingIt.Core.Tests/DoingIt.Core.Tests.csproj`, xUnit |
| Frameworks | `net10.0-android` for app; `net10.0` for core/tests |
| SDK | `global.json`: 10.0.402, latestPatch roll forward |
| Relationships | App and tests reference core; core has no platform dependencies |

## Canonical commands

- Android restore/build: `./tooling/build-android.ps1` (PowerShell 7); defaults Debug. Includes the native resource packager's Windows Unicode-path workaround.
- Core tests: `dotnet test tests/DoingIt.Core.Tests/DoingIt.Core.Tests.csproj`.
- Formatting: `dotnet format DoingIt.slnx --verify-no-changes` (installed Android workload required to load the application project).
- Application entry: `DoingIt.App.CreateWindow`, selected by `MauiProgram` and the Android MainApplication.

Only the app requires `maui-android`, JDK 21, and the Android SDK. `Microsoft.Maui.Controls` follows the installed workload's MauiVersion. No web framework or server is used. See [MAUI_ANDROID](MAUI_ANDROID.md) for deployment.

No application-specific performance or source-debugger instrumentation is configured. Optional local debugger and IDE tooling follows [tooling](../../tooling.md); its availability is not required to run core tests.
