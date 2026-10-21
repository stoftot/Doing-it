using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using DoingIt.Core;
using System.Reflection;
using System.Text;

namespace DoingIt;

// Fatal handlers write synchronously and leave normal runtime termination intact.
internal static class CrashDiagnostics
{
    private static readonly SemaphoreSlim ImportGate = new(1, 1);
    private static readonly Queue<string> Breadcrumbs = new();
    private static DiagnosticReportStore? _store;
    private static Context? _context;
    private static string _directory = "";
    private static string _environment = "";
    private static int _fatalRecorded;
    private static int _navigationRecorded;
    public static string Status { get; private set; } = "Crash reporting is starting.";

    public static void Initialize(Context context)
    {
        try
        {
            if (_store is not null) return;
            _context = context;
            _directory = Path.Combine(context.NoBackupFilesDir!.AbsolutePath, "diagnostics");
            _store = new DiagnosticReportStore(_directory);
            var assembly = typeof(CrashDiagnostics).Assembly;
            var build = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "DiagnosticBuild")?.Value ?? "unknown";
            var revision = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            var package = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!;
#if DEBUG
            const string configuration = "Debug";
#else
            const string configuration = "Release";
#endif
            _environment = $"App: {context.PackageName} {package.VersionName}\nBuild UTC: {build}\nSource revision/base (may include local changes): {revision}\nConfiguration: {configuration}\n"
                + $"Device: {Build.Manufacturer} {Build.Model}\nAndroid: {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt})\n"
                + $"ABI: {string.Join(", ", Build.SupportedAbis ?? [])}\nRuntime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}\n"
                + $"Process: {Android.OS.Process.MyPid()}\nStarted UTC: {DateTimeOffset.UtcNow:O}\n";
#if DIAGNOSTIC_TEST_HOOKS
            _environment += "Test-only crash hook: enabled\n";
#endif
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception) RecordException(exception, "Unhandled managed exception", true);
            };
            AndroidEnvironment.UnhandledExceptionRaiser += (_, args) => RecordException(args.Exception, "Unhandled Android callback", true);
            Status = OperatingSystem.IsAndroidVersionAtLeast(30)
                ? "Captures managed errors and Android's available exit history."
                : "Captures managed errors. Android 11 or newer is needed for system exit history.";
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                // Retained with the crashed process; Android allows at most 128 bytes.
                var summary = Encoding.UTF8.GetBytes($"build={build};config={configuration};version={package.VersionName}");
                (context.GetSystemService(Context.ActivityService) as ActivityManager)?.SetProcessStateSummary(summary.Take(128).ToArray());
            }
            Breadcrumb("Application starting");
        }
        catch (Exception exception) { Failed("Crash reporting could not start on this device.", exception); }
    }

    public static void Breadcrumb(string operation)
    {
        lock (Breadcrumbs)
        {
            Breadcrumbs.Enqueue($"{DateTimeOffset.UtcNow:O} {operation}");
            while (Breadcrumbs.Count > 30) Breadcrumbs.Dequeue();
        }
    }

    public static void RecordNavigationError(Exception exception)
    {
        if (Interlocked.Exchange(ref _navigationRecorded, 1) == 0) RecordException(exception, "Handled navigation error", false);
    }

    private static void RecordException(Exception exception, string kind, bool fatal)
    {
        if (fatal && Interlocked.Exchange(ref _fatalRecorded, 1) != 0) return;
        try
        {
            var details = new StringBuilder(_environment);
            details.AppendLine("\nException messages and task content are omitted. Types and call stacks follow.");
            for (var current = exception; current is not null && details.Length < 48000; current = current.InnerException)
            {
                details.AppendLine(current.GetType().FullName);
                details.AppendLine(current.StackTrace ?? "Stack unavailable.");
            }
            lock (Breadcrumbs) details.AppendLine("\nRecent app screens/lifecycle:\n" + string.Join("\n", Breadcrumbs));
            _store?.Add(new DiagnosticReport(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow,
                kind, exception.GetType().FullName ?? "Exception", details.ToString()));
        }
        catch (Exception captureException) { Android.Util.Log.Warn("DoingIt.Diagnostics", captureException.GetType().Name); }
    }

    public static async Task<DiagnosticReport[]> ReadAsync()
    {
        await CollectPreviousExitsAsync();
        return await Task.Run(() => RequireStore().ReadAll());
    }

    public static Task CollectPreviousExitsAsync() => Task.Run(async () =>
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30) || _store is null || _context is null) return;
        await ImportGate.WaitAsync();
        try
        {
            var manager = _context.GetSystemService(Context.ActivityService) as ActivityManager;
            if (manager is null) return;
            var cutoff = ReadCutoff();
            var exits = manager.GetHistoricalProcessExitReasons(_context.PackageName, 0, 30) ?? [];
            foreach (var exit in exits)
            {
                using (exit)
                {
                    if (exit.Timestamp <= cutoff) continue;
                    var reason = (ApplicationExitInfoReason)exit.Reason;
                    if (reason is ApplicationExitInfoReason.UserRequested or ApplicationExitInfoReason.UserStopped
                        or ApplicationExitInfoReason.ExitSelf or ApplicationExitInfoReason.PermissionChange) continue;
                    if (OperatingSystem.IsAndroidVersionAtLeast(34) && reason is
                        ApplicationExitInfoReason.PackageUpdated or ApplicationExitInfoReason.PackageStateChange) continue;
                    var occurred = DateTimeOffset.FromUnixTimeMilliseconds(exit.Timestamp);
                    if (occurred < DateTimeOffset.UtcNow.AddDays(-30)) continue;
                    var kind = reason switch
                    {
                        ApplicationExitInfoReason.Crash => "Android crash",
                        ApplicationExitInfoReason.CrashNative => "Native crash",
                        ApplicationExitInfoReason.Anr => "App not responding",
                        ApplicationExitInfoReason.LowMemory => "Low-memory shutdown (not a crash)",
                        _ => "System exit: " + reason
                    };
                    var previousSummary = exit.GetProcessStateSummary();
                    var details = $"Exit UTC: {occurred:O}\nReason: {reason} ({exit.Reason})\nPID: {exit.Pid}\n"
                        + $"Status/signal: {exit.Status}\nImportance: {exit.Importance}\nLast sampled PSS/RSS (KiB): {exit.Pss}/{exit.Rss}\n"
                        + $"Crashed-process build: {(previousSummary is null ? "Unknown (before capture or older app)" : Encoding.UTF8.GetString(previousSummary))}\n"
                        + $"\nCollector environment (may be a newer build):\n{_environment}";
                    byte[]? trace = null;
                    string? format = null;
                    try
                    {
                        using var stream = exit.TraceInputStream;
                        if (stream is not null)
                        {
                            using var buffer = new MemoryStream();
                            var chunk = new byte[8192];
                            int read;
                            while (buffer.Length <= 512 * 1024 && (read = stream.Read(chunk, 0, chunk.Length)) > 0) buffer.Write(chunk, 0, read);
                            if (buffer.Length <= 512 * 1024)
                            {
                                trace = buffer.ToArray();
                                format = reason == ApplicationExitInfoReason.CrashNative ? "pb" : "txt";
                            }
                            else details += "\nSystem trace omitted: larger than 512 KiB.\n";
                        }
                        else details += "\nAndroid did not retain a system trace for this exit.\n";
                    }
                    catch (Exception exception) { details += $"\nSystem trace unavailable: {exception.GetType().Name}.\n"; }
                    _store.Add(new DiagnosticReport($"exit-{exit.Timestamp}-{exit.Pid}", occurred, kind, kind, details, trace, format));
                }
            }
        }
        catch (Exception exception) { Failed("Android exit history is currently unavailable. Managed reports may still be available.", exception); }
        finally { ImportGate.Release(); }
    });

    public static async Task<string> ExportAsync()
    {
        await CollectPreviousExitsAsync();
        return await Task.Run(() =>
        {
            var directory = Path.Combine(_context!.CacheDir!.AbsolutePath, "diagnostic-share");
            Directory.CreateDirectory(directory);
            foreach (var old in new DirectoryInfo(directory).GetFiles("*.zip").OrderByDescending(f => f.LastWriteTimeUtc).Skip(2)) old.Delete();
            return RequireStore().Export(directory, _environment);
        });
    }

    public static async Task ClearAsync()
    {
        await ImportGate.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                var store = RequireStore();
                Directory.CreateDirectory(_directory);
                var cutoffPath = Path.Combine(_directory, "cleared-through");
                File.WriteAllText(cutoffPath + ".tmp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
                File.Move(cutoffPath + ".tmp", cutoffPath, true);
                store.Clear();
                var exports = Path.Combine(_context!.CacheDir!.AbsolutePath, "diagnostic-share");
                if (Directory.Exists(exports)) foreach (var file in Directory.GetFiles(exports, "*.zip")) File.Delete(file);
            });
        }
        finally { ImportGate.Release(); }
    }

    private static long ReadCutoff()
    {
        var path = Path.Combine(_directory, "cleared-through");
        return File.Exists(path) && long.TryParse(File.ReadAllText(path), out var cutoff) ? cutoff : 0;
    }
    private static DiagnosticReportStore RequireStore() => _store ?? throw new InvalidOperationException("Crash reporting is unavailable on this device.");
    private static void Failed(string status, Exception exception)
    {
        Status = status;
        Android.Util.Log.Warn("DoingIt.Diagnostics", exception.GetType().Name);
    }
}
