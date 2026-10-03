using FreeformHelper.UI;

namespace FreeformHelper.Tests.TestInfrastructure;

internal static class HeadlessAppBootstrap
{
    private static WeakReference<App>? _initializedApp;

    public static void EnsureInitialized()
    {
        if (Avalonia.Application.Current is not App app)
        {
            throw new InvalidOperationException("Avalonia Application.Current is not initialized for headless tests.");
        }

        if (_initializedApp is not null &&
            _initializedApp.TryGetTarget(out var initializedApp) &&
            ReferenceEquals(initializedApp, app))
        {
            return;
        }

        app.Initialize();
        _initializedApp = new WeakReference<App>(app);
    }
}
