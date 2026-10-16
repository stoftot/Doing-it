# Project Map

| Path | Responsibility |
| --- | --- |
| `DoingIt.slnx`, `global.json` | Solution and .NET SDK selection |
| `src/DoingIt/DoingIt.csproj` | Android-only native MAUI application |
| `src/DoingIt/App.cs`, `MauiProgram.cs` | Window, persistent store, startup and fonts |
| `src/DoingIt/Pages/MainPage.cs` | Home, Recurring, Lists, completion popup and list management |
| `src/DoingIt/Pages/TaskEditorPage.cs` | Mutually exclusive date/list task form |
| `src/DoingIt/Pages/RecurringEditorPage.cs` | Daily, weekly and monthly schedule form |
| `src/DoingIt/Pages/CalendarPage.cs` | Shared month/week calendar and dated task actions |
| `src/DoingIt/Pages/EditorPage.cs` | Shared save/error/modal form handling |
| `src/DoingIt/Ui.cs`, `Resources/` | Palette, native UI building blocks, fonts and vector icons |
| `src/DoingIt/Platforms/Android/` | Android activity, manifest and theme |
| `src/DoingIt.Core/` | Domain records, date queries, validation and atomic JSON storage |
| `tests/DoingIt.Core.Tests/` | xUnit domain and file-storage regression tests |
| `tests/manual/planner.md` | Reusable Android interaction cases |
| `tooling/build-android.ps1` | Native Android build with Windows Unicode-path workaround |
| `docs/android-testing.md` | Shared AndroidTesting discovery, device/session ownership and project UI integration contract |
| `releases/` | Project release outcomes |

Dependency direction: `DoingIt` and `DoingIt.Core.Tests` reference `DoingIt.Core`; the core has no MAUI dependency.

## Flows and investigation anchors

- Launch: Android MainActivity -> MauiProgram -> App -> PlannerStore.LoadAsync -> MainPage.
- Mutation: page action -> PlannerStore validation -> atomic file save -> new Data snapshot -> page refresh.
- Scheduled view: PlannerQueries.ForDate combines exact-date tasks and active recurrence rules; completion is keyed by routine ID and date.
- Views refresh when modal editors close and Home's local date changes/resumes.

## Generated and external areas

`bin/`, `obj/`, `.tooling/`, and `.idea/` are ignored local/generated areas. App data belongs to Android private storage, not the source tree. Shared Android SDK/JDK/.NET installations belong to the machine.

Read [PROJECT_BRIEF](PROJECT_BRIEF.md) for scope, [ARCHITECTURE](ARCHITECTURE.md) for invariants, [CONVENTIONS](CONVENTIONS.md) for editing conventions, and the [.NET](modules/DOTNET.md) / [Android](modules/MAUI_ANDROID.md) facts for execution.
