using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DoingIt.Core;
using Xunit;

namespace DoingIt.Core.Tests;

public sealed class DiagnosticReportsTests
{
    [Fact]
    public void Add_PersistsReportsAcrossStoreInstances()
    {
        using var fixture = new Fixture();
        fixture.Store.Add(Report("persisted"));

        var reopened = new DiagnosticReportStore(fixture.DirectoryPath, fixture.Now);
        var report = Assert.Single(reopened.ReadAll());
        Assert.Equal("persisted", report.Id);
        Assert.Equal("Details", report.Details);
    }

    [Fact]
    public void Add_DeduplicatesByIdAndKeepsOriginal()
    {
        using var fixture = new Fixture();
        fixture.Store.Add(Report("same", summary: "first"));
        fixture.Store.Add(Report("same", summary: "second"));

        Assert.Equal("first", Assert.Single(fixture.Store.ReadAll()).Summary);
        Assert.Single(Directory.GetFiles(fixture.DirectoryPath, "report-*.json"));
    }

    [Fact]
    public async Task Add_ConcurrentCallsRetainEveryUniqueReport()
    {
        using var fixture = new Fixture();
        var stores = Enumerable.Range(0, 8).Select(_ => new DiagnosticReportStore(fixture.DirectoryPath, fixture.Now)).ToArray();
        await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Task.Run(() => stores[i % stores.Length].Add(Report($"id-{i}")))));

        Assert.Equal(20, fixture.Store.ReadAll().Length);
        Assert.Equal(20, Directory.GetFiles(fixture.DirectoryPath, "report-*.json").Length);
    }

    [Fact]
    public void Add_RetainsOnlyNewestTwentyReportsWithinThirtyDays()
    {
        using var fixture = new Fixture();
        for (var i = 0; i < 22; i++)
            fixture.Store.Add(Report($"report-{i:D2}", at: fixture.Current.AddMinutes(i)));
        fixture.Store.Add(Report("old", at: fixture.Current.AddDays(-31)));

        var reports = fixture.Store.ReadAll();
        Assert.Equal(20, reports.Length);
        Assert.DoesNotContain(reports, report => report.Id is "report-00" or "report-01" or "old");
        Assert.Contains(reports, report => report.Id == "report-21");
    }

    [Fact]
    public void ReadAll_IgnoresCorruptAndMismatchedRecords()
    {
        using var fixture = new Fixture();
        fixture.Store.Add(Report("healthy"));
        File.WriteAllText(Path.Combine(fixture.DirectoryPath, "report-broken.json"), "{");
        File.WriteAllText(Path.Combine(fixture.DirectoryPath, "report-wrong-id.json"), "{\"Id\":\"other\"}");

        Assert.Equal("healthy", Assert.Single(fixture.Store.ReadAll()).Id);
        Assert.False(File.Exists(Path.Combine(fixture.DirectoryPath, "report-broken.json")));
        Assert.False(File.Exists(Path.Combine(fixture.DirectoryPath, "report-wrong-id.json")));
    }

    [Fact]
    public void ReadAll_DeletesOversizeCorruptRecordsWithoutLoadingThem()
    {
        using var fixture = new Fixture();
        fixture.Store.Add(Report("healthy"));
        var oversized = Path.Combine(fixture.DirectoryPath, "report-oversized.json");
        File.WriteAllBytes(oversized, new byte[2 * 1024 * 1024 + 1]);

        Assert.Equal("healthy", Assert.Single(fixture.Store.ReadAll()).Id);
        Assert.False(File.Exists(oversized));
    }

    [Fact]
    public void Add_BoundsTextAndOmitsOversizeTraceWithoutTruncatingIt()
    {
        using var fixture = new Fixture();
        var tooLargeTrace = new byte[DiagnosticReportStore.MaximumTraceLength + 1];
        fixture.Store.Add(Report("bounded", summary: new string('s', 3000), details: new string('d', 70000)) with
        {
            Trace = tooLargeTrace,
            TraceFormat = "pb"
        });

        var saved = Assert.Single(fixture.Store.ReadAll());
        Assert.Equal(2048, saved.Summary.Length);
        Assert.Equal(DiagnosticReportStore.MaximumDetailsLength, saved.Details.Length);
        Assert.Null(saved.Trace);
        Assert.Null(saved.TraceFormat);
    }

    [Fact]
    public void Add_PreservesMaximumEscapedDetailsAndTraceWithinRecordLimit()
    {
        using var fixture = new Fixture();
        var details = new string('\u0001', DiagnosticReportStore.MaximumDetailsLength);
        var trace = new byte[DiagnosticReportStore.MaximumTraceLength];
        Random.Shared.NextBytes(trace);
        fixture.Store.Add(Report("maximum-record", details: details) with { Trace = trace, TraceFormat = "pb" });

        var reportPath = Path.Combine(fixture.DirectoryPath, "report-maximum-record.json");
        Assert.InRange(new FileInfo(reportPath).Length, 1024 * 1024 + 1, 2 * 1024 * 1024);
        var saved = Assert.Single(fixture.Store.ReadAll());
        Assert.Equal(details, saved.Details);
        Assert.Equal(trace, saved.Trace);
    }

    [Fact]
    public void Export_ContainsReadableReportsAndValidatedTraceOnly()
    {
        using var fixture = new Fixture();
        fixture.Store.Add(Report("with-trace") with { Trace = [1, 2, 3], TraceFormat = "pb" });
        fixture.Store.Add(Report("with-txt-trace") with { Trace = [7, 8], TraceFormat = "txt" });
        fixture.Store.Add(Report("bad-format") with { Trace = [4, 5], TraceFormat = "exe" });
        var outsideSibling = Path.Combine(fixture.DirectoryPath, "planner.json");
        File.WriteAllText(outsideSibling, "must not export");

        var zipPath = fixture.Store.Export(fixture.ExportPath, "Environment details");
        using var archive = ZipFile.OpenRead(zipPath);
        var names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name).ToArray();
        Assert.Equal(new[] { "README.txt", "reports/bad-format.txt", "reports/with-trace.trace.pb", "reports/with-trace.txt", "reports/with-txt-trace.trace.txt", "reports/with-txt-trace.txt" }, names);
        Assert.Contains("Environment details", ReadEntry(archive, "README.txt"));
        Assert.Contains("Details", ReadEntry(archive, "reports/with-trace.txt"));
        Assert.Equal(new byte[] { 1, 2, 3 }, ReadEntryBytes(archive, "reports/with-trace.trace.pb"));
        Assert.Equal(new byte[] { 7, 8 }, ReadEntryBytes(archive, "reports/with-txt-trace.trace.txt"));
        Assert.Contains("Details", ReadEntry(archive, "reports/with-txt-trace.txt"));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("planner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Clear_RemovesOnlyStoreReportsAndTemporaryFiles()
    {
        using var fixture = new Fixture();
        fixture.Store.Add(Report("remove-me"));
        var storeTemp = Path.Combine(fixture.DirectoryPath, ".report-remove-me.0123456789abcdef0123456789abcdef.tmp");
        var unrelated = Path.Combine(fixture.DirectoryPath, "planner.json");
        var unrelatedTemp = Path.Combine(fixture.DirectoryPath, ".keep.tmp");
        var clearedThrough = Path.Combine(fixture.DirectoryPath, "cleared-through");
        File.WriteAllText(storeTemp, "torn");
        File.WriteAllText(unrelated, "planner data");
        File.WriteAllText(unrelatedTemp, "other");
        File.WriteAllText(clearedThrough, "cutoff");

        fixture.Store.Clear();

        Assert.Empty(fixture.Store.ReadAll());
        Assert.False(File.Exists(Path.Combine(fixture.DirectoryPath, "report-remove-me.json")));
        Assert.False(File.Exists(storeTemp));
        Assert.True(File.Exists(unrelated));
        Assert.True(File.Exists(unrelatedTemp));
        Assert.True(File.Exists(clearedThrough));
    }

    [Fact]
    public void Add_RejectsIdsLongerThanOneHundredCharacters()
    {
        using var fixture = new Fixture();
        Assert.Throws<ArgumentException>(() => fixture.Store.Add(Report(new string('a', 101))));
        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath));
    }

    [Fact]
    public void ReadAll_OrdersNewestReportsFirst()
    {
        using var fixture = new Fixture();
        fixture.Store.Add(Report("older", at: fixture.Current.AddMinutes(-1)));
        fixture.Store.Add(Report("newer", at: fixture.Current));

        Assert.Equal(new[] { "newer", "older" }, fixture.Store.ReadAll().Select(report => report.Id));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("bad/name")]
    [InlineData("with space")]
    [InlineData("")]
    public void Add_RejectsMaliciousOrInvalidIds(string id)
    {
        using var fixture = new Fixture();
        Assert.Throws<ArgumentException>(() => fixture.Store.Add(Report(id)));
        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath));
    }

    private static DiagnosticReport Report(string id, string summary = "Summary", string details = "Details", DateTimeOffset? at = null) =>
        new(id, at ?? new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), "test", summary, details);

    private static string ReadEntry(ZipArchive archive, string name) => Encoding.UTF8.GetString(ReadEntryBytes(archive, name));
    private static byte[] ReadEntryBytes(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private sealed class Fixture : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "doing-it-diagnostics-" + Guid.NewGuid().ToString("N"));
        public string ExportPath { get; }
        public DateTimeOffset Current { get; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        public Func<DateTimeOffset> Now { get; }
        public DiagnosticReportStore Store { get; }

        public Fixture()
        {
            Directory.CreateDirectory(DirectoryPath);
            ExportPath = Path.Combine(DirectoryPath, "exports");
            Now = () => Current;
            Store = new DiagnosticReportStore(DirectoryPath, Now);
        }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
