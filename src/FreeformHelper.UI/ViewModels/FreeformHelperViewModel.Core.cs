using System.Collections.Frozen;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
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
using NotchResolvedCacheEntry = (int Revision, ulong CadPoolFingerprint, int ActiveRegularHash, ulong GridFingerprint, bool EnableToRegular, bool EnableToFull, bool EnableToFullRuleEngine, bool EnableToFullRuleTrace, double StrictOverlapRatio, int? AnchorIcIndex, int? AnchorDiffIndex, FreeformHelper.Application.Services.NotchV22TargetAllocationAreaMode TargetAllocationAreaMode, FreeformHelper.Application.Services.NotchV22ResolvedResult Resolved);

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
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Gets or sets the <see cref="ICanvasHost"/> reference, allowing the ViewModel
    /// to interact with the canvas control in the View without direct coupling.
    /// </summary>
    public ICanvasHost? CanvasHost { get; set; }

    // --- Delegates for UI-specific Dialogs ---
    // These delegates are set by the View (FreeformHelperView) to enable the ViewModel
    // to trigger platform-specific file dialogs or confirmation prompts.
    public Func<Task<string?>>? PickOpenDxfPathAsync { get; set; }
    public Func<Task<IReadOnlyList<string>>>? PickOpenDiffCsvPathsAsync { get; set; }
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
    public Func<IReadOnlyList<string>, Task<DxfLayerImageExportRequest?>>? OpenDxfLayerImageExportAsync { get; set; }

    // --- Application Services ---
    // Instances of various services responsible for business logic and data manipulation.
    private readonly CadAreaBucketService _cadAreaBucketService = new();
    private readonly JsonProjectStore _projectStore = new();
    private readonly DxfImportService _dxfImportService = new();
    private readonly DxfLayerCatalogReader _dxfLayerCatalogReader = new();
    private readonly GridBuildService _gridBuildService = new();
    private readonly NotchExportService _notchExportService = new();
    private readonly PadMatchService _padMatchService = new();
    private readonly ManualSizingService _manualSizingService = new();

    // --- Internal State Management ---
    private readonly Dictionary<int, double> _cadPadCustomValues = new(); // Stores custom user-defined values for CAD pads.
    private readonly CadEditState _cadEdit = new(); // Holds the hidden, duplicate and baseline data of the CAD edits.
    private readonly CombinedCadState _combinedCad = new();
    private readonly Dictionary<string, WorkspaceHeaderItem> _workspaceHeaderItemMap = new(); // Maps keys to header items.
    private string? _lastSavedPath; // Stores the path where the project was last saved.
    // Independent nesting-safe suppression counters; UI-thread-only access.
    private readonly SettingsSuppressionScopes _settingsSuppression = new();
    private readonly PendingLayerSelectionState _pendingLayerSelection = new(); // Holds the layer selection data that waits for the next CAD load or layer toggle.
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
    // The four per-CAD-pad index tables are stored once. The members below keep the old names and
    // read through it: members that carry analyzer debt read them, and renaming them would change
    // their ledger fingerprints.
    private readonly CadIndexMaps _cadIndexMaps = new();

    internal CadIndexMaps CadIndexMaps => _cadIndexMaps;

    // Maps CAD pad ID -> Step 4 CAD output FW diff idx.
    private Dictionary<int, int> _cadOutputFwDiffIndexByCadId => _cadIndexMaps.OutputFwDiffIndexByCadId;

    // Maps CAD pad ID -> stable display index (L->R, T->B, 1-based).
    private Dictionary<int, int> _cadDisplayIndexByCadId => _cadIndexMaps.DisplayIndexByCadId;

    // Maps CAD pad ID -> inferred IC index (grid partition based).
    private Dictionary<int, int> _cadIcIndexByCadId => _cadIndexMaps.IcIndexByCadId;

    private IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> _latestCadOutputFwDiffAssignmentDecisionsByCadId
    {
        get => _cadIndexMaps.LatestAssignmentDecisionsByCadId;
        set => _cadIndexMaps.LatestAssignmentDecisionsByCadId = value;
    }

    private int _workflowDataSnapshotRevision = 1;
    private readonly GridStartupState _gridStartup = new(); // Holds the one-time startup flags and the fit request of the grid build.
    private readonly GridRebuildUseCase _gridRebuildUseCase;
    private readonly ManualSizingUseCase _manualSizingUseCase;
    private readonly SelectionCoordinator _selectionCoordinator;
    private readonly CadLoadUseCase _cadLoadUseCase;
    private readonly CadLoadWorkflowService _cadLoadWorkflowService;
    private readonly LayerCatalogStateService _layerCatalogStateService;
    private readonly GridRebuildOrchestrator _gridRebuildOrchestrator;
    private readonly ProjectPersistenceUseCase _projectPersistenceUseCase;
    private readonly PadEditUseCase _padEditUseCase;
    private readonly NotchExportGenerationCacheService _notchExportGenerationCacheService = new();
    private readonly DxfRegularMappingUseCase _dxfRegularMappingUseCase = new();
    private readonly CadOutputFwDiffIndexAssignmentService _cadOutputFwDiffIndexAssignmentService = new();
    private readonly UndoService _undoService = new();
    private readonly UiOperationStatusReporter _statusReporter;
    private readonly NotchResultCacheState _notchCache = new(); // Holds the notch compensation and validation caches and the last generated notch table.
    private int _notchCompensationCacheRevision;
    private readonly NotchCompensationCacheCounters _notchCompensationCacheCounters = new();
    private readonly NotchExportFingerprintRevisions _notchExportFingerprintRevisions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FreeformHelperViewModel"/> class.
    /// Sets up default values, initializes UI options, and configures commands.
    /// </summary>
    public FreeformHelperViewModel(AppGeneralSettingsStore? appGeneralSettingsStore = null)
        : this(appGeneralSettingsStore, null)
    {
    }

    internal FreeformHelperViewModel(AppGeneralSettingsStore? appGeneralSettingsStore, UiEventRunner? uiEvents)
    {
        _showInternalLegacyNotchFields = false;
        StartupPerfTracker.Mark("workspace.vm-ctor-start");
        _statusReporter = new UiOperationStatusReporter(message => StatusText = message);
        UiEvents = uiEvents ?? new UiEventRunner(new UiEventFailureReporter(_statusReporter).Report, EmergencyLogSink.Report);
        _appGeneralSettingsStore = appGeneralSettingsStore ?? new AppGeneralSettingsStore();
        InitializeAppGeneralSettingsPersistence();
        // Populate dropdown options for ScanOrder.
        ScanOrders = Enum.GetValues<ScanOrder>().ToList();
        ScanOrderOptions = new List<ScanOrderOption>
        {
            new(ScanOrder.LeftToRight_TopToBottom, "L->R, T->B"),
            new(ScanOrder.RightToLeft_TopToBottom, "R->L, T->B"),
            new(ScanOrder.LeftToRight_BottomToTop, "L->R, B->T"),
            new(ScanOrder.RightToLeft_BottomToTop, "R->L, B->T"),
        };
        // Populate dropdown options for GridAlignment.
        GridAlignmentOptions = new List<GridAlignmentOption>
        {
            new(GridAlignmentMode.FromPanelAa, "Use panel AA"),
            new(GridAlignmentMode.FromCadBounds, "Use DXF bounds"),
        };
        RegularSourceModeOptions = new List<RegularSourceModeOption>
        {
            new(RegularSourceMode.GeneratedGrid, "Generated grid"),
            new(RegularSourceMode.FromDxfLayer, "DXF layer pads"),
        };
        DxfEditRotationScopeOptions = new List<DxfEditRotationScopeOption>
        {
            new(DxfEditRotationScope.SelectedPads, "Selected pads", "Rotate the currently selected baseline CAD pads as one rigid group."),
            new(DxfEditRotationScope.TargetLayer, "Target layer", "Rotate all visible baseline CAD pads in the selected rotation layer as one rigid group."),
        };
        LogLevelOptions = LoggingBootstrapper.SupportedMinimumLevels
            .Select(static level => new LogLevelOption(level, level))
            .ToList();
        CadOutputFwDiffAutoModeOptions = new List<CadOutputFwDiffAutoModeOption>
        {
            new(
                CadOutputFwDiffAutoMode.BestMatchDirect,
                "Geometry seed",
                "Use each CAD pad's geometry-seeded regular diff directly. Choose this when hardware behavior should follow the primary regular one-to-one."),
            new(
                CadOutputFwDiffAutoMode.StrictUnique,
                "Geometry seed (unique per IC)",
                "Use geometry seed directly, but keep diff idx unique within each IC. Conflicts stay unresolved instead of shifting other CAD pads."),
        };
        NotchExportFileTypeOptions = new List<NotchExportFileTypeOption>
        {
            new(NotchExportFileType.Csv, "CSV review (.csv)", "csv"),
            new(NotchExportFileType.Cv21, "C v2.1 (.c)", "c", NotchAlgorithmVersion.V21),
            new(NotchExportFileType.Cv22, "C v2.2 (.c)", "c", NotchAlgorithmVersion.V22),
        };
        NotchExportProfileOptions = new List<NotchExportProfileOption>
        {
            new(NotchExportProfile.Release, "Release (minimal FW)"),
            new(NotchExportProfile.Debug, "Debug (trace + sim mask)"),
        };
        NotchCompensationModelOptions = new List<NotchCompensationModelOption>
        {
            new(
                NotchCompensationModel.CurrentGain,
                "Current (Gain)",
                "To Regular x To Full (current model)."),
            new(
                NotchCompensationModel.ConservativeNoGain,
                "Conservative (No Gain)",
                "Apply ToRegular source combine; ToFull only gates boundary support."),
            new(
                NotchCompensationModel.Disabled,
                "Disabled",
                "Turn off To Full and To Regular for baseline comparison."),
        };
        _selectedNotchExportFileTypeOption = NotchExportFileTypeOptions[0];
        _selectedNotchExportProfileOption = NotchExportProfileOptions[0];
        _selectedNotchCompensationModelOption = NotchCompensationModelOptions[0];
        var initialLogLevelName = LoggingBootstrapper.GetCurrentMinimumLevelName();
        var initialLogLevel = LogLevelOptions.FirstOrDefault(option =>
            string.Equals(option.Value, initialLogLevelName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(initialLogLevel.Display))
        {
            initialLogLevel = LogLevelOptions.Count > 0 ? LogLevelOptions[0] : default;
        }
        if (string.IsNullOrWhiteSpace(initialLogLevel.Display))
        {
            initialLogLevel = new LogLevelOption("Info", "Info");
        }
        _selectedLogLevelOption = initialLogLevel;
        using (_settingsSuppression.EnterBoundLayerSelectionChange())
        {
            BoundLayerOptions.Add(new BoundLayerOption(null, "Auto (visible layers)"));
            SelectedBoundLayerOption = BoundLayerOptions[0];
        }
        using (_settingsSuppression.EnterRegularSourceLayerSelectionChange())
        {
            RegularSourceLayerOptions.Add(new RegularSourceLayerOption(null, "Select layer"));
            SelectedRegularSourceLayerOption = RegularSourceLayerOptions[0];
        }
        _dxfEditPanel = _dxfEditPanel with { SelectedDxfEditRotationScopeOption = DxfEditRotationScopeOptions[0] };
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
        InitializeCommands(); // Set up all UI commands.
        StartupPerfTracker.Mark("workspace.vm-commands-initialized");
        // Subscribe to selection changes from the InteractionState.
        InteractionState.SelectionChanged += OnInteractionSelectionChanged;
        _gridRebuildUseCase = new GridRebuildUseCase(RequestFitAfterNextRebuild, RebuildGridAsync);
        _manualSizingUseCase = new ManualSizingUseCase(_manualSizingService);
        _selectionCoordinator = new SelectionCoordinator(InteractionState);
        _cadLoadUseCase = new CadLoadUseCase(_dxfImportService);
        _cadLoadWorkflowService = new CadLoadWorkflowService(_cadLoadUseCase);
        _layerCatalogStateService = new LayerCatalogStateService(_dxfLayerCatalogReader);
        _gridRebuildOrchestrator = new GridRebuildOrchestrator(_gridBuildService);
        _projectPersistenceUseCase = new ProjectPersistenceUseCase(_projectStore);
        _padEditUseCase = new PadEditUseCase(_manualSizingService);
        StartupPerfTracker.Mark("workspace.vm-usecases-initialized");
        Logger.Debug(CultureInfo.InvariantCulture, "FreeformHelperViewModel initialized.");
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
        if (CanvasHost is null)
        {
            return; // Cannot fit if canvas host is not available.
        }

        // If there's data to display, fit it immediately.
        if (CadPads.Count > 0 || RegularPads.Count > 0)
        {
            CanvasHost.FitToContent();
        }
        else
        {
            // If no data, but potentially will have data after a grid rebuild,
            // schedule a fit after the next rebuild.
            _gridStartup.FitAfterNextBuild = true;
        }
    }

    public bool TryGetCadOutputFwDiffIndex(int cadPadId, out int dxfIndex)
    {
        return _cadOutputFwDiffIndexByCadId.TryGetValue(cadPadId, out dxfIndex);
    }

    internal int WorkflowDataSnapshotRevision => _workflowDataSnapshotRevision;

    /// <summary>Gets the pad match and workflow snapshot holder for tests.</summary>
    internal WorkflowDataState WorkflowDataForTests => _workflowData;

    /// <summary>Gets the notch result cache holder for tests.</summary>
    internal NotchResultCacheState NotchCacheForTests => _notchCache;

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

    private void SetLatestCadOutputFwDiffAssignmentDecisions(IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> decisions)
    {
        _latestCadOutputFwDiffAssignmentDecisionsByCadId = decisions;
        InvalidateWorkflowDataSnapshot();
    }

    public bool TryGetCadIcIndex(int cadPadId, out int icIndex)
    {
        return _cadIcIndexByCadId.TryGetValue(cadPadId, out icIndex);
    }

    public bool TryGetCadDisplayIndex(int cadPadId, out int displayIndex)
    {
        return _cadDisplayIndexByCadId.TryGetValue(cadPadId, out displayIndex);
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

    /// <summary>
    /// Holds the notch compensation result cache, the notch validation bucket cache, the last generated notch table and the last preview visibility seen by playback, for the main view model.
    /// </summary>
    internal sealed class NotchResultCacheState
    {
        /// <summary>Gets the resolved notch result cache keyed by CAD pad id.</summary>
        internal Dictionary<int, NotchResolvedCacheEntry> ResolvedResultByCadId { get; } = new();

        /// <summary>Gets or sets the cached validation bucket with the table and null diff value it was built for.</summary>
        internal (NotchValidationBucket Bucket, NotchTable Table, int NullDiffValue)? ValidationBucket { get; set; }

        /// <summary>Gets or sets the table of the last notch generation.</summary>
        internal NotchTable? LastGeneratedTable { get; set; }

        /// <summary>Gets or sets the preview visibility that the last playback sync saw.</summary>
        internal bool LastPreviewVisibleForPlayback { get; set; }
    }
}

