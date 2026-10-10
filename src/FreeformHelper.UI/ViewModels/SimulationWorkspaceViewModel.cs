using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.Services;
using Nvt.Core.Threading;

namespace FreeformHelper.UI.ViewModels;

public enum SimulationInputSourceMode
{
    Manual = 0,
    Csv = 1,
    Copper = 2,
}

public readonly record struct SimulationInputSourceOption(
    SimulationInputSourceMode Value,
    string Display);

public enum SimulationContactModelMode
{
    GroundedCopper = 0,
    FingerPress = 1,
}

public readonly record struct SimulationContactModelOption(
    SimulationContactModelMode Value,
    string Display,
    double PeakValue,
    string Description);

internal readonly record struct SimulationRegularQuerySnapshot(
    int RegularPadId,
    int Row,
    int Col,
    int IcIndex,
    int FwDiffIndex,
    int CadOutputFwDiffIndex,
    double BeforeValue,
    double AfterValue,
    double DeltaValue,
    bool IsEmsSafetyRisk,
    bool IsActiveSurface,
    bool IsWithinSelectedArea,
    string InputSourceText,
    IReadOnlyList<NotchApplySimulationImpactItemViewModel> ImpactItems)
{
    // Backward-compatible alias for existing runtime query clients.
    public int DiffIndex => FwDiffIndex;
    public int CadLayerDiffIndex => CadOutputFwDiffIndex;
}

internal readonly record struct SimulationCadOutputFwDiffAssignmentDecisionSummarySnapshot(
    int DecisionCount,
    string Mode,
    IReadOnlyDictionary<string, int> ReasonCounts,
    IReadOnlyDictionary<string, int> DecisionSourceCounts)
{
    public static readonly SimulationCadOutputFwDiffAssignmentDecisionSummarySnapshot Empty = new(
        DecisionCount: 0,
        Mode: "unknown",
        ReasonCounts: new Dictionary<string, int>(StringComparer.Ordinal),
        DecisionSourceCounts: new Dictionary<string, int>(StringComparer.Ordinal));
}

public sealed partial class SimulationWorkspaceViewModel : ObservableObject
{
    internal UiEventRunner? UiEvents { get; init; }

    public sealed record SimulationTextExportRequest(
        string Title,
        string SuggestedFileName,
        string DefaultExtension,
        string FileTypeName,
        IReadOnlyList<string> Patterns,
        string Content);

    private const double SignificantDeltaEpsilon = 1e-9;
    private const double DefaultThresholdValue = 5d;
    private const double DefaultGlobalValue = 0d;
    private const double DefaultCopperPeakValue = 400d;
    private const double DefaultFingerPeakValue = 360d;
    private const double DefaultCopperBaselineValue = 0d;
    private const int DefaultPlaybackFramesPerSecond = 6;
    private static readonly IReadOnlyList<SimulationInputSourceOption> SharedInputSourceOptions =
    [
        new SimulationInputSourceOption(SimulationInputSourceMode.Manual, "Manual"),
        new SimulationInputSourceOption(SimulationInputSourceMode.Csv, "CSV"),
        new SimulationInputSourceOption(SimulationInputSourceMode.Copper, "Copper"),
    ];
    private static readonly IReadOnlyList<SimulationContactModelOption> SharedCopperContactModelOptions =
    [
        new(
            SimulationContactModelMode.GroundedCopper,
            "Grounded copper (400)",
            DefaultCopperPeakValue,
            "Grounded copper: full CAD pad contact is modeled as 400 diff."),
        new(
            SimulationContactModelMode.FingerPress,
            "Finger press (~360)",
            DefaultFingerPeakValue,
            "Finger press: same CAD overlap model, lower full-contact signal than grounded copper."),
    ];

    private static readonly NotchApplySimulationCanvasViewOption[] SharedCanvasViewOptions =
    {
        new(NotchApplySimulationCanvasViewMode.Before, "Before"),
        new(NotchApplySimulationCanvasViewMode.After, "After"),
        new(NotchApplySimulationCanvasViewMode.Delta, "Delta"),
        new(NotchApplySimulationCanvasViewMode.ChangedOnly, "Changed only"),
    };

    private static readonly NotchApplySimulationCanvasColorOption[] SharedCanvasColorOptions =
    {
        new(NotchApplySimulationCanvasColorMode.Auto, "AUTO"),
        new(NotchApplySimulationCanvasColorMode.Threshold, "TH"),
    };

