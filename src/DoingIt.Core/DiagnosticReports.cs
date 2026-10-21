using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoingIt.Core;

public sealed record DiagnosticReport(
    string Id,
    DateTimeOffset OccurredAt,
    string Kind,
    string Summary,
    string Details,
    byte[]? Trace = null,
    string? TraceFormat = null);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DiagnosticReport))]
internal partial class DiagnosticReportJsonContext : JsonSerializerContext;

/// <summary>A bounded, local-only store for diagnostic reports.</summary>
public sealed class DiagnosticReportStore
{
    public const int MaximumReports = 20;
    public static readonly TimeSpan MaximumAge = TimeSpan.FromDays(30);
    public const int MaximumDetailsLength = 64 * 1024;
    public const int MaximumTraceLength = 512 * 1024;

    private const int MaximumKindLength = 128;
    private const int MaximumSummaryLength = 2048;
    private const int MaximumEnvironmentLength = 16 * 1024;
    // Allow JSON escaping plus base64 at the documented text/trace limits.
    private const long MaximumRecordLength = 2 * 1024 * 1024;
    private const int MaximumIdLength = 100;
    private const string ReportFilePrefix = "report-";
    private static readonly Dictionary<string, object> DirectoryLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object LockGate = new();
    private readonly string _directory;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly object _gate;

    public DiagnosticReportStore(string directory) : this(directory, () => DateTimeOffset.UtcNow) { }

    public DiagnosticReportStore(string directory, Func<DateTimeOffset> utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(utcNow);
        _directory = Path.GetFullPath(directory);
        _utcNow = utcNow;
        lock (LockGate)
        {
            if (!DirectoryLocks.TryGetValue(_directory, out var directoryLock))
                DirectoryLocks[_directory] = directoryLock = new object();
            _gate = directoryLock;
        }
    }

