using DoingIt.Core;
using DoingIt.Pages;

namespace DoingIt;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        UserAppTheme = AppTheme.Light;
        var store = new PlannerStore(Path.Combine(FileSystem.AppDataDirectory, "planner.json"));
        var page = new MainPage(store);
        var window = new Window(new NavigationPage(page));
        window.Resumed += (_, _) => page.RefreshCurrentDay();
        return window;
    }
}