    private readonly SimulationWorkspaceUseCase _useCase;
    private readonly SimulationWorkspaceSession _session;
    private DispatcherTimer? _playbackTimer;
    private readonly Dictionary<int, double> _manualOverridesByRegularPadId = new();
    private readonly Dictionary<int, RegularPad> _regularPadById;
    private readonly Dictionary<int, int> _cadOutputFwDiffByRegularPadId;
    private readonly Dictionary<(int IcIndex, int DiffIndex), int[]> _regularPadIdsByIcDiff;
    private readonly Dictionary<SimulationSnapshotCacheKey, SimulationScenarioSnapshot> _csvSnapshotCache = new();
    private readonly ObservableCollection<NotchApplySimulationSourceFileItemViewModel> _csvFiles = new();
    private readonly ObservableCollection<NotchApplySimulationFrameOption> _frameOptions = new();
    private readonly ObservableCollection<NotchApplySimulationImpactItemViewModel> _selectedImpactItems = new();
    private readonly ObservableCollection<SimulationSafetyRiskDiffViewModel> _simulationHighRiskDiffs = new();
    private readonly ObservableCollection<CopperPillarPathReplayArtifactRow> _copperPathReplayRows = new();
    private IReadOnlyDictionary<int, NotchApplySimulationDiffCell> _cellsByRegularPadId = new Dictionary<int, NotchApplySimulationDiffCell>();
    private Dictionary<int, NotchApplySimulationDiffCell> _snapshotCellsByRegularPadId = new();
    private Dictionary<int, IReadOnlyList<NotchApplySimulationImpactItemViewModel>> _impactItemsByRegularPadId = new();
    private double _legendMinimumValue;
    private double _legendMaximumValue;
    private double _legendMaximumAbsDelta;
    private readonly IReadOnlyList<CadPad> _cadPads;
    private readonly bool _showCad;
    private readonly bool _showRegular;
    private readonly bool _highlightFreeform;
    private readonly bool _highlightUnmatched;
    private double _legendAutoNegativeClampAbs;
    private double _legendAutoPositiveClamp;
    private IReadOnlyList<string> _csvSourcePaths = Array.Empty<string>();
    private NotchApplySimulationImportedDataset _csvDataset = NotchApplySimulationImportedDataset.Empty;
    private CopperPillarPathReplayResult _copperPathReplayResult = CopperPillarPathReplayResult.Empty;
    private CopperPillarPathReplayArtifactSnapshot _copperPathReplayArtifactSnapshot =
        CopperPillarPathReplayArtifactSnapshot.Empty;
    private SimulationScenarioSnapshot? _snapshot;
    private IReadOnlyList<NotchApplySimulationAaDisplayCell> _simulationOverlayItems = Array.Empty<NotchApplySimulationAaDisplayCell>();
    private bool _isSyncingFrameSelection;
    private bool _isUpdatingCopperCenter;

