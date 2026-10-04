using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using NLog;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// The ShellViewModel serves as the main ViewModel for the application's shell (main window).
/// It manages which sub-ViewModel is currently active, handles navigation, and provides
/// access to application-wide services like logging.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private const int ConsoleRenderTailSourceLineLimit = 4000;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly StringBuilder _consoleTextBuffer = new();
    private bool _isDisposed;

    // --- Sub-Viewmodels ---
    public FreeformHelperViewModel FreeformHelper { get; }
    private HowToUseViewModel? _howToUse;
    private DevViewModel? _dev;
    private SimulationHostViewModel? _simulation;
    private CoordinatePlannerHostViewModel? _coordinatePlanner;
    private readonly SimulationWorkspaceUseCase _simulationWorkspaceUseCase = new(new NotchApplySimulationReviewUseCase());
    private readonly SemaphoreSlim _simulationWorkspaceBuildGate = new(1, 1);
    private readonly CoordinatePlannerWorkspaceUseCase _coordinatePlannerWorkspaceUseCase = new();
    private readonly SemaphoreSlim _coordinatePlannerWorkspaceBuildGate = new(1, 1);
    private int _pendingWorkspaceSourceChangeVersion;
    private int _processedWorkspaceSourceChangeVersion;
    private int _workspaceSourceChangeDrainRunning;
    public HowToUseViewModel HowToUse => _howToUse ??= new HowToUseViewModel();
    public DevViewModel Dev => _dev ??= new DevViewModel();
    public SimulationHostViewModel Simulation => _simulation ??= new SimulationHostViewModel();
    public CoordinatePlannerHostViewModel CoordinatePlanner => _coordinatePlanner ??= new CoordinatePlannerHostViewModel();

    /// <summary>
    /// Gets or sets the currently active ViewModel, which is displayed in the main content area.
    /// </summary>
    [ObservableProperty]
    private object _currentViewModel = null!;

    // --- Navigation State Flags ---
    /// <summary>
    /// Gets or sets a value indicating whether the main Workspace view is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isWorkspaceActive;
    /// <summary>
    /// Gets or sets a value indicating whether the How To Use view is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isHowToUseActive;
    /// <summary>
    /// Gets or sets a value indicating whether the Dev view is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isDevActive;
    /// <summary>
    /// Gets or sets a value indicating whether the Simulation view is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isSimulationActive;
    /// <summary>
    /// Gets or sets a value indicating whether the Coordinate page is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isCoordinateActive;

    // --- Console/Log Display ---
    /// <summary>
    /// Gets a read-only observable collection of application log entries.
    /// </summary>
    public static ReadOnlyObservableCollection<AppLogEntry> LogEntries => AppLogStore.Instance.Entries;

    /// <summary>
    /// Gets or sets a value indicating whether the in-app console panel is expanded.
    /// </summary>
    [ObservableProperty]
    private bool _isConsoleExpanded = true;

    /// <summary>
    /// Gets or sets the console font size.
    /// </summary>
    [ObservableProperty]
    private double _consoleFontSize = 14.0;

    /// <summary>
    /// Gets or sets the concatenated text content of all log entries for display in the console.
    /// </summary>
    [ObservableProperty]
    private string _consoleText = string.Empty;

    /// <summary>
    /// Gets or sets the rendered line count in current console view.
    /// </summary>
    [ObservableProperty]
    private int _consoleRenderedLineCount;

    /// <summary>
    /// Gets or sets the source line count currently stored in app log entries.
    /// </summary>
    [ObservableProperty]
    private int _consoleSourceLineCount;

    /// <summary>
    /// Gets or sets a value indicating whether duplicated consecutive console lines are collapsed.
    /// </summary>
    [ObservableProperty]
    private bool _isConsoleDedupEnabled;

    /// <summary>
    /// Gets or sets the console search keyword.
    /// </summary>
    [ObservableProperty]
    private string _consoleSearchText = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether console should filter to matched lines only.
    /// </summary>
    [ObservableProperty]
    private bool _isConsoleFilterEnabled;

    /// <summary>
    /// Gets the summary text shown in console header.
    /// </summary>
    public string ConsoleSummaryText => IsConsoleDedupEnabled
        ? $"Lines {ConsoleRenderedLineCount} (from {ConsoleSourceLineCount})"
        : ConsoleRenderedLineCount < ConsoleSourceLineCount
            ? $"Lines {ConsoleRenderedLineCount} / {ConsoleSourceLineCount}"
            : $"Lines {ConsoleRenderedLineCount}";

    // --- Commands ---
    /// <summary>
    /// Gets the command to show the main Workspace view.
    /// </summary>
    public IRelayCommand ShowWorkspaceCommand { get; }
    /// <summary>
    /// Gets the command to show the How To Use view.
    /// </summary>
    public IRelayCommand ShowHowToUseCommand { get; }
    /// <summary>
    /// Gets the command to show the Dev view.
    /// </summary>
    public IRelayCommand ShowDevCommand { get; }
    /// <summary>
    /// Gets the command to show the Simulation view.
    /// </summary>
    public IAsyncRelayCommand ShowSimulationCommand { get; }
    /// <summary>
    /// Gets the command to show the Coordinate view.
    /// </summary>
    public IAsyncRelayCommand ShowCoordinateCommand { get; }
    /// <summary>
    /// Gets the command to clear all messages from the console.
    /// </summary>
    public IRelayCommand ClearConsoleCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellViewModel"/> class.
    /// </summary>
    public ShellViewModel()
    {
        StartupPerfTracker.Mark("shell.vm-ctor-start");
        StartupPerfTracker.Mark("shell.freeform-vm-create-start");
        FreeformHelper = new FreeformHelperViewModel();
        StartupPerfTracker.Mark("shell.freeform-vm-created");

        ShowWorkspaceCommand = new RelayCommand(ShowWorkspace);
        ShowHowToUseCommand = new RelayCommand(ShowHowToUse);
        ShowDevCommand = new RelayCommand(ShowDev);
        ShowSimulationCommand = new AsyncRelayCommand(ShowSimulationAsync);
        ShowCoordinateCommand = new AsyncRelayCommand(ShowCoordinateAsync);
        ClearConsoleCommand = new RelayCommand(() => AppLogStore.Instance.Clear()); // Command to clear log store.
        StartupPerfTracker.Mark("shell.commands-initialized");
        FreeformHelper.GetCurrentSimulationSafetyAudit = () => Simulation.CurrentWorkspace?.GetSimulationSafetyAuditSnapshot();
        Simulation.RequestBuildWorkspaceAsync = BuildSimulationWorkspaceAsync;
        Simulation.RequestPrewarmWorkspaceAsync = PrewarmSimulationWorkspaceAsync;
        Simulation.UpdateSourceRevision(FreeformHelper.SimulationWorkspaceSourceRevision);
        AttachSimulationSafetyOverviewSync();
        CoordinatePlanner.RequestBuildWorkspaceAsync = BuildCoordinatePlannerWorkspaceAsync;
        CoordinatePlanner.RequestPrewarmWorkspaceAsync = PrewarmCoordinatePlannerWorkspaceAsync;
        CoordinatePlanner.UpdateSourceRevision(FreeformHelper.WorkspaceDerivedSourceRevision);
        FreeformHelper.WorkspaceDerivedSourceChanged += OnFreeformHelperWorkspaceSourceChanged;

        // Subscribe to changes in the log entries collection to update the console text.
        if (AppLogStore.Instance.Entries is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += OnLogEntriesChanged;
        }

        RefreshConsoleSummaryCounts();
        if (IsConsoleExpanded)
        {
            RebuildConsoleText();
        }

        if (UiThread.TryGetRunningDispatcher(out var dispatcher))
        {
            dispatcher!.Post(() => { StartupPerfTracker.Mark("shell.console-background-ready"); }, DispatcherPriority.Background);
        }
        else
        {
            StartupPerfTracker.Mark("shell.console-background-ready");
        }
        StartupPerfTracker.Mark("shell.console-bound");

        if (!ShouldSkipInitialGridBootstrap())
        {
            FreeformHelper.EnsureInitialGrid();
            StartupPerfTracker.Mark("shell.initial-grid-requested");
        }

        ShowWorkspace(); // Start with the workspace view active.
        StartupPerfTracker.Mark("shell.workspace-activated");
        StartupPerfTracker.Mark("shell.vm-ctor-complete");
    }

}
