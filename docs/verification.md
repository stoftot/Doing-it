# Tooling verification

Run applicable checks on the target machine. Installation, configuration validity,
compilation and actual runtime behavior are separate evidence. Report results in
the current conversation; do not populate this template with machine-specific
results, session history or acceptance dates.

## Android guidance verification

For documentation/integration-guidance maintenance, use static inspection:

- Check local Markdown links/anchors, skill metadata and the single root review
  marker. Confirm command parameters against the installed shared scripts without
  dot-sourcing their runtime libraries or invoking the runner for validation/help.
- Read the shared README, `version.json` and relevant changelog entries through the
  discovery path in [project integration](android-testing.md). Use only the read-only
  `Get-AndroidTestingChanges.ps1` for revision queries; recheck immediately before
  recording completion. Investigate malformed/future markers instead of lowering them.
- Compare documented TFM/package/build paths, native APK ABIs, test locations and
  locators to the actual project, existing output and source. An old APK's contents
  establish its shape, not that current source has been built or tested.
- Inspect existing project suites and any project JSON statically. The task-details
  suite runs without `android-testing.json`; do not create placeholder configuration
  that silently runs the shared Settings smoke.

Do not run template installers, builds, `Test-AndroidEnvironment.ps1`, ADB discovery,
runtime listing helpers, Appium or an emulator merely to validate these documents.
Some apparently informational shared helpers initialize runtime state. Preserve
shared scripts/configuration/presets/version/changelog/docs and all devices/data.
Report checks as static, and leave the review marker unchanged when required
integration validation remains incomplete. This maintenance does not bump the app
version or update its release records.

## Android project verification

For actual app changes/tests, follow [Android integration](android-testing.md) and
[module facts](ai/modules/MAUI_ANDROID.md). Existing automated coverage is the core
xUnit project, `tests/android/TaskDetails.ps1` for task text/details, and
`tests/android/DailyNotifications.ps1` for notification preferences, completion,
dismissal and alarm scheduling through Managed Appium sessions. The
[notification cases](../tests/manual/notifications.md) retain overnight/reboot,
privacy and OEM checks. The [planner manual cases](../tests/manual/planner.md)
cover the wider app, checklists and widgets. The broader acceptance below remains
necessary before advancing the root review marker:

1. Use the task-details suite against actual Appium page source and existing
   automation hooks, with unique fixtures and baseline-aware assertions. Extend
   coverage for 003/004/009 complete/Undo, date/list separation and persistence,
   and the other relevant cases. If choosing External instead, implement its explicit
   context/capability/teardown contract; core xUnit tests do not supply it.
2. In authorized runtime work, discover compatible existing devices, resources and
   reservations. Build once using `./tooling/build-android.ps1`; stop on failure and
   inspect the fresh signed APK's ABI. Run
   `dotnet test tests/DoingIt.Core.Tests/DoingIt.Core.Tests.csproj` for core behavior,
   reporting it separately from device tests.
3. Execute the app suite via `Invoke-AndroidTests.ps1`, targeting the shared fallback
   or explicitly configured existing project devices. Assert app behavior, not
   Settings/sample-counter behavior. Inspect `summary.json`, per-worker results,
   diagnostics and verified session cleanup/reservation release. Record actual
   serials, APK/configuration, cases and outcomes in transient run evidence.
4. Report skipped cases/preconditions and emulator/physical/OEM/API coverage limits.
   Case 001 needs an empty dedicated installation or separately authorized reset;
   never reset shared data automatically. Case 010 needs keyboard/back/layout and
   large-font assessment on suitable targets, with original settings restored.
5. Preserve services and data; follow documented ownership cleanup. Recheck the
   central revision and review new entries before recording the reviewed marker.

No build, unit-test, device launch or UI assertion pass is implied by completing the
documentation checks. Physical phone and Nothing OS tests require the separate
explicit-serial workflow; the emulator runner does not cover them.

## Repeat the tests

The remaining checks are the retained generic template/MCP acceptance procedure,
not a prerequisite for editing Android guidance or a replacement for app tests.

Run from the repository root. Keep configuration trusted in Codex and Rider open
for IDE checks. Do not start another verification over existing scratch fixtures.

```powershell
./tooling/prepare-verification.ps1
Get-Content tooling/verify-codex.md -Raw |
  codex exec --ephemeral --skip-git-repo-check --strict-config --approve-for-me --json `
    -o .tooling/codex-verification.md - > .tooling/codex-verification.jsonl
python tooling/verify-codex-evidence.py
python tooling/verify-config.py
```

For this project's MAUI verification, use the actual app wrapper and existing
tests above. Creating a scratch MAUI project or running the module installer is
not required for normal maintenance. Shared-tool smoke results describe the tool,
not Doing it behavior.

For Rider, open the scratch **solution**, confirm its identity with
`get_solution_projects(rootFolder=...)`, then call `get_file_problems`,
`search_symbol`, `build_solution_start` and `build_solution_state`. A successful
HTTP connection is not proof of the correct solution being loaded. If Rider is
closed, the integration must fail visibly while core/.NET CLI work remains usable.
Create the solution before opening Rider with `dotnet new sln -n ToolingProbe -o
.tooling/scratch`, then use `dotnet sln .tooling/scratch/ToolingProbe.slnx add` with
the scratch project paths. Include the MAUI project only when verifying that module.

After stopping test sessions and closing the scratch solution:

```powershell
./tooling/clean-verification.ps1
```

