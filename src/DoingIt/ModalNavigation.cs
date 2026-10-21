using System.Runtime.CompilerServices;

namespace DoingIt;

// All callers run on the UI thread. The window owns one gate, including nested
// editors, hardware Back and shortcuts; stale input is dropped rather than queued.
internal static class ModalNavigation
{
    private static readonly ConditionalWeakTable<Window, SemaphoreSlim> Gates = new();

    public static bool IsBusy(Page page) => page.Window is Window window && Gate(window).CurrentCount == 0;

    public static Task PushAsync(Page source, Func<Page> createPage) =>
        RunAsync(source, () => source.Navigation.PushModalAsync(createPage()));

    public static Task CloseAsync(Page page, Func<Task<bool>>? beforeClose = null) => RunAsync(page, async () =>
    {
        if (beforeClose is not null && !await beforeClose()) return;
        // Never pop a parent or a different editor if this page was already removed.
        if (page.Navigation.ModalStack.LastOrDefault() == page)
            await page.Navigation.PopModalAsync();
    });

    public static bool HandleBack(Page page)
    {
        _ = CloseAsync(page);
        return true;
    }

    public static async Task RunAsync(Page source, Func<Task> action)
    {
        if (source.Window is not Window window) return;
        var gate = Gate(window);
        if (!gate.Wait(0)) return;
        try
        {
            if (IsCurrent(source)) await action();
        }
        catch (Exception exception) { await ReportError(window, exception); }
        finally { gate.Release(); }
    }

    // External intents retain their latest requested destination while a save or
    // animation finishes. They use the same gate before replacing the modal stack.
    public static async Task RunShortcutAsync(Page source, Func<Task> action)
    {
        if (source.Window is not Window window) return;
        var gate = Gate(window);
        await gate.WaitAsync();
        try
        {
            if (source.Window == window) await action();
        }
        catch (Exception exception) { await ReportError(window, exception); }
        finally { gate.Release(); }
    }

    private static SemaphoreSlim Gate(Window window) => Gates.GetValue(window, _ => new SemaphoreSlim(1, 1));

    private static bool IsCurrent(Page page) =>
        (page.Navigation.ModalStack.LastOrDefault() ?? page.Navigation.NavigationStack.LastOrDefault()) == page;

    private static async Task ReportError(Window window, Exception exception)
    {
        CrashDiagnostics.RecordNavigationError(exception);
        Android.Util.Log.Error("DoingIt.Navigation", exception.ToString());
        var current = window.Page?.Navigation.ModalStack.LastOrDefault()
            ?? (window.Page as NavigationPage)?.CurrentPage ?? window.Page;
        if (current is null) return;
        try { await current.DisplayAlertAsync("Couldn't change page", "Please try again.", "OK"); }
        catch (Exception alertException) { Android.Util.Log.Error("DoingIt.Navigation", alertException.ToString()); }
    }
}
