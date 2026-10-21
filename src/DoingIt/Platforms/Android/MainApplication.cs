using Android.App;
using Android.Runtime;

namespace DoingIt;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        CrashDiagnostics.Initialize(this);
        base.OnCreate();
        _ = CrashDiagnostics.CollectPreviousExitsAsync();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