    public SimulationWorkspaceViewModel(
        SimulationWorkspaceUseCase useCase,
        SimulationWorkspaceSession session)
    {
        _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _cadPads = _session.CadPads;
        _regularPadById = _session.FwDiffGrid.Pads.ToDictionary(pad => pad.RegularPadId);
        _cadOutputFwDiffByRegularPadId = _session.CadOutputFwDiffGrid.Pads.ToDictionary(
            static pad => pad.RegularPadId,
            static pad => pad.DiffIndex);
        _regularPadIdsByIcDiff = _session.FwDiffGrid.Pads
            .GroupBy(static pad => (pad.IcIndex, pad.DiffIndex))
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static pad => pad.RegularPadId).ToArray());
        _showCad = false;
        _showRegular = _session.DefaultShowRegular;
        _highlightFreeform = _session.DefaultHighlightFreeform;
        _highlightUnmatched = _session.DefaultHighlightUnmatched;

        SourceOptions = SharedInputSourceOptions;
        CopperContactModelOptions = SharedCopperContactModelOptions;
        CanvasViewOptions = SharedCanvasViewOptions;
        CanvasColorOptions = SharedCanvasColorOptions;
        VersionOptions = BuildVersionOptions(_session.Table);
        AreaOptions = BuildAreaOptions(_session.FwDiffGrid.Pads);
        CsvFiles = new ReadOnlyObservableCollection<NotchApplySimulationSourceFileItemViewModel>(_csvFiles);
        FrameOptions = new ReadOnlyObservableCollection<NotchApplySimulationFrameOption>(_frameOptions);
        SelectedImpactItems = new ReadOnlyObservableCollection<NotchApplySimulationImpactItemViewModel>(_selectedImpactItems);
        SimulationHighRiskDiffs = new ReadOnlyObservableCollection<SimulationSafetyRiskDiffViewModel>(_simulationHighRiskDiffs);
        CopperPathReplayRows = new ReadOnlyObservableCollection<CopperPillarPathReplayArtifactRow>(_copperPathReplayRows);

        ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync, () => CanImportCsv);
        ClearCsvCommand = new RelayCommand(ClearCsv, () => HasCsvFiles);
        RandomizeVisibleValuesCommand = new RelayCommand(RandomizeVisibleValues, () => IsManualSource && CanRandomizeVisibleValues);
        ApplyManualPresetCommand = new RelayCommand<string?>(ApplyManualPreset, _ => IsManualSource);
        TogglePlaybackCommand = new RelayCommand(TogglePlayback, () => CanPlayFrames);
        PreviousFrameCommand = new RelayCommand(MoveToPreviousFrame, () => CanStepFrames);
        NextFrameCommand = new RelayCommand(MoveToNextFrame, () => CanStepFrames);
        ApplySelectedOverrideCommand = new RelayCommand(ApplySelectedOverride, () => HasSelectedRegular);
        ClearSelectedOverrideCommand = new RelayCommand(ClearSelectedOverride, () => HasSelectedOverride);
        ClearAllManualOverridesCommand = new RelayCommand(ClearAllManualOverrides, () => HasManualOverrides);
        FitCanvasCommand = new RelayCommand(() => FitCanvasRequested?.Invoke(this, EventArgs.Empty));
        SelectSimulationRiskDiffCommand = new RelayCommand<SimulationSafetyRiskDiffViewModel?>(SelectSimulationRiskDiff);
        ReplayCopperPathCommand = new RelayCommand(ReplayConfiguredCopperPath, () => IsCopperSource);
        UseCurrentCopperAsPathStartCommand = new RelayCommand(
            () => CaptureCopperPathEndpoint(useStart: true),
            () => IsCopperSource);
        UseCurrentCopperAsPathEndCommand = new RelayCommand(
            () => CaptureCopperPathEndpoint(useStart: false),
            () => IsCopperSource);
        SelectCopperPathReplayRowCommand =
            new RelayCommand<CopperPillarPathReplayArtifactRow?>(SelectCopperPathReplayRow);
        CopyCopperPathReplayCommand = new AsyncRelayCommand(CopyCopperPathReplayAsync);
        ExportCopperPathReplayCsvCommand = new AsyncRelayCommand(ExportCopperPathReplayCsvAsync);
        ExportCopperPathReplayJsonCommand = new AsyncRelayCommand(ExportCopperPathReplayJsonAsync);

        _ = EnsurePlaybackTimer();

        _selectedInputSourceOption = SourceOptions[0];
        _selectedCopperContactModelOption = CopperContactModelOptions[0];
        _selectedVersionOption = VersionOptions[0];
        _selectedCanvasViewOption = SharedCanvasViewOptions[0];
        _selectedCanvasColorOption = SharedCanvasColorOptions[0];
        _selectedAreaOption = AreaOptions[0];
        _colorThresholdValue = DefaultThresholdValue;
        _globalValue = DefaultGlobalValue;
        _copperPeakValue = DefaultCopperPeakValue;
        _copperBaselineValue = DefaultCopperBaselineValue;
        _copperDiameter = ResolveDefaultCopperDiameter(_session.FwDiffGrid);
        _copperCenterX = (_session.FwDiffGrid.Bounds.MinX + _session.FwDiffGrid.Bounds.MaxX) * 0.5d;
        _copperCenterY = (_session.FwDiffGrid.Bounds.MinY + _session.FwDiffGrid.Bounds.MaxY) * 0.5d;
        _copperPathStartX = (decimal)_session.FwDiffGrid.Bounds.MinX;
        _copperPathStartY = (decimal)_copperCenterY;
        _copperPathEndX = (decimal)_session.FwDiffGrid.Bounds.MaxX;
        _copperPathEndY = (decimal)_copperCenterY;
        _copperPathStepCount = 9m;
        _selectedManualValue = DefaultGlobalValue;
        _playbackFramesPerSecond = DefaultPlaybackFramesPerSecond;
        _statusText = "Simulation ready. Use Manual to set baseline values, or import CSV for frame playback.";

        RefreshFrameOptions();
        RefreshSnapshot();
    }

    private readonly record struct SimulationSnapshotCacheKey(
        NotchAlgorithmVersion Version,
        int FrameIndex);

    public event EventHandler? FitCanvasRequested;
    public Func<Task<IReadOnlyList<string>>>? PickOpenDiffCsvPathsAsync { get; set; }
    public Func<string, Task<bool>>? RequestSetClipboardTextAsync { get; set; }
    public Func<SimulationTextExportRequest, Task<bool>>? RequestSaveTextFileAsync { get; set; }

    public IReadOnlyList<SimulationInputSourceOption> SourceOptions { get; }
    public IReadOnlyList<SimulationContactModelOption> CopperContactModelOptions { get; }
    public IReadOnlyList<NotchApplySimulationCanvasViewOption> CanvasViewOptions { get; }
    public IReadOnlyList<NotchApplySimulationCanvasColorOption> CanvasColorOptions { get; }
    public IReadOnlyList<NotchApplySimulationVersionOption> VersionOptions { get; }
    public IReadOnlyList<NotchApplySimulationAreaOption> AreaOptions { get; }
    public ReadOnlyObservableCollection<NotchApplySimulationSourceFileItemViewModel> CsvFiles { get; }
    public ReadOnlyObservableCollection<NotchApplySimulationFrameOption> FrameOptions { get; }
    public ReadOnlyObservableCollection<NotchApplySimulationImpactItemViewModel> SelectedImpactItems { get; }
    public ReadOnlyObservableCollection<SimulationSafetyRiskDiffViewModel> SimulationHighRiskDiffs { get; }
    public ReadOnlyObservableCollection<CopperPillarPathReplayArtifactRow> CopperPathReplayRows { get; }

    public IAsyncRelayCommand ImportCsvCommand { get; }
    public IRelayCommand ClearCsvCommand { get; }
    public IRelayCommand RandomizeVisibleValuesCommand { get; }
    public IRelayCommand<string?> ApplyManualPresetCommand { get; }
    public IRelayCommand TogglePlaybackCommand { get; }
    public IRelayCommand PreviousFrameCommand { get; }
    public IRelayCommand NextFrameCommand { get; }
    public IRelayCommand ApplySelectedOverrideCommand { get; }
    public IRelayCommand ClearSelectedOverrideCommand { get; }
    public IRelayCommand ClearAllManualOverridesCommand { get; }
    public IRelayCommand FitCanvasCommand { get; }
    public IRelayCommand<SimulationSafetyRiskDiffViewModel?> SelectSimulationRiskDiffCommand { get; }
    public IRelayCommand ReplayCopperPathCommand { get; }
    public IRelayCommand UseCurrentCopperAsPathStartCommand { get; }
    public IRelayCommand UseCurrentCopperAsPathEndCommand { get; }
    public IRelayCommand<CopperPillarPathReplayArtifactRow?> SelectCopperPathReplayRowCommand { get; }
    public IAsyncRelayCommand CopyCopperPathReplayCommand { get; }
    public IAsyncRelayCommand ExportCopperPathReplayCsvCommand { get; }
    public IAsyncRelayCommand ExportCopperPathReplayJsonCommand { get; }

    [ObservableProperty] private SimulationInputSourceOption _selectedInputSourceOption;
    [ObservableProperty] private SimulationContactModelOption _selectedCopperContactModelOption;
    [ObservableProperty] private NotchApplySimulationVersionOption _selectedVersionOption;
    [ObservableProperty] private NotchApplySimulationCanvasViewOption _selectedCanvasViewOption;
    [ObservableProperty] private NotchApplySimulationCanvasColorOption _selectedCanvasColorOption;
    [ObservableProperty] private NotchApplySimulationAreaOption _selectedAreaOption;
    [ObservableProperty] private double _colorThresholdValue;
    [ObservableProperty] private double _globalValue;
    [ObservableProperty] private double _copperCenterX;
    [ObservableProperty] private double _copperCenterY;
    [ObservableProperty] private double _copperDiameter;
    [ObservableProperty] private double _copperPeakValue;
    [ObservableProperty] private double _copperBaselineValue;
    [ObservableProperty] private decimal _copperPathStartX;
    [ObservableProperty] private decimal _copperPathStartY;
    [ObservableProperty] private decimal _copperPathEndX;
    [ObservableProperty] private decimal _copperPathEndY;
    [ObservableProperty] private decimal _copperPathStepCount;
    [ObservableProperty] private CopperPillarPathReplayArtifactRow? _selectedCopperPathReplayRow;
    [ObservableProperty] private int _selectedFrameSliderIndex;
    [ObservableProperty] private bool _isPlaybackRunning;
    [ObservableProperty] private bool _isPlaybackLoopEnabled = true;
    [ObservableProperty] private bool _isSimulationSafetyExpanded;
    [ObservableProperty] private int _playbackFramesPerSecond;
    [ObservableProperty] private int _selectedRegularPadId = -1;
    [ObservableProperty] private double _selectedManualValue;
    [ObservableProperty] private string _statusText;

    public IReadOnlyList<CadPad> CadPads => _cadPads;
    public IReadOnlyList<RegularPad> RegularPads => _session.FwDiffGrid.Pads;
    public IReadOnlyList<NotchApplySimulationAaDisplayCell> SimulationOverlayItems => _simulationOverlayItems;
    public NotchApplySimulationCanvasViewMode SelectedCanvasViewMode => SelectedCanvasViewOption.Value;
    public NotchApplySimulationCanvasColorMode SelectedCanvasColorMode => SelectedCanvasColorOption.Value;
    public bool ShowCad => _showCad;
    public bool ShowRegular => _showRegular;
    public bool HighlightFreeform => _highlightFreeform;
    public bool HighlightUnmatched => _highlightUnmatched;
    public bool CanImportCsv => PickOpenDiffCsvPathsAsync is not null;
    public bool HasCsvFiles => CsvFiles.Count > 0;
    public bool HasCsvData => _csvDataset.CompatibleFrameCount > 0;
    public bool IsCsvSource => SelectedInputSourceOption.Value == SimulationInputSourceMode.Csv;
    public bool IsCopperSource => SelectedInputSourceOption.Value == SimulationInputSourceMode.Copper;
    public bool IsManualSource => SelectedInputSourceOption.Value == SimulationInputSourceMode.Manual;
    public bool ShowSimulationDiagnostics => _snapshot is not null;
    public bool HasUserDerivedState => HasCsvFiles || HasManualOverrides || IsCopperSource;
    public bool CanAutoRefreshFromSourceChange => !HasUserDerivedState;
    public bool CanPlayFrames => IsCsvSource && FrameOptions.Count > 1;
    public bool CanStepFrames => IsCsvSource && FrameOptions.Count > 0;
    public bool CanRandomizeVisibleValues => _session.FwDiffGrid.Pads.Any(IsPadInSelectedAreaAndActiveSurface);
    public bool IsThresholdMode => SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Threshold;
    public bool IsSignedCanvasView => SelectedCanvasViewMode is NotchApplySimulationCanvasViewMode.Delta or NotchApplySimulationCanvasViewMode.ChangedOnly;
    public bool UsesSignedColorScale => IsSignedCanvasView || _legendMinimumValue < -SignificantDeltaEpsilon;
    public bool HasSelectedRegular => SelectedRegularPadId >= 0 && _regularPadById.ContainsKey(SelectedRegularPadId);
    public bool HasManualOverrides => _manualOverridesByRegularPadId.Count > 0;
    public bool HasSelectedOverride => HasSelectedRegular && _manualOverridesByRegularPadId.ContainsKey(SelectedRegularPadId);
    public int ActiveSurfaceRegularCount => _session.ActiveRegularPadIds.Count;
    public string SessionSummaryText => $"Grid {_session.RowCount} x {_session.ColumnCount} · REG {_session.PadCount} · Rows {_session.Table.Rows.Count}";
    public int SourceRevision => _session.SourceRevision;
    public string AreaSummaryText => SelectedAreaOption.Display;
    public string SourceModeSummaryText => SelectedInputSourceOption.Value switch
    {
        SimulationInputSourceMode.Csv => "Source: CSV",
        SimulationInputSourceMode.Copper => "Source: Copper",
        _ => "Source: Manual",
    };
    public string InputSummaryText => IsCsvSource && HasCsvData
        ? $"CSV {CsvFiles.Count} file(s) · Compatible frames {FrameOptions.Count}"
        : IsCopperSource
            ? CopperSummaryText
            : $"Manual baseline {GlobalValue.ToString("0.###", CultureInfo.InvariantCulture)} · Overrides {_manualOverridesByRegularPadId.Count}";
    public string InputPrimarySummaryText => SelectedInputSourceOption.Value switch
    {
        SimulationInputSourceMode.Csv => HasCsvData ? $"CSV {CsvFiles.Count} file(s)" : "CSV not loaded",
        SimulationInputSourceMode.Copper => $"Signal peak {FormatWholeNumberDisplay(CopperPeakValue)}",
        _ => $"Manual baseline {GlobalValue.ToString("0.###", CultureInfo.InvariantCulture)}",
    };
    public string InputSecondarySummaryText => SelectedInputSourceOption.Value switch
    {
        SimulationInputSourceMode.Csv => HasCsvData ? $"Compatible frames {FrameOptions.Count}" : "Import CSV to project frames onto the current regular grid.",
        SimulationInputSourceMode.Copper => CopperPositionText,
        _ => $"Overrides {_manualOverridesByRegularPadId.Count}",
    };
    public string InputContractTitleText => SelectedInputSourceOption.Value switch
    {
        SimulationInputSourceMode.Csv => "Contract: FW/tool rectangular diff frame",
        SimulationInputSourceMode.Copper => "Contract: CAD physical overlap",
        _ => "Contract: regular/FW memory frame",
    };
    public string InputContractDetailText => SelectedInputSourceOption.Value switch
    {
        SimulationInputSourceMode.Csv =>
            "CSV is treated as a rectangular FW/tool diff frame, projected onto the regular grid, then passed through the current notch table.",
        SimulationInputSourceMode.Copper =>
            "Copper computes CAD pad overlap first, maps signal to CAD Output FW Diff, then shows regular/FW Before and notch After.",
        _ =>
            "Manual writes Before directly into regular/FW memory. Use it for uniform-field and single-diff checks; it does not model CAD pad geometry.",
    };
    public string CsvProjectionContractText => IsCsvSource
        ? "CSV row origin: imported top row maps to AA visual top row. Use CSV only for FW/tool rectangular diff frames."
        : string.Empty;
    public bool HasCsvInspectorData => IsCsvSource && HasCsvFiles;
    public string CsvInspectorSummaryText => HasCsvFiles
        ? $"Files {CsvFiles.Count} · Compatible frames {_csvDataset.CompatibleFrameCount}/{_csvDataset.Frames.Count} · Expected grid {_session.FwDiffGrid.Cols} x {_session.FwDiffGrid.Rows}"
        : "No CSV loaded.";
    public string CsvInspectorShapeSummaryText => BuildCsvShapeSummaryText();
    public string CsvInspectorRowOriginText =>
        $"Row origin: CSV row 0 -> AA visual top (regular row {_session.FwDiffGrid.Rows - 1}); CSV last row -> regular row 0.";
    public string CsvInspectorBeforePreviewText => BuildCsvBeforePreviewText();
    public string FrameSummaryText => IsCsvSource && FrameOptions.Count > 0
        ? $"Frame {SelectedFrameSliderIndex + 1}/{FrameOptions.Count}"
        : IsCopperSource
            ? "Copper frame"
            : "Manual frame";
    public string VersionSummaryText => $"Version: {SelectedVersionOption.Display}";
    public string ViewModeSummaryText => $"View: {SelectedCanvasViewOption.Display}";
    public string ColorModeSummaryText => SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Threshold
        ? $"Color: TH scale ({ColorThresholdValue.ToString("0.###", CultureInfo.InvariantCulture)})"
        : "Color: AUTO scale";
    public string CanvasStatusSummaryText => HasSimulationData
        ? StatusText
        : $"Mode ready · {AreaSummaryText}";
    public string PlaybackButtonText => IsPlaybackRunning ? "Pause" : "Play";
    public string PlaybackButtonGlyph => IsPlaybackRunning ? IconGlyphs.Pause : IconGlyphs.PlayArrow;
    public int SelectedFrameSliderMaximum => Math.Max(FrameOptions.Count - 1, 0);
    public bool HasSimulationData => SimulationOverlayItems.Count > 0;
    public string PlaybackLoopText => IsPlaybackLoopEnabled ? "Loop on" : "Loop off";
    public string SelectedFrameDisplayText => IsCsvSource && FrameOptions.Count > 0 && SelectedFrameSliderIndex >= 0 && SelectedFrameSliderIndex < FrameOptions.Count
        ? FrameOptions[SelectedFrameSliderIndex].Display
        : IsCopperSource
            ? CopperSummaryText
            : "Manual baseline";
    public string ColorLegendTitleText => IsThresholdMode ? "TH color scale" : "AUTO color scale";
    public string ColorLegendMeaningText => UsesSignedColorScale
        ? SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Auto
            ? "AUTO percentile-clamped scale: ordinary values stay green, strongly negative values trend blue, and stronger positive values move through yellow/orange to red based on the visible distribution."
            : "TH signed scale: negative values trend blue, values near 0 trend green, and positive values move through yellow/orange to red, using TH as the scale reference."
        : SelectedCanvasColorOption.Value == NotchApplySimulationCanvasColorMode.Auto
            ? "AUTO percentile-clamped scale: regular values stay green at baseline, then move through yellow/orange to red as they rise within the visible distribution."
            : "TH mode: lower diff trends blue, near-threshold values trend green/yellow, and higher diff trends orange/red, using TH as the color scale reference.";
    public string ColorLegendStartText => UsesSignedColorScale
        ? FormatLegendValue(-GetLegendSignedRange())
        : IsThresholdMode
            ? "0"
            : FormatLegendValue(_legendMinimumValue);
    public string ColorLegendCenterText => UsesSignedColorScale
        ? "0"
        : string.Empty;
    public string ColorLegendEndText => UsesSignedColorScale
        ? FormatSignedLegendValue(GetLegendSignedRange())
        : IsThresholdMode
            ? $"TH {ColorThresholdValue.ToString("0.###", CultureInfo.InvariantCulture)}"
            : FormatLegendValue(_legendMaximumValue);
    public bool HasDuplicateDiffResolutions => _snapshot?.Result.DiffIdentityContract.HasDuplicateResolutions ?? false;
    public int DuplicateDiffResolutionCount => _snapshot?.Result.DiffIdentityContract.DuplicateResolutionCount ?? 0;
    public string DuplicateDiffResolutionStrategyText => FormatDuplicateDiffResolutionStrategy(
        _snapshot?.Result.DiffIdentityContract.DuplicateResolutionStrategy
        ?? NotchApplySimulationDuplicateDiffResolutionStrategy.MergeSumActiveRegularPads);
    public string DuplicateDiffResolutionSummaryText => HasDuplicateDiffResolutions
        ? $"Duplicate FW Diff Idx keys: {DuplicateDiffResolutionCount.ToString(CultureInfo.InvariantCulture)} (strategy: {DuplicateDiffResolutionStrategyText})."
        : "No duplicate active FW Diff Idx keys detected.";
    public string DuplicateDiffResolutionSampleText
    {
        get
        {
            if (_snapshot is null || !HasDuplicateDiffResolutions)
            {
                return "IC/diff -";
            }

            var sample = _snapshot.Result.DiffIdentityContract.DuplicateResolutions[0];
            var mergedRegularPadIds = new[] { sample.PrimaryRegularPadId }
                .Concat(sample.SuppressedRegularPadIds)
                .OrderBy(static regularPadId => regularPadId)
                .ToArray();
            var suffix = DuplicateDiffResolutionCount > 1
                ? $" (+{(DuplicateDiffResolutionCount - 1).ToString(CultureInfo.InvariantCulture)})"
                : string.Empty;
            return $"IC{sample.IcIndex + 1}/FW Diff Idx {sample.DiffIndex}: merge REG {string.Join(", ", mergedRegularPadIds)}{suffix}";
        }
    }
    public bool ShowLegendCenterLabel => UsesSignedColorScale;
    public bool ShowSequentialLegend => !ShowLegendCenterLabel;
    public bool HasSelectedImpactItems => SelectedImpactItems.Count > 0;
    public string SelectedImpactSummaryText => !HasSelectedRegular
        ? "Select a regular pad to inspect notch impact."
        : HasSelectedImpactItems
            ? $"{SelectedImpactItems.Count} notch impact(s) affect this regular."
            : "No notch rows affect this regular under the current simulation version.";
    public string SelectedRegularHeaderText => TryGetSelectedRegularPad(out var pad)
        ? $"REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · FW Diff Idx {GetFwDiffIndex(pad.RegularPadId)} · CAD Output FW Diff Idx {GetCadOutputFwDiffIndex(pad.RegularPadId)}"
        : "No regular selected.";
    public string SelectedRegularLocationText => TryGetSelectedRegularPad(out var locationPad)
        ? $"R{locationPad.Row}, C{locationPad.Col}"
        : "-";
    public string SelectedBeforeText => TryGetSelectedCell(out var beforeCell)
        ? FormatWholeNumberDisplay(beforeCell.BeforeValue)
        : "-";
    public string SelectedAfterText => TryGetSelectedCell(out var afterCell)
        ? FormatWholeNumberDisplay(afterCell.AfterValue)
        : "-";
    public string SelectedDeltaText => TryGetSelectedCell(out var deltaCell)
        ? deltaCell.DeltaValue.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture)
        : "-";
    public string SelectedPercentChangeText
    {
        get
        {
            if (!TryGetSelectedCell(out var cell) || Math.Abs(cell.BeforeValue) <= SignificantDeltaEpsilon)
            {
                return "-";
            }

            var percent = ((cell.AfterValue - cell.BeforeValue) / cell.BeforeValue) * 100d;
            return percent.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture) + "%";
        }
    }

    public string SelectedSourceText => SelectedInputSourceOption.Value switch
    {
        SimulationInputSourceMode.Manual => HasSelectedOverride ? "Manual override" : "Global baseline",
        SimulationInputSourceMode.Csv => HasCsvData ? "CSV frame projection" : "CSV not loaded",
        SimulationInputSourceMode.Copper => "CAD overlap -> CAD Output FW Diff",
        _ => "-",
    };
    public string SelectedDiffBadgeText => TryGetSelectedRegularPad(out var badgePad)
        ? $"FW Diff Idx {GetFwDiffIndex(badgePad.RegularPadId)} · CAD Output FW Diff Idx {GetCadOutputFwDiffIndex(badgePad.RegularPadId)}"
        : "-";
    public string CanvasHintText => HasSimulationData
        ? string.Empty
        : $"No projected regular pads under {SelectedAreaOption.Display}.";
    public bool HasCanvasHint => !HasSimulationData;
    public bool ShowSelectedOverrideEditor => HasSelectedRegular && IsManualSource;
    public string CopperPositionText =>
        $"X {CopperCenterX.ToString("0.###", CultureInfo.InvariantCulture)} · Y {CopperCenterY.ToString("0.###", CultureInfo.InvariantCulture)} · Ø {CopperDiameter.ToString("0.###", CultureInfo.InvariantCulture)}";
    public string CopperSummaryText =>
        $"{SelectedCopperContactModelOption.Display} · {CopperPositionText}";
    public string CopperContactModelSummaryText =>
        $"{SelectedCopperContactModelOption.Description} Peak {FormatWholeNumberDisplay(CopperPeakValue)}.";
    public string CopperProjectionModelText =>
        $"Input path: {SelectedCopperContactModelOption.Display} CAD overlap -> CAD Output FW Diff -> regular/FW Before -> notch After.";
    public string CopperInstructionText =>
        IsCopperSource
            ? "Move the mouse over the AA canvas to update signal in real time. Copper mode runs EMS, net-flow, and geometry audit through the same simulation gate."
            : "Select Copper to drive Before by movable CAD overlap area.";
    public bool HasCopperPathReplay => _copperPathReplayResult.HasSteps;
    public string CopperPathReplaySummaryText => BuildCopperPathReplaySummaryText(_copperPathReplayResult);
    public string CopperPathReplayArtifactSummaryText => HasCopperPathReplay
        ? $"Replay rows {_copperPathReplayArtifactSnapshot.StepCount.ToString(CultureInfo.InvariantCulture)} · EMS {_copperPathReplayArtifactSnapshot.TotalEmsViolationCount.ToString(CultureInfo.InvariantCulture)} · net-flow {_copperPathReplayArtifactSnapshot.TotalNetFlowResidualCount.ToString(CultureInfo.InvariantCulture)} · coverage {_copperPathReplayArtifactSnapshot.TotalTargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)}"
        : "Replay rows: not run.";
    public bool HasSelectedCopperPathReplayRow => SelectedCopperPathReplayRow is not null;

    private int GetFwDiffIndex(int regularPadId)
    {
        return _regularPadById.TryGetValue(regularPadId, out var channelPad)
            ? channelPad.DiffIndex
            : -1;
    }

    private int GetCadOutputFwDiffIndex(int regularPadId)
    {
        return _cadOutputFwDiffByRegularPadId.TryGetValue(regularPadId, out var cadOutputFwDiffIndex)
            ? cadOutputFwDiffIndex
            : GetFwDiffIndex(regularPadId);
    }

    internal SimulationCadOutputFwDiffAssignmentDecisionSummarySnapshot BuildCadOutputFwDiffAssignmentDecisionSummarySnapshot()
    {
        var decisions = _session.CadOutputFwDiffAssignmentDecisions.Values.ToArray();
        if (decisions.Length == 0)
        {
            return SimulationCadOutputFwDiffAssignmentDecisionSummarySnapshot.Empty;
        }

        var mode = decisions
            .GroupBy(static decision => decision.Mode)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .Select(static group => group.Key.ToContractString())
            .FirstOrDefault() ?? "unknown";
        var reasonCounts = decisions
            .GroupBy(static decision => decision.ReasonCode.ToContractString())
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var sourceCounts = decisions
            .GroupBy(static decision => decision.DecisionSource.ToContractString())
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        return new SimulationCadOutputFwDiffAssignmentDecisionSummarySnapshot(
            DecisionCount: decisions.Length,
            Mode: mode,
            ReasonCounts: reasonCounts,
            DecisionSourceCounts: sourceCounts);
    }

    public void MoveCopperToWorldPoint(Point2 worldPoint)
    {
        if (!IsCopperSource)
        {
            return;
        }

        if (Math.Abs(CopperCenterX - worldPoint.X) <= 1e-6 &&
            Math.Abs(CopperCenterY - worldPoint.Y) <= 1e-6)
        {
            return;
        }

        _isUpdatingCopperCenter = true;
        CopperCenterX = worldPoint.X;
        CopperCenterY = worldPoint.Y;
        _isUpdatingCopperCenter = false;
        RefreshCopperSnapshotIfActive();
    }

    public CopperPillarPathReplayResult ReplayCopperPath(Point2 start, Point2 end, int stepCount)
    {
        var result = SimulationWorkspaceUseCase.ReplayCopperPath(
            _session,
            SelectedVersionOption.Value,
            new CopperPillarPathReplayRequest(
                start,
                end,
                stepCount,
                CopperDiameter,
                CopperPeakValue,
                CopperBaselineValue));
        _copperPathReplayResult = result;
        _copperPathReplayArtifactSnapshot = CopperPillarPathReplayArtifactService.BuildSnapshot(result);
        RebuildCopperPathReplayRows();
        StatusText = BuildCopperPathReplaySummaryText(result);
        OnPropertyChanged(nameof(HasCopperPathReplay));
        OnPropertyChanged(nameof(CopperPathReplaySummaryText));
        OnPropertyChanged(nameof(CopperPathReplayArtifactSummaryText));
        return result;
    }

    public void SetCanvasViewMode(NotchApplySimulationCanvasViewMode mode)
    {
        var option = CanvasViewOptions.FirstOrDefault(candidate => candidate.Value == mode);
        if (!option.Equals(default(NotchApplySimulationCanvasViewOption)))
        {
            SelectedCanvasViewOption = option;
        }
    }

    public void ToggleBeforeAfterCanvasView()
    {
        SetCanvasViewMode(SelectedCanvasViewMode == NotchApplySimulationCanvasViewMode.Before
            ? NotchApplySimulationCanvasViewMode.After
            : NotchApplySimulationCanvasViewMode.Before);
    }

    private string BuildCsvShapeSummaryText()
    {
        if (!HasCsvFiles)
        {
            return "Declared Xch/Ych: none.";
        }

        var declared = _csvDataset.Files
            .Select(static file => file.DeclaredCols.HasValue && file.DeclaredRows.HasValue
                ? $"{file.DeclaredCols.Value} x {file.DeclaredRows.Value}"
                : "not declared")
            .GroupBy(static shape => shape)
            .Select(static group => $"{group.Key} ({group.Count()} file(s))");
        var detected = _csvDataset.Files
            .Select(static file => file.FirstFrameCols.HasValue && file.FirstFrameRows.HasValue
                ? $"{file.FirstFrameCols.Value} x {file.FirstFrameRows.Value}"
                : "unknown")
            .GroupBy(static shape => shape)
            .Select(static group => $"{group.Key} ({group.Count()} file(s))");

        return $"Declared Xch/Ych: {string.Join(", ", declared)} · Detected frame: {string.Join(", ", detected)}";
    }

    private string BuildCsvBeforePreviewText()
    {
        var compatibleFrames = _csvDataset.Frames
            .Where(static frame => frame.Projection.IsCompatible)
            .ToArray();
        if (compatibleFrames.Length == 0)
        {
            return HasCsvFiles
                ? "Before preview: no compatible frame. Check Xch/Ych and grid size."
                : "Before preview: no CSV loaded.";
        }

        var selectedIndex = Math.Clamp(SelectedFrameSliderIndex, 0, compatibleFrames.Length - 1);
        var frame = compatibleFrames[selectedIndex];
        var values = frame.Projection.Cells.Select(static cell => cell.Value).ToArray();
        if (values.Length == 0)
        {
            return "Before preview: compatible frame has no projected cells.";
        }

        var min = values.Min();
        var max = values.Max();
        var average = values.Average();
        return $"Before preview: frame {selectedIndex + 1}/{compatibleFrames.Length} · shape {frame.Frame.ColCount} x {frame.Frame.RowCount} · projected {values.Length} cells · range {FormatCompactNumber(min)}..{FormatCompactNumber(max)} · avg {FormatCompactNumber(average)}";
    }

    private static string FormatCompactNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static double ResolveDefaultCopperDiameter(RegularGrid grid)
    {
        var widths = grid.Pads
            .Select(static pad => pad.Bounds.Width)
            .Where(static width => width > 1e-9)
            .OrderBy(static width => width)
            .ToArray();
        var heights = grid.Pads
            .Select(static pad => pad.Bounds.Height)
            .Where(static height => height > 1e-9)
            .OrderBy(static height => height)
            .ToArray();
        if (widths.Length == 0 || heights.Length == 0)
        {
            return 9d;
        }

        return Math.Min(widths[widths.Length / 2], heights[heights.Length / 2]);
    }

    private static string BuildCopperPathReplaySummaryText(CopperPillarPathReplayResult result)
    {
        if (!result.HasSteps)
        {
            return "Copper path replay: not run.";
        }

        var worst = result.WorstStep;
        var worstText = worst is null
            ? "Max After -"
            : $"Max After {worst.MaxAfterValue.ToString("0.###", CultureInfo.InvariantCulture)} at step {worst.StepIndex.ToString(CultureInfo.InvariantCulture)}";
        var isSupported = !result.HasUnsupportedSteps;
        var baseStatusText = SimulationSafetyTextProjector.BuildReplayStatusText(
            isSupported,
            result.HasEmsViolations,
            result.HasPhysicalAuditRisks);
        var statusText = isSupported && result.HasEmsViolations
            ? $"{baseStatusText} {result.TotalEmsViolationCount.ToString(CultureInfo.InvariantCulture)}"
            : baseStatusText;
        return $"Copper path replay: {result.Steps.Count.ToString(CultureInfo.InvariantCulture)} point(s) · {worstText} · {statusText}.";
    }

    private static string FormatDuplicateDiffResolutionStrategy(
        NotchApplySimulationDuplicateDiffResolutionStrategy strategy)
    {
        return strategy switch
        {
            NotchApplySimulationDuplicateDiffResolutionStrategy.MergeSumActiveRegularPads => "merge-sum-active-regular-pads",
            _ => "unknown"
        };
    }
}
