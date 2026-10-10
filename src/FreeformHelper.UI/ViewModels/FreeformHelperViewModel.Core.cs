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
    private readonly DxfOverlapAnalyzer _dxfOverlapAnalyzer = new();
    private readonly CadAreaBucketService _cadAreaBucketService = new();
    private readonly JsonProjectStore _projectStore = new();
    private readonly DxfImportService _dxfImportService = new();
    private readonly DxfLayerCatalogReader _dxfLayerCatalogReader = new();
    private readonly GridBuildService _gridBuildService = new();
    private readonly NotchExportService _notchExportService = new();
    private readonly DxfExportService _dxfExportService = new();
    private readonly CadPadUnionService _cadPadUnionService = new();
    private readonly PadMatchService _padMatchService = new();
    private readonly FreeformStatisticsBuilder _freeformStatisticsBuilder = new();
    private readonly DxfIndexAssigner _dxfIndexAssigner = new();
    private readonly ManualSizingService _manualSizingService = new();
    private readonly PadOverrideService _padOverrideService = new();

    // --- Internal State Management ---
    private readonly Dictionary<int, double> _cadPadCustomValues = new(); // Stores custom user-defined values for CAD pads.
    private readonly HashSet<int> _hiddenCadPadIds = new();
    private readonly HashSet<int> _autoHiddenDuplicateCadPadIds = new();
    private readonly Dictionary<int, int> _duplicateCadPadIdToCanonicalId = new();
    private readonly Stack<List<int>> _hiddenCadUndoStack = new();
    private readonly HashSet<int> _combinedCadPadIds = new();
    private readonly HashSet<int> _combinedSourceCadPadIds = new();
    private readonly Dictionary<int, int> _combinedCadGroupIdByOutputCadId = new();
    private readonly Dictionary<int, int> _combinedCadGroupIdBySourceCadId = new();
    private readonly Dictionary<int, CombinedCadGroupState> _combinedCadGroups = new();
    private int _nextCombinedCadGroupId = 1;
    private int _nextCombinedCadPadId = 1_500_000_000;
    private CadPadSet? _dxfEditBaselineCad;
    private readonly Dictionary<string, WorkspaceHeaderItem> _workspaceHeaderItemMap = new(); // Maps keys to header items.
    private string? _lastSavedPath; // Stores the path where the project was last saved.
    private bool _suppressCascadeRebuild; // Flag to temporarily suppress cascading rebuilds.
    private bool _isLoadingSettings; // Flag to indicate that settings are currently being loaded.
    private bool _suppressLayerToggleChange; // Flag to suppress layer toggle change handling during batch updates.
    private Dictionary<string, bool>? _pendingLayerSelectionUndoSnapshot;
    private bool _suppressBoundLayerSelectionChange;
    private bool _suppressRegularSourceLayerSelectionChange;
    private bool _suppressScanOrderSelectionSync;
    private bool _suppressGridAlignmentSelectionSync;
    private bool _suppressRegularSourceModeSelectionSync;
    private bool _suppressCadOutputFwDiffAutoModeSelectionSync;
    private bool _suppressDxfIndexAnchorChange;
    private string? _pendingBoundLayerName;
    private string? _pendingRegularSourceLayerName;
    private ProjectFile _projectFile = new(); // The core project data model.

    /// <summary>
    /// Gets the <see cref="WorkspaceInteractionState"/> instance, which manages global UI interaction states
    /// such as current selection and active info popovers.
    /// </summary>
    public WorkspaceInteractionState InteractionState { get; } = new();

    private CadPadSet? _cad; // The loaded CAD pad data.
    private RegularGrid? _grid; // The generated regular grid data.
    private PadMatchResult _latestPadMatchResult = PadMatchResult.Empty;
    private CadPadSet? _cachedFilteredCadForBuild; // Cache for layer-filtered CAD pads.
    private readonly Dictionary<int, int> _cadOutputFwDiffIndexByCadId = new(); // Maps CAD pad ID -> Step 4 CAD output FW diff idx.
    private readonly Dictionary<int, int> _cadDisplayIndexByCadId = new(); // Maps CAD pad ID -> stable display index (L->R, T->B, 1-based).
    private readonly Dictionary<int, int> _cadIcIndexByCadId = new(); // Maps CAD pad ID -> inferred IC index (grid partition based).
    private IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> _latestCadOutputFwDiffAssignmentDecisionsByCadId =
        new Dictionary<int, CadOutputFwDiffAssignmentDecision>();
    private WorkflowDataSnapshot _workflowDataSnapshotCache = WorkflowDataSnapshot.Empty;
    private bool _isWorkflowDataSnapshotCacheValid;
    private int _workflowDataSnapshotRevision = 1;
    private int _workflowDataSnapshotCacheRevision;
    private bool _fitAfterNextGridBuild; // Flag to request a canvas fit-to-content after the next grid rebuild.
    private readonly GridRebuildUseCase _gridRebuildUseCase;
    private readonly SelectionSummaryUseCase _selectionSummaryUseCase = new();
    private readonly ManualSizingUseCase _manualSizingUseCase;
    private readonly SelectionCoordinator _selectionCoordinator;
    private readonly LayerFilterUseCase _layerFilterUseCase = new();
    private readonly WorkflowStepGateService _workflowStepGateService = new();
    private readonly WorkflowPipelineService _workflowPipelineService = new();
    private readonly CadLoadUseCase _cadLoadUseCase;
    private readonly CadLoadWorkflowService _cadLoadWorkflowService;
    private readonly LayerCatalogStateService _layerCatalogStateService;
    private readonly GridRebuildOrchestrator _gridRebuildOrchestrator;
    private readonly SettingsChangePolicy _settingsChangePolicy = new();
    private readonly ProjectPersistenceUseCase _projectPersistenceUseCase;
    private readonly PadEditUseCase _padEditUseCase;
    private readonly NotchValidationUseCase _notchValidationUseCase = new();
    private readonly NotchValidationTraceService _notchValidationTraceService = new();
    private readonly NotchExportGenerationCacheService _notchExportGenerationCacheService = new();
    private readonly NotchOverlayVisibilityPolicy _notchOverlayVisibilityPolicy = new();
    private readonly DxfRegularMappingUseCase _dxfRegularMappingUseCase = new();
    private readonly CadOutputFwDiffIndexAssignmentService _cadOutputFwDiffIndexAssignmentService = new();
    private readonly UndoService _undoService = new();
    private readonly UiOperationStatusReporter _statusReporter;
    private readonly NotchApplySimulationReviewUseCase _notchApplySimulationReviewUseCase = new();
    private readonly Dictionary<int, (
        int Revision, ulong CadPoolFingerprint, int ActiveRegularHash, ulong GridFingerprint,
        bool EnableToRegular, bool EnableToFull, bool EnableToFullRuleEngine, bool EnableToFullRuleTrace,
        double StrictOverlapRatio, int? AnchorIcIndex, int? AnchorDiffIndex,
        NotchV22TargetAllocationAreaMode TargetAllocationAreaMode, NotchV22ResolvedResult Resolved)>
        _notchResolvedResultCacheByCadId = new();
    private NotchValidationBucket? _notchValidationBucketCache;
    private NotchTable? _notchValidationBucketTable;
    private int _notchValidationBucketNullDiffValue = -1;
    private NotchTable? _lastGeneratedNotchTable;
    private int _notchCompensationCacheRevision;
    private long _notchCompensationCacheHitCount;
    private long _notchCompensationCacheMissCount;
    private long _notchCompensationCacheInvalidationCount;
    private int _notchCompensationCacheLastClearSize;
    private bool _lastNotchPreviewVisibleForPlayback;
    private bool _startupInitialGridBuiltMarked;
    private bool _initialGridBuildRequested;

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
        _selectedCadOutputFwDiffAutoModeOption = CadOutputFwDiffAutoModeOptions[0];
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
        _suppressBoundLayerSelectionChange = true;
        BoundLayerOptions.Add(new BoundLayerOption(null, "Auto (visible layers)"));
        SelectedBoundLayerOption = BoundLayerOptions[0];
        _suppressBoundLayerSelectionChange = false;
        _suppressRegularSourceLayerSelectionChange = true;
        RegularSourceLayerOptions.Add(new RegularSourceLayerOption(null, "Select layer"));
        SelectedRegularSourceLayerOption = RegularSourceLayerOptions[0];
        _suppressRegularSourceLayerSelectionChange = false;
        _selectedDxfEditRotationScopeOption = DxfEditRotationScopeOptions[0];
        StartupPerfTracker.Mark("workspace.vm-options-initialized");

        // Layered settings precedence: Project > App General > Built-in defaults.
        // At startup without a project file loaded, app general overrides default values.
        TryApplyAppGeneralSettingsLayer();
        _suppressAppGeneralPersistence = true;
        ApplyCanvasColorDefaultsFromTokens();
        // Load initial settings to UI properties from layered settings snapshot.
        LoadSettingsToUi(_projectFile.Settings);
        ApplyViewSnapshot(_projectFile.UiSnapshot.View);
        ApplyImportSnapshot(_projectFile.UiSnapshot.Import);
        _suppressAppGeneralPersistence = false;
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
        _gridRebuildOrchestrator = new GridRebuildOrchestrator(_gridBuildService, _padOverrideService);
        _projectPersistenceUseCase = new ProjectPersistenceUseCase(_projectStore, _padOverrideService);
        _padEditUseCase = new PadEditUseCase(_manualSizingService);
        StartupPerfTracker.Mark("workspace.vm-usecases-initialized");

        Logger.Debug(CultureInfo.InvariantCulture, "FreeformHelperViewModel initialized.");
        StartupPerfTracker.Mark("workspace.vm-ctor-complete");
    }

    private void RequestFitAfterNextRebuild()
    {
        _fitAfterNextGridBuild = true;
    }

    /// <summary>
    /// Ensures an initial grid is built when the application starts if no CAD data
    /// or existing grid is present. This is typically for displaying an empty grid initially.
    /// </summary>
    public void EnsureInitialGrid()
    {
        // If CAD data is already loaded or a grid already exists, no initial grid is needed.
        if (_cad is not null || RegularPads.Count > 0 || _initialGridBuildRequested)
        {
            return;
        }

        _initialGridBuildRequested = true;
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
            _initialGridBuildRequested = false;
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
            _fitAfterNextGridBuild = true;
        }
    }

    public bool TryGetCadOutputFwDiffIndex(int cadPadId, out int dxfIndex)
    {
        return _cadOutputFwDiffIndexByCadId.TryGetValue(cadPadId, out dxfIndex);
    }

    internal int WorkflowDataSnapshotRevision => _workflowDataSnapshotRevision;

    internal int WorkflowDataSnapshotBuildCount { get; private set; }

    private WorkflowDataSnapshot BuildWorkflowDataSnapshot()
    {
        if (_isWorkflowDataSnapshotCacheValid &&
            _workflowDataSnapshotCacheRevision == _workflowDataSnapshotRevision)
        {
            return _workflowDataSnapshotCache;
        }

        var activeMask = GetActiveRegularVisibilityMaskPadIds()?.ToFrozenSet();

        var cadToRegular = _latestPadMatchResult.CadToRegular
            .ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<PadMatchLink>)pair.Value.ToArray())
            .ToFrozenDictionary();

        _workflowDataSnapshotCache = new WorkflowDataSnapshot(
            _cadOutputFwDiffIndexByCadId.ToFrozenDictionary(),
            _latestCadOutputFwDiffAssignmentDecisionsByCadId.ToFrozenDictionary(),
            _cadIcIndexByCadId.ToFrozenDictionary(),
            _cadDisplayIndexByCadId.ToFrozenDictionary(),
            cadToRegular,
            _projectFile.CadOutputFwDiffIndexOverrides.ToFrozenDictionary(),
            activeMask);
        _workflowDataSnapshotCacheRevision = _workflowDataSnapshotRevision;
        _isWorkflowDataSnapshotCacheValid = true;
        WorkflowDataSnapshotBuildCount++;
        return _workflowDataSnapshotCache;
    }

    private void InvalidateWorkflowDataSnapshot()
    {
        _isWorkflowDataSnapshotCacheValid = false;
        _workflowDataSnapshotRevision++;
    }

    private void SetLatestPadMatchResult(PadMatchResult result)
    {
        _latestPadMatchResult = result;
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
}

