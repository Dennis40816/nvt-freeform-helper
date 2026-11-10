using System.Collections.Frozen;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using NLog;
using Nvt.Core.Lifecycle;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class defines the core functionalities and state management
/// for the <see cref="FreeformHelperViewModel"/>. It acts as the central hub
/// coordinating between the UI (via commands and observable properties),
/// application services, and data models.
/// </summary>
public sealed partial class FreeformHelperViewModel : ObservableObject
{
    internal UiEventRunner UiEvents { get; }

    // Starts the fire-and-forget background loops and refreshes. Tests replace its runner to observe them.
    internal BackgroundWorkRunnerSlot BackgroundWork { get; } = new();

    // Logger instance for recording events and debugging information.
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Gets or sets the <see cref="ICanvasHost"/> reference, allowing the ViewModel
    /// to interact with the canvas control in the View without direct coupling.
    /// </summary>
    public ICanvasHost? CanvasHost { get; set; }

    // --- Delegates for UI-specific Dialogs ---
    // These delegates are set by the View (FreeformHelperView) to enable the ViewModel
    // to trigger platform-specific file dialogs or confirmation prompts.
    public Func<Task<string?>>? PickOpenDxfPathAsync { get; set; }
    public Func<Task<string?>>? PickOpenRegularVisibilityMaskPathAsync { get; set; }
    public Func<Task<string?>>? PickLoadProjectPathAsync { get; set; }
    public Func<Task<string?>>? PickSaveProjectPathAsync { get; set; }
    public Func<string, string, Task<string?>>? PickSaveNotchPathAsync { get; set; }
    public Func<string, string, Task<string?>>? PickSaveExportPathAsync { get; set; }
    public Func<Task<bool>>? ConfirmEmbedDxfAsync { get; set; }
    public Func<string, string, Task>? ShowWarningAsync { get; set; }
    public Func<bool>? EnsureNoPendingEdits { get; set; }
    public Func<NotchDetailViewModel, Task>? OpenNotchDetailAsync { get; set; }
    public Func<NotchExportSelectionViewModel, Task<NotchTable?>>? OpenNotchExportSelectionAsync { get; set; }
    public Func<SimulationSafetyAuditResult?>? GetCurrentSimulationSafetyAudit { get; set; }
    public Func<DxfEditChangeListViewModel, Task>? OpenDxfEditChangeListAsync { get; set; }
    public Func<IndexMappingReportViewModel, Task>? OpenIndexMappingReportAsync { get; set; }
    public Func<DxfOverlapReportViewModel, Task>? OpenDxfOverlapReportAsync { get; set; }
    public Func<LayerImageExportContext, Task<LayerImageExportRequest?>>? OpenDxfLayerImageExportAsync { get; set; }

    // --- Application Services ---
    // Instances of various services responsible for business logic and data manipulation.
    private readonly JsonProjectStore _projectStore = new();
    private readonly DxfImportService _dxfImportService = new();
    private readonly DxfLayerCatalogReader _dxfLayerCatalogReader = new();
    private readonly NotchExportService _notchExportService = new();

    // --- Internal State Management ---
    private readonly Dictionary<int, double> _cadPadCustomValues = new(); // Stores custom user-defined values for CAD pads.
    private readonly Dictionary<string, WorkspaceHeaderItem> _workspaceHeaderItemMap = new(); // Maps keys to header items.
    private string? _lastSavedPath; // Stores the path where the project was last saved.
    // Independent nesting-safe suppression counters; UI-thread-only access.
    private readonly SettingsSuppressionScopes _settingsSuppression = new();
    private ProjectFile _projectFile = new(); // The core project data model.

    /// <summary>
    /// Gets the <see cref="WorkspaceInteractionState"/> instance, which manages global UI interaction states
    /// such as current selection and active info popovers.
    /// </summary>
    public WorkspaceInteractionState InteractionState { get; } = new();

    private CadPadSet? _cad; // The loaded CAD pad data.
    private RegularGrid? _grid; // The generated regular grid data.
    private readonly WorkflowDataState _workflowData = new(); // Holds the latest pad match result and the cached workflow data snapshot built from it.
    private CadPadSet? _cachedFilteredCadForBuild; // Cache for layer-filtered CAD pads.

    private int _workflowDataSnapshotRevision = 1;
    private readonly GridStartupState _gridStartup = new(); // Holds the one-time startup flags and the fit request of the grid build.
    private readonly GridRebuildUseCase _gridRebuildUseCase;
    private readonly SelectionCoordinator _selectionCoordinator;
    private readonly CadLoadUseCase _cadLoadUseCase;
    private readonly CadLoadWorkflowService _cadLoadWorkflowService;
    private readonly LayerCatalogStateService _layerCatalogStateService;
    private readonly ProjectPersistenceUseCase _projectPersistenceUseCase;
    private readonly UndoService _undoService = new();
    private readonly UiOperationStatusReporter _statusReporter;