    public void Add(DiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ValidateId(report.Id);
        var bounded = Bound(report);
        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            Prune(_utcNow().ToUniversalTime());
            var destination = ReportPath(bounded.Id);
            if (File.Exists(destination)) return;

            var temporary = Path.Combine(_directory, $".report-{bounded.Id}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, bounded, DiagnosticReportJsonContext.Default.DiagnosticReport);
                    stream.Flush(flushToDisk: true);
                }
                try { File.Move(temporary, destination, overwrite: false); }
                catch (IOException) when (File.Exists(destination)) { }
                Prune(_utcNow().ToUniversalTime());
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    public DiagnosticReport[] ReadAll()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_directory)) return [];
            Prune(_utcNow().ToUniversalTime());
            return ReadValidReports().OrderByDescending(r => r.OccurredAt).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (!Directory.Exists(_directory)) return;
            foreach (var file in Directory.EnumerateFiles(_directory, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (TryGetReportId(name, out _) ||
                    IsStoreTemporaryFile(name))
                    File.Delete(file);
            }
        }
    }

    public string Export(string destinationDirectory, string environmentDetails)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentNullException.ThrowIfNull(environmentDetails);
        var reports = ReadAll();
        Directory.CreateDirectory(destinationDirectory);
        var path = Path.Combine(Path.GetFullPath(destinationDirectory), $"diagnostics-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "README.txt", Encoding.UTF8.GetBytes(
                "Doing It diagnostic report export\n\n" + Truncate(environmentDetails, MaximumEnvironmentLength) +
                $"\n\nReports: {reports.Length}\n" +
                "Reports may include more than one record for the same incident; correlate UTC times and process IDs.\n" +
                "Managed messages and planner files are omitted. System traces can contain app data or diagnostic text.\n" +
                "Trace .pb files are Android native tombstone protobufs; .txt traces are Android thread dumps.\n" +
                "Android may not retain a trace. Low-memory/system exits are not necessarily crashes.\n"));
            foreach (var report in reports)
            {
                var text = $"Id: {report.Id}\nOccurred at: {report.OccurredAt:O}\nKind: {report.Kind}\nSummary: {report.Summary}\n\n{report.Details}\n";
                WriteEntry(archive, $"reports/{report.Id}.txt", Encoding.UTF8.GetBytes(text));
                if (report.Trace is { Length: > 0 } trace && IsValidTraceFormat(report.TraceFormat))
                    WriteEntry(archive, $"reports/{report.Id}.trace.{report.TraceFormat!.ToLowerInvariant()}", trace);
            }
        }
        return path;
    }

    private void Prune(DateTimeOffset now)
    {
        DeleteRecognizedTemporaryFiles();
        var reports = ReadValidReports().ToArray();
        var cutoff = now - MaximumAge;
        var retained = reports.Where(r => r.OccurredAt.ToUniversalTime() >= cutoff)
            .OrderByDescending(r => r.OccurredAt).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Take(MaximumReports).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var report in reports)
            if (!retained.Contains(report.Id)) File.Delete(ReportPath(report.Id));
    }

    private IEnumerable<DiagnosticReport> ReadValidReports()
    {
        foreach (var file in Directory.EnumerateFiles(_directory, ReportFilePrefix + "*.json", SearchOption.TopDirectoryOnly))
        {
            if (!TryGetReportId(Path.GetFileName(file), out var id)) continue;
            DiagnosticReport? report;
            try
            {
                var info = new FileInfo(file);
                if (info.Length > MaximumRecordLength)
                {
                    File.Delete(file);
                    continue;
                }
                using var stream = File.OpenRead(file);
                report = JsonSerializer.Deserialize(stream, DiagnosticReportJsonContext.Default.DiagnosticReport);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                TryDelete(file);
                continue;
            }
            if (report is null || !string.Equals(report.Id, id, StringComparison.Ordinal) || !IsValidId(report.Id))
            {
                TryDelete(file);
                continue;
            }
            yield return Bound(report);
        }
    }

    private string ReportPath(string id) => Path.Combine(_directory, ReportFilePrefix + id + ".json");

    private static DiagnosticReport Bound(DiagnosticReport report)
    {
        var trace = report.Trace is { Length: <= MaximumTraceLength } ? report.Trace.ToArray() : null;
        var format = trace is not null && IsValidTraceFormat(report.TraceFormat) ? report.TraceFormat!.ToLowerInvariant() : null;
        return report with
        {
            Kind = Truncate(report.Kind ?? "", MaximumKindLength),
            Summary = Truncate(report.Summary ?? "", MaximumSummaryLength),
            Details = Truncate(report.Details ?? "", MaximumDetailsLength),
            Trace = trace,
            TraceFormat = format
        };
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length];
    private static bool IsValidTraceFormat(string? format) => format is not null &&
        (format.Equals("pb", StringComparison.OrdinalIgnoreCase) || format.Equals("txt", StringComparison.OrdinalIgnoreCase));
    private static bool IsStoreTemporaryFile(string name)
    {
        if (!name.StartsWith(".report-", StringComparison.Ordinal) || !name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return false;
        var parts = name[8..^4].Split('.');
        return parts.Length == 2 && IsValidId(parts[0]) && Guid.TryParseExact(parts[1], "N", out _);
    }
    private static bool TryGetReportId(string fileName, out string id)
    {
        id = "";
        if (!fileName.StartsWith(ReportFilePrefix, StringComparison.Ordinal) || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
        id = fileName[ReportFilePrefix.Length..^5];
        return IsValidId(id);
    }
    private void DeleteRecognizedTemporaryFiles()
    {
        foreach (var file in Directory.EnumerateFiles(_directory, ".report-*.tmp", SearchOption.TopDirectoryOnly))
            if (IsStoreTemporaryFile(Path.GetFileName(file))) TryDelete(file);
    }
    private static void TryDelete(string file)
    {
        try { File.Delete(file); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private static bool IsValidId(string? id) => id is { Length: > 0 and <= MaximumIdLength } &&
        id.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
    private static void ValidateId(string id)
    {
        if (!IsValidId(id)) throw new ArgumentException("Report IDs may contain only ASCII letters, digits, underscores, and hyphens.", nameof(id));
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] contents)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(contents);
    }
}
