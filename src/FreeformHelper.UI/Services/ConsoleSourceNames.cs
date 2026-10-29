using System.Collections.Immutable;
using Nvt.Core.LogConsole;

namespace FreeformHelper.UI.Services;

/// <summary>
/// The short display names of the console sources. A log event keeps the full logger name as its source ID,
/// so filtering, the <c>terminal</c> query and the exported text still show the full name.
/// Core shows a source that is not listed here under its source ID.
/// </summary>
internal static class ConsoleSourceNames
{
    /// <summary>Gets the sources of the app in display order. Every source ID and every display name is unique.</summary>
    public static ImmutableArray<ConsoleSource> Registry { get; } =
    [
        new("FreeformHelper.UI.App", "App"),
        new("FreeformHelper.UI.Program", "Program"),
        new("FreeformHelper.UI.Logging.LoggingBootstrapper", "Logging"),
        new("FreeformHelper.UI.Services.StartupPerfTracker", "Startup"),
        new("FreeformHelper.UI.ViewModels.ShellViewModel", "Shell"),
        new("FreeformHelper.UI.ViewModels.FreeformHelperViewModel", "Workflow"),
        new("FreeformHelper.UI.Views.FreeformHelperView", "View"),
        new("FreeformHelper.UI.Views.DevView", "DevView"),
        new("FreeformHelper.UI.Services.CadLoadWorkflowService", "CadLoad"),
        new("FreeformHelper.UI.Services.CadLoadSpinnerProcessHost", "Spinner"),
        new("FreeformHelper.UI.Services.GridRebuildOrchestrator", "Grid"),
        new("FreeformHelper.UI.Services.LayerCatalogStateService", "Layers"),
        new("FreeformHelper.UI.Services.AppGeneralSettingsStore", "Settings"),
        new("FreeformHelper.UI.Services.ConsoleLinkOpener", "Links"),
        new("FreeformHelper.UI.Services.UiEventFailureReporter", "UiEvents"),
        new("FreeformHelper.UI.Services.RuntimeQueryIpcServer", "RuntimeQuery")
    ];
}