    /// <summary>Gets the confirmation hooks for project save and load that the View wires.</summary>
    public ProjectDialogHooks ProjectDialogs { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FreeformHelperViewModel"/> class.
    /// Sets up default values, initializes UI options, and configures commands.
    /// </summary>
    public FreeformHelperViewModel(AppGeneralSettingsStore? appGeneralSettingsStore = null)
        : this(appGeneralSettingsStore, null)
    {
    }

    private readonly ProjectEditingSession _projectEditingSession;

    internal FreeformHelperViewModel(AppGeneralSettingsStore? appGeneralSettingsStore, UiEventRunner? uiEvents)
    {
        _projectEditingSession = new ProjectEditingSession(PublishProjectEditingAvailabilityChange);
        StartupPerfTracker.Mark("workspace.vm-ctor-start");
        _statusReporter = new UiOperationStatusReporter(message => StatusText = message);
        UiEvents = uiEvents ?? new UiEventRunner(new UiEventFailureReporter(_statusReporter).Report, EmergencyLogSink.Report);
        _appGeneralSettingsStore = appGeneralSettingsStore ?? new AppGeneralSettingsStore();
        InitializeAppGeneralSettingsPersistence();
        ScanOrderOptions = BuildScanOrderOptions();
        GridAlignmentOptions = BuildGridAlignmentOptions();
        RegularSourceModeOptions = BuildRegularSourceModeOptions();
        DxfEditRotationScopeOptions = BuildDxfEditRotationScopeOptions();
        LogLevelOptions = BuildLogLevelOptions();
        CadOutputFwDiffAutoModeOptions = BuildCadOutputFwDiffAutoModeOptions();
        NotchExportFileTypeOptions = BuildNotchExportFileTypeOptions();
        NotchExportProfileOptions = BuildNotchExportProfileOptions();
        NotchCompensationModelOptions = BuildNotchCompensationModelOptions();
        _selectedNotchExportFileTypeOption = NotchExportFileTypeOptions[0];
        _selectedNotchExportProfileOption = NotchExportProfileOptions[0];
        _selectedNotchCompensationModelOption = NotchCompensationModelOptions[0];
        _selectedLogLevelOption = ResolveInitialLogLevelOption();
        InitializeLayerSelectionOptions();
        InitializeDxfEditOptionSelection();
        StartupPerfTracker.Mark("workspace.vm-options-initialized");
        // Layered settings precedence: Project > App General > Built-in defaults.
        // At startup without a project file loaded, app general overrides default values.
        TryApplyAppGeneralSettingsLayer();
        _appGeneralPersistence.SuppressPersistence = true;
        ApplyCanvasColorDefaultsFromTokens();
        // Load initial settings to UI properties from layered settings snapshot.
        LoadSettingsToUi(_projectFile.Settings);
        ApplyViewSnapshot(_projectFile.UiSnapshot.View);
        ApplyImportSnapshot(_projectFile.UiSnapshot.Import);
        _appGeneralPersistence.SuppressPersistence = false;
        StartupPerfTracker.Mark("workspace.vm-settings-loaded");
        InitializeWorkspaceHeaderItems(); // Set up items for the workspace header menu.
        StartupPerfTracker.Mark("workspace.vm-header-initialized");
        RebuildCascadeSettings(); // Initialize cascade-related settings.
        StartupPerfTracker.Mark("workspace.vm-cascade-initialized");
        _notchCommands = CreateNotchCommands(); // Register Notch commands first.
        _projectCommands = CreateProjectCommands(); // Register project commands next.
        _selectionCommands = CreateSelectionCommands(); // Register selection commands next.
        _dxfEditCommands = CreateDxfEditCommands(); // Keep DXF edit command registration next.
        _generalCommands = CreateGeneralCommands(); // Finish command registration in the original order.
        StartupPerfTracker.Mark("workspace.vm-commands-initialized");
        // Subscribe to selection changes from the InteractionState.
        InteractionState.SelectionChanged += OnInteractionSelectionChanged;
        _gridRebuildUseCase = new GridRebuildUseCase(RequestFitAfterNextRebuild, RebuildGridAsync);
        _selectionCoordinator = new SelectionCoordinator(InteractionState);
        _cadLoadUseCase = new CadLoadUseCase(_dxfImportService);
        _cadLoadWorkflowService = new CadLoadWorkflowService(_cadLoadUseCase);
        _layerCatalogStateService = new LayerCatalogStateService(_dxfLayerCatalogReader);
        _projectPersistenceUseCase = new ProjectPersistenceUseCase(_projectStore);
        StartupPerfTracker.Mark("workspace.vm-usecases-initialized");
        _logger.Debug(CultureInfo.InvariantCulture, "FreeformHelperViewModel initialized.");
        StartupPerfTracker.Mark("workspace.vm-ctor-complete");
    }

    private void RequestFitAfterNextRebuild()
    {
        _gridStartup.FitAfterNextBuild = true;
    }

    /// <summary>
    /// Ensures an initial grid is built when the application starts if no CAD data
    /// or existing grid is present. This is typically for displaying an empty grid initially.
    /// </summary>
    public void EnsureInitialGrid()
    {
        // If CAD data is already loaded or a grid already exists, no initial grid is needed.
        if (_cad is not null || RegularPads.Count > 0 || _gridStartup.InitialBuildRequested)
        {
            return;
        }

        _gridStartup.InitialBuildRequested = true;
        _ = RequestInitialGridBuildAsync();
    }

    internal Task WaitForGridRebuildIdleAsync()
    {
        return _gridRebuildUseCase.WaitForIdleAsync();
    }

    private async Task RequestInitialGridBuildAsync()
    {
        try
        {
            // Yield once so explicit test/setup assignments (CAD/grid) can settle first.
            await Task.Yield();
            if (_cad is not null || _grid is not null || CadPads.Count > 0 || RegularPads.Count > 0)
            {
                return;
            }

            await TriggerGridRebuildAsync(requestFit: true);
        }
        finally
        {
            _gridStartup.InitialBuildRequested = false;
        }
    }

    /// <summary>
    /// Ensures that the canvas content is fitted to view upon initial load,
    /// or schedules it if the canvas host is not yet available.
    /// </summary>
    public void EnsureInitialFit()
    {
        var action = InitialCanvasFitPolicy.Decide(CanvasHost is not null, CadPads.Count > 0, RegularPads.Count > 0);
        if (action == InitialFitAction.None)
        {
            return; // Cannot fit if canvas host is not available.
        }

        // If there's data to display, fit it immediately.
        if (action == InitialFitAction.FitNow)
        {
            CanvasHost?.FitToContent();
        }
        else
        {
            // If no data, but potentially will have data after a grid rebuild,
            // schedule a fit after the next rebuild.
            _gridStartup.FitAfterNextBuild = true;
        }
    }

    /// <summary>Gets the pad match and workflow snapshot holder for tests.</summary>
    internal WorkflowDataState WorkflowDataForTests => _workflowData;

    internal int WorkflowDataSnapshotBuildCount { get; private set; }

    private WorkflowDataSnapshot BuildWorkflowDataSnapshot()
    {
        if (_workflowData.SnapshotCache is { } cached &&
            cached.Revision == _workflowDataSnapshotRevision)
        {
            return cached.Snapshot;
        }

        var activeMask = GetActiveRegularVisibilityMaskPadIds()?.ToFrozenSet();

        var cadToRegular = _workflowData.LatestPadMatch.CadToRegular
            .ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<PadMatchLink>)pair.Value.ToArray())
            .ToFrozenDictionary();

