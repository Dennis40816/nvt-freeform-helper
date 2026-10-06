using System.Runtime.CompilerServices;
using FreeformHelper.UI.Services;

namespace FreeformHelper.Tests;

internal static class TestAppSettingsIsolation
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // xUnit v3 runs this executable instead of testhost. The product skipped the startup grid and the
        // CAD load spinner process only for testhost, so the switches keep both off in tests.
        AppContext.SetSwitch("FreeformHelper.DisableInitialGridBootstrap", true);
        AppContext.SetSwitch(CadLoadSpinnerProcessHost.DisableProcessLaunchSwitch, true);

        var sandboxDir = Path.Combine(
            Path.GetTempPath(),
            "FreeformHelper.Tests",
            "app-settings");
        Directory.CreateDirectory(sandboxDir);
        var sandboxPath = Path.Combine(sandboxDir, "{guid}", "app-general-settings.json");
        Environment.SetEnvironmentVariable(
            AppGeneralSettingsStore.SettingsPathOverrideEnvironmentVariable,
            sandboxPath);
    }
}
