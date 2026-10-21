# Local crash reports

Install a normal Release APK, then use the app as usual. Collection starts
automatically and works offline. After an unexpected exit, reopen Doing it so
Android's previous-process record can be collected.

Open **Home → Crash reports → Share diagnostics** to create a ZIP and choose
where to save or send it through Android's share sheet. Attach that ZIP to the
debugging conversation later. If possible, add the approximate time and what you
noticed. No USB connection, account or reporting server is needed during use.
The app never uploads reports automatically. The diagnostics page is also
available from the planner-load failure screen.

## What is recorded

- Managed unhandled exceptions: exception types and call stacks, UTC time,
  app/build identity, Android version, phone model, ABI, process ID and the last
  30 screen/lifecycle events. Exception messages, task titles, descriptions and
  typed values are not deliberately recorded by the managed collector.
- The first handled navigation error in each process, clearly labeled as handled.
- On Android 11/API 30 or newer, Android's available historical exit reasons.
  Native crashes, unresponsive apps, low-memory kills and other system exits
  are labeled separately. Normal exits, user stops and package updates are ignored.
- Android thread dumps when available; on Android 12/API 31 or newer, available
  native tombstones are preserved as binary `.trace.pb` attachments. These follow
  Android's [tombstone format](https://developer.android.com/ndk/guides/debug).
  Text traces use `.trace.txt`; the ordinary report remains readable `.txt`.

Managed and Android reports can describe the same incident. Correlate process
IDs and UTC times rather than treating every report as a separate crash. Each
process supplies a compact build stamp to Android; records from before capture
started may have an unknown crashed build. The collector's environment is
explicitly distinguished from the crashed process's build after an upgrade.

## Storage and privacy

Reports stay in the app's private `NoBackupFilesDir/diagnostics` directory, excluded
from Android automatic backup. They survive app restarts and an update installed
with the same application/signing identity. Uninstalling or clearing app storage
removes them. Retention keeps the newest 20 reports, pruning those older than 30
days when collecting or reading. Each report has at most 65,536 characters of detail text
and a 512 KiB trace; oversized traces are omitted whole, with an explanation.

Exports include only report records and technical environment details, never
`planner.json` or its backup. Raw Android traces can contain app data or diagnostic
text, so review the export before forwarding it. Share destinations receive a ZIP
only when the user chooses them. Up to three recent export ZIPs remain in the app
cache for sharing/retry; Android may reclaim this cache.

**Delete saved reports** requires confirmation, preserves planner data and prevents
old Android exit records from being reimported. It also removes the app's cached
exports. It cannot remove copies already shared or Android's own system history.

## Limits

This is best-effort capture, not a guarantee that every exit leaves a report. A
failure before managed initialization, a sudden power loss, storage failure, or
severe memory exhaustion may prevent writing. Android's exit/trace buffers are
limited and vendor dependent; reopening promptly improves the chance of capture.
Older Android versions retain managed reports but cannot supply this system exit
history. An ANR trace normally becomes available after the affected process ends.
Fatal handlers do not mark exceptions handled or attempt to continue a broken
process. Diagnostic failures must not prevent ordinary app startup.

## Regression checks

`DiagnosticReportsTests` covers persistence, deduplication, concurrent writes,
bounded retention, corrupt/torn records, exact trace preservation, private-file
exclusion and deletion. `tests/android/Diagnostics.ps1` is a Managed suite using
the [reserved Android runner](android-testing.md). Its default mode deliberately
crashes only Doing it with Android's `am crash`, then verifies report recovery,
build/PID details, share cancellation, delete cancellation and restart persistence.
It preserves existing reports and planner data; induced reports remain labeled
by their timestamps/PIDs in test artifacts. Device tests must run on an emulator
or another explicitly designated test installation, not a phone being used normally.

For managed-fatal verification, build a **test-only APK** with:

```powershell
./tooling/build-android.ps1 -Configuration Release -DiagnosticTestHooks
```

Run the same suite with `ManagedCrash: true` in the runner suite parameters. That
build alone accepts the `com.doingit.app.TEST_MANAGED_CRASH` intent while running.
The suite verifies a real managed exception survives process death and excludes
its private test message. Rebuild without the switch for normal use; this removes
the hook. Run `VerifyHooksAbsent: true` against the final ordinary build to check
the test action leaves its process alive. Never distribute the test-only APK.

Manual device coverage and remaining older-API/OEM cases are in
[DIAGNOSTICS-001](../tests/manual/diagnostics.md).
