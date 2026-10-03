using System.Runtime.CompilerServices;
using FreeformHelper.UI.Services;

namespace FreeformHelper.Tests;

internal static class TestAppSettingsIsolation
{
    [ModuleInitializer]
    internal static void Initialize()
    {
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
