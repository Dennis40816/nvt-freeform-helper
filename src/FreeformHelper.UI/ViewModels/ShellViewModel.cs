using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using NLog;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// The ShellViewModel serves as the main ViewModel for the application's shell (main window).
/// It manages which sub-ViewModel is currently active, handles navigation, and provides
/// access to application-wide services like logging.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private ConsoleSession? _console;

    // Tests pass their own probe to hold a workspace build back.
    private readonly IShellTimingProbe _timingProbe;
    private bool _isDisposed;
    // Navigation selection is owned by the shell's UI-thread command entry point.
    private ShellPage _selectedPage = ShellPage.Workspace;

    // --- Sub-Viewmodels ---
    public FreeformHelperViewModel FreeformHelper { get; }
    internal UiEventRunner UiEvents => FreeformHelper.UiEvents;
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

    internal const string WorkspaceSourceChangeDrainWorkName = "workspace-source-change-drain";

    // Starts the workspace source change drain. Tests replace its runner to observe the drain and its failure.
    internal BackgroundWorkRunnerSlot BackgroundWork { get; } = new();

    public HowToUseViewModel HowToUse => _howToUse ??= new HowToUseViewModel();
    public DevViewModel Dev => _dev ??= new DevViewModel { UiEvents = UiEvents };
    public SimulationHostViewModel Simulation => _simulation ??= new SimulationHostViewModel();
    public CoordinatePlannerHostViewModel CoordinatePlanner => _coordinatePlanner ??= new CoordinatePlannerHostViewModel();

    /// <summary>
    /// Gets the selected page's ViewModel for the main content area.
    /// </summary>
    public object CurrentViewModel => GetPageViewModel(_selectedPage);

    internal ShellPage SelectedPage => _selectedPage;

    // --- Navigation State Flags ---
    /// <summary>
    /// Gets a value indicating whether the main Workspace view is currently active.
    /// </summary>
    public bool IsWorkspaceActive => _selectedPage == ShellPage.Workspace;
    /// <summary>
    /// Gets a value indicating whether the How To Use view is currently active.
    /// </summary>
    public bool IsHowToUseActive => _selectedPage == ShellPage.HowToUse;
    /// <summary>
    /// Gets a value indicating whether the Dev view is currently active.
    /// </summary>
    public bool IsDevActive => _selectedPage == ShellPage.Dev;
    /// <summary>
    /// Gets a value indicating whether the Dev page is offered. Release builds hide it. A test sets it.
    /// </summary>
    public bool IsDevPageAvailable { get; init; } = AppBuildChannel.IsDevelopmentBuild;
    /// <summary>
    /// Gets a value indicating whether the Simulation view is currently active.
    /// </summary>
    public bool IsSimulationActive => _selectedPage == ShellPage.Simulation;
    /// <summary>
    /// Gets a value indicating whether the Coordinate page is currently active.
    /// </summary>
    public bool IsCoordinateActive => _selectedPage == ShellPage.Coordinate;

    // --- Console/Log Display ---
    /// <summary>
    /// Gets or sets a value indicating whether the in-app console panel is expanded.
    /// </summary>
    [ObservableProperty]
    private bool _isConsoleExpanded = true;

    /// <summary>
    /// Gets the log store that the console shows. The default is the process-wide store that the NLog target writes to.
    /// A test supplies a store of its own, so it never has to clear or filter the shared one.
    /// </summary>
    public Nvt.Core.LogConsole.LogStore ConsoleStore { get; init; } = AppConsoleLog.Store;

    /// <summary>
    /// Gets the console session that the console panels bind to. The first read creates it and must run on the UI thread.
    /// </summary>
    public ConsoleSession ConsoleSession => _console ??= new ConsoleSession(ConsoleStore, UiEvents, new ConsoleLinkOpener());

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
    /// Initializes a new instance of the <see cref="ShellViewModel"/> class.
    /// </summary>
    public ShellViewModel()
        : this(null)
    {
    }

    internal ShellViewModel(FreeformHelperViewModel? freeformHelper)
        : this(freeformHelper, NullShellTimingProbe.Instance)
    {
    }

    internal ShellViewModel(FreeformHelperViewModel? freeformHelper, IShellTimingProbe timingProbe)
    {
        ArgumentNullException.ThrowIfNull(timingProbe);
        _timingProbe = timingProbe;
        StartupPerfTracker.Mark("shell.vm-ctor-start");
        StartupPerfTracker.Mark("shell.freeform-vm-create-start");
        FreeformHelper = freeformHelper ?? new FreeformHelperViewModel();
        StartupPerfTracker.Mark("shell.freeform-vm-created");

        ShowWorkspaceCommand = new RelayCommand(ShowWorkspace);
        ShowHowToUseCommand = new RelayCommand(ShowHowToUse);
        ShowDevCommand = new RelayCommand(ShowDev);
        ShowSimulationCommand = new AsyncRelayCommand(ShowSimulationAsync);
        ShowCoordinateCommand = new AsyncRelayCommand(ShowCoordinateAsync);
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

        if (!ShouldSkipInitialGridBootstrap())
        {
            FreeformHelper.EnsureInitialGrid();
            StartupPerfTracker.Mark("shell.initial-grid-requested");
        }

        ShowWorkspace(); // Start with the workspace view active.
        StartupPerfTracker.Mark("shell.workspace-activated");
        StartupPerfTracker.Mark("shell.vm-ctor-complete");
    }

    private static bool ShouldSkipInitialGridBootstrap()
    {
        if (AppContext.TryGetSwitch("FreeformHelper.DisableInitialGridBootstrap", out var disabledBySwitch) &&
            disabledBySwitch)
        {
            return true;
        }

        var processName = Environment.ProcessPath is { Length: > 0 }
            ? Path.GetFileNameWithoutExtension(Environment.ProcessPath)
            : string.Empty;
        return processName.Contains("testhost", StringComparison.OrdinalIgnoreCase);
    }
}