        var snapshot = new WorkflowDataSnapshot(
            _cadOutputFwDiffIndexByCadId.ToFrozenDictionary(),
            _latestCadOutputFwDiffAssignmentDecisionsByCadId.ToFrozenDictionary(),
            _cadIcIndexByCadId.ToFrozenDictionary(),
            _cadDisplayIndexByCadId.ToFrozenDictionary(),
            cadToRegular,
            _projectFile.CadOutputFwDiffIndexOverrides.ToFrozenDictionary(),
            activeMask);
        _workflowData.SnapshotCache = (_workflowDataSnapshotRevision, snapshot);
        WorkflowDataSnapshotBuildCount++;
        return snapshot;
    }

    internal void InvalidateWorkflowDataSnapshot()
    {
        _workflowDataSnapshotRevision++;
    }

    private void SetLatestPadMatchResult(PadMatchResult result)
    {
        _workflowData.LatestPadMatch = result;
        InvalidateWorkflowDataSnapshot();
    }

    /// <summary>
    /// Holds the latest pad match result and the cached workflow data snapshot with the revision it was built for, for the main view model.
    /// </summary>
    internal sealed class WorkflowDataState
    {
        /// <summary>Gets or sets the pad match result of the last match run.</summary>
        internal PadMatchResult LatestPadMatch { get; set; } = PadMatchResult.Empty;

        /// <summary>Gets or sets the last built workflow data snapshot with the revision it was built for.</summary>
        internal (int Revision, WorkflowDataSnapshot Snapshot)? SnapshotCache { get; set; }
    }
}
