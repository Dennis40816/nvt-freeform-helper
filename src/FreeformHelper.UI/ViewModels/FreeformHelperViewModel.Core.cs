using System.Collections.Frozen;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.CadEditing;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.Services.CadEditing;
using FreeformHelper.UI.UseCases.ProjectLoad;
using FreeformHelper.UI.ViewModels.Grid;
using FreeformHelper.UI.ViewModels.Project;
using FreeformHelper.UI.ViewModels.Sizing;
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
public sealed partial class FreeformHelperViewModel : ObservableObject, IProjectEditingAvailability
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

    private readonly ViewCallbacks _viewCallbacks = new();

    // --- Delegates for UI-specific Dialogs ---
    // These delegates are set by the View (FreeformHelperView) to enable the ViewModel
    // to trigger platform-specific file dialogs or confirmation prompts.
    public Func<Task<string?>>? PickOpenDxfPathAsync { get => _viewCallbacks.PickOpenDxfPathAsync; set => _viewCallbacks.PickOpenDxfPathAsync = value; }
    public Func<Task<string?>>? PickOpenRegularVisibilityMaskPathAsync { get => RegularMask.PickOpenRegularVisibilityMaskPathAsync; set => RegularMask.PickOpenRegularVisibilityMaskPathAsync = value; }
    public Func<Task<string?>>? PickLoadProjectPathAsync { get => _viewCallbacks.PickLoadProjectPathAsync; set => _viewCallbacks.PickLoadProjectPathAsync = value; }
    public Func<Task<string?>>? PickSaveProjectPathAsync { get => _viewCallbacks.PickSaveProjectPathAsync; set => _viewCallbacks.PickSaveProjectPathAsync = value; }
    public Func<string, string, Task<string?>>? PickSaveNotchPathAsync { get => _viewCallbacks.PickSaveNotchPathAsync; set => _viewCallbacks.PickSaveNotchPathAsync = value; }
    public Func<string, string, Task<string?>>? PickSaveExportPathAsync { get => _viewCallbacks.PickSaveExportPathAsync; set => _viewCallbacks.PickSaveExportPathAsync = value; }
    public Func<Task<bool>>? ConfirmEmbedDxfAsync { get => _viewCallbacks.ConfirmEmbedDxfAsync; set => _viewCallbacks.ConfirmEmbedDxfAsync = value; }
    public Func<string, string, Task>? ShowWarningAsync { get => _viewCallbacks.ShowWarningAsync; set => _viewCallbacks.ShowWarningAsync = value; }
    public Func<bool>? EnsureNoPendingEdits { get => _viewCallbacks.EnsureNoPendingEdits; set => _viewCallbacks.EnsureNoPendingEdits = value; }
    public Func<NotchDetailViewModel, Task>? OpenNotchDetailAsync { get => _viewCallbacks.OpenNotchDetailAsync; set => _viewCallbacks.OpenNotchDetailAsync = value; }
    public Func<NotchExportSelectionViewModel, Task<NotchTable?>>? OpenNotchExportSelectionAsync { get => _viewCallbacks.OpenNotchExportSelectionAsync; set => _viewCallbacks.OpenNotchExportSelectionAsync = value; }
    public Func<SimulationSafetyAuditResult?>? GetCurrentSimulationSafetyAudit { get => _viewCallbacks.GetCurrentSimulationSafetyAudit; set => _viewCallbacks.GetCurrentSimulationSafetyAudit = value; }
    public Func<DxfEditChangeListViewModel, Task>? OpenDxfEditChangeListAsync { get => DxfEdit.OpenDxfEditChangeListAsync; set => DxfEdit.OpenDxfEditChangeListAsync = value; }
    public Func<IndexMappingReportViewModel, Task>? OpenIndexMappingReportAsync { get => _viewCallbacks.OpenIndexMappingReportAsync; set => _viewCallbacks.OpenIndexMappingReportAsync = value; }
    public Func<DxfOverlapReportViewModel, Task>? OpenDxfOverlapReportAsync { get => _viewCallbacks.OpenDxfOverlapReportAsync; set => _viewCallbacks.OpenDxfOverlapReportAsync = value; }
    public Func<LayerImageExportContext, Task<LayerImageExportRequest?>>? OpenDxfLayerImageExportAsync { get => _viewCallbacks.OpenDxfLayerImageExportAsync; set => _viewCallbacks.OpenDxfLayerImageExportAsync = value; }

    // --- Application Services ---
    // Instances of various services responsible for business logic and data manipulation.
    private readonly JsonProjectStore _projectStore = new();
    private readonly DxfImportService _dxfImportService = new();
    private readonly DxfLayerCatalogReader _dxfLayerCatalogReader = new();
    private readonly NotchExportService _notchExportService = new();

    // --- Internal State Management ---
    private readonly Dictionary<int, double> _cadPadCustomValues = new(); // Stores custom user-defined values for CAD pads.
    private readonly Dictionary<string, WorkspaceHeaderItem> _workspaceHeaderItemMap = new(); // Maps keys to header items.
    // Independent nesting-safe suppression counters; UI-thread-only access.
    private readonly SettingsSuppressionScopes _settingsSuppression = new();
    private ProjectFile _projectFile { get => _projectSession.Document; set => _projectSession.Document = value; }

    /// <summary>
    /// Gets the <see cref="WorkspaceInteractionState"/> instance, which manages global UI interaction states
    /// such as current selection and active info popovers.
    /// </summary>
    public WorkspaceInteractionState InteractionState { get; } = new();

    internal CadDocumentSession CadDocument { get; } = new();
    public DxfEditViewModel DxfEdit { get; }

    private readonly CadEditWorkflow _cadEditWorkflow;
    private readonly CadEditReplay _cadEditReplay;
    private CadPadSet? _cad { get => CadDocument.Cad; set => CadDocument.Cad = value; }
    private RegularGrid? _grid; // The generated regular grid data.
    private readonly WorkflowDataState _workflowData = new(); // Holds the latest pad match result and the cached workflow data snapshot built from it.

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

    /// <summary>Gets the mask child, whose identity survives project replacement.</summary>
    public RegularMaskViewModel RegularMask { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FreeformHelperViewModel"/> class.
    /// Sets up default values, initializes UI options, and configures commands.
    /// </summary>
    public FreeformHelperViewModel(AppGeneralSettingsStore? appGeneralSettingsStore = null)
        : this(appGeneralSettingsStore, null)
    {
    }

    private (CadEditWorkflow Workflow, CadEditReplay Replay, IDxfEditActions Actions, DxfEditInteraction Interaction) CreateCadEditEngine()
    {
        var publication = new CadEditPublication(
            CaptureCadEditNotifications, SyncRelayeredCadState, SyncRotatedCadState,
            SyncHiddenCadIdsToProject, SyncCombinedCadState, SyncDuplicateCadState);
        var layers = new CadEditLayers(
            CaptureLayerSelectionSnapshot, PreserveDxfDependentLayerSelections, UpdateLayerToggles,
            RestoreLayerSelectionAfterCadLayerEdit, CadLayersMatchDocument, name => DxfEditTargetLayerName = name);
        var effects = new CadEditEffects(
            InteractionState.ClosePadInfo, () => _selectionCoordinator.ClearSelection(CanvasHost),
            FilterCadPadsByLayer, RebuildAfterCadEdit, SetStatus, MarkUnsaved, PushUndo);
        var interaction = CreateDxfEditInteraction();
        var workflow = new CadEditWorkflow(CadDocument, publication, layers, effects, interaction);
        var actions = new DxfEditWorkflow(CadDocument, workflow, publication, layers, effects, interaction);
        return (workflow, new CadEditReplay(CadDocument, workflow, publication, layers), actions, interaction);
    }

    private DxfEditInteraction CreateDxfEditInteraction() => new(
        () => _selectedCadIds, () => DxfEdit.PanelInput, name => DxfEdit.DxfEditNewLayerName = name,
        FocusDxfEditCadPad, SetStatus,
        () => OnPropertyChanged(nameof(CanOffsetSelectedCadOutputFwDiffIndices)),
        () => OnPropertyChanged(nameof(Step4DiffShiftSelectionSummary)));

    private DxfEditViewModel CreateDxfEditChild(IDxfEditActions actions, DxfEditInteraction interaction)
    {
        var child = new DxfEditViewModel(
            CadDocument, actions, interaction,
            new CadEditPersistence(ids => _projectFile.HiddenCadPadIds = ids, ids => _projectFile.VisibleDuplicateCadPadIds = ids),
            () => _projectEditingSession.IsProjectEditingEnabled,
            execute => CreateEditingCommand(execute), execute => CreateEditingAsyncCommand(execute));
        child.PropertyChanging += (_, args) => OnPropertyChanging(args);
        child.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(IsProjectEditingEnabled)) OnPropertyChanged(args);
        };
        return child;
    }

    private void FocusDxfEditCadPad(int cadPadId)
    {
        TryLocateSelection(
            new[] { cadPadId }, Array.Empty<int>(), focusSelection: true,
            notVisibleStatus: $"CAD {cadPadId} is not visible.",
            successStatusBuilder: (cadCount, _) => $"DXF edit focused: CAD {cadPadId} (CAD output={cadCount}).");
    }

    internal DxfCombinedGroupReplayResult? ApplyCapturedDxfEditProjectState(
        PersistedDxfEditProjectState state, string? cadFallbackWarning = null) => _cadEditReplay.Apply(state, cadFallbackWarning);

    private readonly ProjectEditingSession _projectEditingSession;

    internal FreeformHelperViewModel(AppGeneralSettingsStore? appGeneralSettingsStore, UiEventRunner? uiEvents)
    {
        var editEngine = CreateCadEditEngine();
        (_cadEditWorkflow, _cadEditReplay) = (editEngine.Workflow, editEngine.Replay);
        _settingsState = new SettingsStateViewModel(this);
        _fwAssignmentSettings = new FwAssignmentSettingsViewModel(this);
        _canvasStrokeFillState = new CanvasStrokeFillState(
            () => _isLoadingSettings,
            () => _undoSuppression.IsActive,
            () => _undoSuppression.Enter(),
            PushUndo,
            () => CanvasHost?.Invalidate(),
            OnPropertyChanging,
            OnPropertyChanged);
        _workflowPanelState = new WorkflowPanelState(SelectWorkspaceWorkStep, OnPropertyChanging, OnPropertyChanged);
        _projectEditingSession = new ProjectEditingSession(PublishProjectEditingAvailabilityChange);
        _projectSession = new ProjectSessionState(
            new ProjectFile(), () => _cad is not null, () => _projectEditingSession.IsProjectEditingEnabled,
            load => _projectEditingSession.RunLoadAsync(load), OnPropertyChanging, OnPropertyChanged);
        _manualSizingOwner = new Lazy<ManualSizingOwner>(CreateManualSizingOwner, LazyThreadSafetyMode.None);
        _gridMatchingOwner = new Lazy<GridMatchingOwner>(CreateGridMatchingOwner, LazyThreadSafetyMode.None);
        DxfEdit = CreateDxfEditChild(editEngine.Actions, editEngine.Interaction);
        _dxfLayerImagePreferenceState = new DxfLayerImagePreferenceState(MarkUnsaved, SchedulePersistAppGeneralSettings);
        _projectUiSnapshotMapper = new ProjectUiSnapshotMapper(CreateProjectSnapshotReaders());
        StartupPerfTracker.Mark("workspace.vm-ctor-start");
        _statusReporter = new UiOperationStatusReporter(message => StatusText = message);
        RegularMask = CreateRegularMask();
        UiEvents = uiEvents ?? new UiEventRunner(new UiEventFailureReporter(_statusReporter).Report, EmergencyLogSink.Report);
        _appGeneralSettingsStore = appGeneralSettingsStore ?? new AppGeneralSettingsStore();
        _appGeneralPersistenceCoordinator = new AppGeneralPersistenceCoordinator(this, _appGeneralPersistence);
        InitializeAppGeneralSettingsPersistence();
        ScanOrderOptions = BuildScanOrderOptions();
        GridAlignmentOptions = BuildGridAlignmentOptions();
        RegularSourceModeOptions = BuildRegularSourceModeOptions();
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
        // Register project commands next, through the existing editing session registry.
        _projectCommands = new ProjectCommandOwner(
            CreateEditingAsyncCommand, CreateEditingAsyncCommand, SaveProjectAsync, LoadProjectAsync);
        _selectionCommands = CreateSelectionCommands(); // Register selection commands next.
        _dxfWorkspaceCommands = CreateDxfWorkspaceCommands();
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

    private RegularMaskViewModel CreateRegularMask()
    {
        var stateChange = _settingsSuppression.RegularMaskStateChange;
        var effects = new RegularMaskEffects(
            GetGrid: () => _grid,
            HasCad: () => _cad is not null,
            GetVisiblePads: () => CadPads.Count > 0 ? CadPads.ToList() : BuildFilteredCadPadSet()?.Pads.ToList(),
            UpdateMapping: UpdateCadOutputFwDiffIndexing,
            InvalidateCanvas: () => CanvasHost?.Invalidate(),
            InvalidateDownstream: () => InvalidateDownstreamFromStep4(showStatus: false),
            PublishSimulationChange: NotifySimulationWorkspaceSourceChanged,
            InvalidateSnapshot: InvalidateWorkflowDataSnapshot,
            RefreshDuplicateGroups: () => RefreshStep4DuplicateDiffGroups(),
            ClearProjectEmbeddedMask: () =>
            {
                _projectFile.EmbeddedRegularVisibilityMask = null;
                _projectFile.EmbeddedRegularVisibilityMaskName = null;
            },
            SetStatus: message => SetStatus(message),
            EnterStateChange: stateChange.Enter,
            IsStateChangeActive: stateChange.IsActive);
        return new RegularMaskViewModel(presentation => new RegularMaskWorkflow(presentation, effects, _logger));
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

        var activeMask = RegularMask.Workflow.ActiveRegularPadIds?.ToFrozenSet();

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
