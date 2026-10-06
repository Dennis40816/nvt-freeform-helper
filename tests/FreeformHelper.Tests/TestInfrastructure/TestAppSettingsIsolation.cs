using System.Runtime.CompilerServices;
using FreeformHelper.UI.Services;

namespace FreeformHelper.Tests;

internal static class TestAppSettingsIsolation
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // xUnit v3 runs this executable instead of testhost, which used to skip the startup grid.
        AppContext.SetSwitch("FreeformHelper.DisableInitialGridBootstrap", true);

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
