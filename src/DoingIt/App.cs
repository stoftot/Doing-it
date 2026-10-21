using DoingIt.Core;
using DoingIt.Pages;

namespace DoingIt;

public sealed class App : Application
{
    private MainPage? _mainPage;
    private bool? _pendingWidgetNavigation;

    public void OpenFromWidget(bool createTask)
    {
        if (_mainPage is not null) _mainPage.OpenFromWidget(createTask);
        else _pendingWidgetNavigation = createTask;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        UserAppTheme = AppTheme.Light;
        var store = new PlannerStore(Path.Combine(FileSystem.AppDataDirectory, "planner.json"), TaskTitleLayout.Fits);
        store.Changed += (_, _) => AndroidWidgets.PlannerWidgets.Refresh();
        var page = new MainPage(store);
        _mainPage = page;
        if (_pendingWidgetNavigation is bool createTask)
        {
            _pendingWidgetNavigation = null;
            page.OpenFromWidget(createTask);
        }
        var window = new Window(new NavigationPage(page));
        window.Resumed += (_, _) =>
        {
            page.RefreshCurrentDay();
            AndroidWidgets.PlannerWidgets.Refresh();
        };
        AndroidWidgets.PlannerWidgets.Refresh();
        return window;
    }
}
