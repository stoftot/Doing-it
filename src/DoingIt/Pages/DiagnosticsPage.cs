using DoingIt.Core;

namespace DoingIt.Pages;

public sealed class DiagnosticsPage : ContentPage
{
    private readonly VerticalStackLayout _body = new() { Spacing = 16, Padding = 24 };
    private bool _busy;

    public DiagnosticsPage()
    {
        Ui.ConfigurePage(this);
        var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        var header = Ui.ModalHeader("Crash reports", () => _busy ? Task.CompletedTask : ModalNavigation.CloseAsync(this));
        header.Margin = new Thickness(24, 12, 16, 12);
        root.Add(header);
        root.Add(new ScrollView { Content = _body }, 0, 1);
        Content = root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_busy) return;
        _busy = true;
        try { await Refresh(); }
        catch (Exception) { _body.Clear(); _body.Add(Ui.Text("Reports couldn't be opened. Close this page and try again.")); }
        finally { _busy = false; }
    }

    private async Task Refresh()
    {
        var reports = await CrashDiagnostics.ReadAsync();
        _body.Clear();
        _body.Add(Ui.Text("Saved on this phone", 22, bold: true));
        _body.Add(Ui.Text("Reports are collected automatically. Nothing is uploaded. After a crash, reopen the app so it can collect Android's record.", 14));
        _body.Add(Ui.Text("Keeps the latest 20 reports for up to 30 days. Planner files are not attached. System traces can contain app data or diagnostic text; review exports before forwarding them.", 12, Ui.Muted));
        _body.Add(Ui.Text(CrashDiagnostics.Status, 12, Ui.Muted));
        var count = Ui.Text($"{reports.Length} saved reports", 16, bold: true);
        count.AutomationId = "DiagnosticCount";
        _body.Add(count);
        _body.Add(Ui.Button("Share diagnostics", ShareReports, id: "ShareDiagnostics"));
        if (reports.Length > 0) _body.Add(Ui.Quiet("Delete saved reports", ClearReports, "ClearDiagnostics"));
        if (reports.Length == 0) _body.Add(Ui.Text("No reports saved yet. Not every shutdown leaves a trace, and an empty list doesn't prove the app hasn't crashed.", 13, Ui.Muted));
        foreach (var report in reports)
        {
            var kind = Ui.Text(report.Kind, 16, bold: true);
            kind.AutomationId = "DiagnosticKind_" + report.Id;
            _body.Add(Ui.Card(Ui.Stack(kind,
                Ui.Text(report.OccurredAt.ToLocalTime().ToString("g"), 12, Ui.Muted), Ui.Text(report.Summary, 12),
                Ui.Quiet("View report", () => ModalNavigation.PushAsync(this, () => new DiagnosticDetailPage(report)), "ViewDiagnostic_" + report.Id))));
        }
    }

    private Task ShareReports() => ModalNavigation.RunAsync(this, async () =>
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var path = await CrashDiagnostics.ExportAsync();
            await Share.Default.RequestAsync(new ShareFileRequest("Doing it diagnostics", new ShareFile(path, "application/zip")));
        }
        catch (Exception) { await DisplayAlertAsync("Couldn't share reports", "Please try again. Saved reports have been kept.", "OK"); }
        finally { _busy = false; }
    });

    private Task ClearReports() => ModalNavigation.RunAsync(this, async () =>
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (!await DisplayAlertAsync("Delete saved reports?", "This removes reports on this phone. Your tasks are kept. Copies you already shared are unaffected.", "Delete", "Keep reports")) return;
            await CrashDiagnostics.ClearAsync();
            await Refresh();
        }
        catch (Exception) { await DisplayAlertAsync("Couldn't delete reports", "Please try again.", "OK"); }
        finally { _busy = false; }
    });

    protected override bool OnBackButtonPressed() => _busy || ModalNavigation.HandleBack(this);

    private sealed class DiagnosticDetailPage : ContentPage
    {
        public DiagnosticDetailPage(DiagnosticReport report)
        {
            Ui.ConfigurePage(this);
            var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
            var header = Ui.ModalHeader("Report details", () => ModalNavigation.CloseAsync(this));
            header.Margin = new Thickness(24, 12, 16, 12);
            root.Add(header);
            var text = Ui.Text($"{report.Kind}\n{report.OccurredAt:O}\n\n{report.Details}", 12);
            text.AutomationId = "DiagnosticDetails";
            text.Margin = 24;
            root.Add(new ScrollView { Content = text }, 0, 1);
            Content = root;
        }
        protected override bool OnBackButtonPressed() => ModalNavigation.HandleBack(this);
    }
}
