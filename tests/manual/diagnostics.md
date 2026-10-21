# Diagnostic report regressions

## DIAGNOSTICS-001 — Capture, retain and share crash reports

### Purpose
Verify reports remain useful after process death and can be shared without changing planner data.

### Preconditions
- Use a designated test installation. Preserve existing tasks and reports. Record the build configuration, Android version, model, PID and approximate device time.
- Managed-fatal testing requires the explicit test-only build described in [diagnostics](../../docs/diagnostics.md). Normal phone use must use a build without test hooks.

### Steps
1. Open Home → Crash reports. Verify the local-only explanation, retention limit and report count. Open an existing report, if present, and return once to the list.
2. On the reserved emulator, induce one Android crash via the suite, then reopen the app. Verify an Android crash report with the old PID and UTC time is retained. Normal force-stop/relaunch must not add a crash report.
3. On the test-only build, induce the managed failure via the suite. Reopen and verify exception type, call stack, build stamp and recent screen names. The exception message `PRIVATE-TEST-MESSAGE` must be absent from the managed report.
4. Close/reopen the report page and restart the app. Verify the same records remain without duplicates. Correlate managed/system reports for one incident by PID/time rather than requiring a single record.
5. Select Share diagnostics. Verify Android displays a ZIP; cancel and confirm reports remain. Separately save the ZIP to a user-chosen destination, open it and inspect README/report text plus available trace files. Planner files must not be attached. No destination should receive anything before selection.
6. Select Delete saved reports, cancel, and confirm the count remains. Only on an installation whose reports are all disposable or already exported, confirm deletion. Verify the list remains empty after restart, old Android history is not reimported, and tasks remain unchanged. A later new crash must be captured.
7. On an ordinary Release build, send the test intent via the suite's VerifyHooksAbsent mode. The process must remain alive. Use the ordinary build for subsequent phone use.

### Expected result
- Capture is automatic and local. Reports survive restarts and updates with the same signing identity. Unavailable traces are explained, normal stops are excluded, and low-memory exits are not labeled crashes.
- Only an explicit share sends data; cancellation and diagnostic deletion preserve planner data. Keep at most 20 reports and remove expired ones on access.
- Android below API 30 explains its managed-only coverage. On API 31+ a naturally occurring native crash should include a tombstone if Android retained one within the size bound; absence is not falsely presented as success.
- A diagnostics storage/import failure must not prevent the planner opening. Severe early/runtime failures may yield only Android history or no report, as documented.

### Cleanup
- Return to Home and restore the ordinary Release build. Preserve pre-existing planner data and reports; remove only explicitly disposable diagnostic exports. Leave shared services under their normal ownership.
