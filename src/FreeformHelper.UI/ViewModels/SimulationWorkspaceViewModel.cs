using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Simulation;
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
    IReadOnlyList<NotchApplySimulationImpactItemViewModel> ImpactItems);

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

    private const double DefaultGlobalValue = 0d;
    private const double DefaultCopperPeakValue = 400d;
    private const double DefaultFingerPeakValue = 360d;
    private const double DefaultCopperBaselineValue = 0d;
    private const int DefaultPlaybackFramesPerSecond = 6;
    private static readonly IReadOnlyList<SimulationInputSourceOption> _sharedInputSourceOptions =
    [
        new SimulationInputSourceOption(SimulationInputSourceMode.Manual, "Manual"),
        new SimulationInputSourceOption(SimulationInputSourceMode.Csv, "CSV"),
        new SimulationInputSourceOption(SimulationInputSourceMode.Copper, "Copper"),
    ];
    private static readonly IReadOnlyList<SimulationContactModelOption> _sharedCopperContactModelOptions =
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

    private static readonly NotchApplySimulationCanvasViewOption[] _sharedCanvasViewOptions =
    {
        new(NotchApplySimulationCanvasViewMode.Before, "Before"),
        new(NotchApplySimulationCanvasViewMode.After, "After"),
        new(NotchApplySimulationCanvasViewMode.Delta, "Delta"),
        new(NotchApplySimulationCanvasViewMode.ChangedOnly, "Changed only"),
    };

    private static readonly NotchApplySimulationCanvasColorOption[] _sharedCanvasColorOptions =
    {
        new(NotchApplySimulationCanvasColorMode.Auto, "AUTO"),
        new(NotchApplySimulationCanvasColorMode.Threshold, "TH"),
    };

    private readonly SimulationSourceOwner<SimulationScenarioSnapshot, NotchApplySimulationImportedDataset> _source;
    private readonly SimulationPlaybackOwner _playback;
    private readonly SimulationCopperOwner _copper;
    private readonly SimulationCanvasOwner<SimulationOverlayProjection> _canvas;
    private readonly SimulationSelectionOwner _selection;
    private readonly SimulationWorkspaceUseCase _useCase;
    private readonly SimulationWorkspaceSession _session;
    private readonly IReadOnlyList<CadPad> _cadPads;
    private readonly bool _showCad;
    private readonly bool _showRegular;
    private readonly bool _highlightFreeform;
    private readonly bool _highlightUnmatched;
    private NotchApplySimulationImportedDataset _csvDataset => _source.Dataset;
    private IReadOnlyDictionary<int, double> _manualOverridesByRegularPadId => _source.ManualOverrides;
    private SimulationScenarioSnapshot? _snapshot => _source.Current;

    public SimulationWorkspaceViewModel(
        SimulationWorkspaceUseCase useCase,
        SimulationWorkspaceSession session)
    {
        _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _cadPads = _session.CadPads;
        _showCad = false;
        _showRegular = _session.DefaultShowRegular;
        _highlightFreeform = _session.DefaultHighlightFreeform;
        _highlightUnmatched = _session.DefaultHighlightUnmatched;

        _canvas = CreateCanvasOwner();
        _selection = CreateSelectionOwner();

        FitCanvasCommand = new RelayCommand(() => FitCanvasRequested?.Invoke(this, EventArgs.Empty));
        _source = CreateSourceOwner();
        _playback = new SimulationPlaybackOwner(
            new SimulationPlaybackOwner.Seed(0, false, true, DefaultPlaybackFramesPerSecond),
            () => IsCsvSource, () => FrameOptions, RefreshSnapshot, RefreshCommandState,
            CreatePlaybackTimer, name => OnPropertyChanging(name), name => OnPropertyChanged(name));
        _copper = CreateCopperOwner();
        _playback.EnsureTimer();

        _statusText = "Simulation ready. Use Manual to set baseline values, or import CSV for frame playback.";

        _source.RefreshFrameOptions();
        RefreshSnapshot();
    }

    private SimulationSourceOwner<SimulationScenarioSnapshot, NotchApplySimulationImportedDataset> CreateSourceOwner()
    {
        var versions = BuildVersionOptions(_session.Table);
        return new SimulationSourceOwner<SimulationScenarioSnapshot, NotchApplySimulationImportedDataset>(
            new SimulationSourceOwner<SimulationScenarioSnapshot, NotchApplySimulationImportedDataset>.Seed(
                _sharedInputSourceOptions[0], versions[0], DefaultGlobalValue),
            _sharedInputSourceOptions, versions, _session.FwDiffGrid.Pads,
            NotchApplySimulationImportedDataset.Empty, dataset => dataset.CompatibleFrameCount > 0,
            dataset => dataset.Files.Select(file => new NotchApplySimulationSourceFileItemViewModel(
                file.FileName, file.SummaryText, file.ShapeText)),
            dataset => dataset.Frames.Where(static frame => frame.Projection.IsCompatible)
                .Select(static frame => new NotchApplySimulationFrameOption(frame.GlobalIndex, frame.DisplayText)),
            ImportCsvSources,
            (version, dataset, frameIndex) => SimulationWorkspaceUseCase.BuildScenarioSnapshot(
                _session, SimulationWorkspaceUseCase.BuildCsvScenario(
                    dataset, version, NotchApplySimulationAggregationMode.SingleFrame, frameIndex)),
            (version, globalValue, overrides) => SimulationWorkspaceUseCase.BuildScenarioSnapshot(
                _session, SimulationWorkspaceUseCase.BuildManualScenario(_session, version, globalValue, overrides)),
            version => SimulationWorkspaceUseCase.BuildScenarioSnapshot(
                _session, SimulationWorkspaceUseCase.BuildCopperScenario(_session, version, _copper.CaptureRequest())),
            AcceptSnapshot, () => SelectedFrameSliderIndex, StopPlayback, () => _playback.ReconcileFrames(),
            RefreshCommandState, RefreshSelectionPresentation,
            () => HasSelectedRegular, () => SelectedRegularPadId,
            () => SelectedManualValue, value => SelectedManualValue = value,
            _selection.IsPadInSelectedAreaAndActiveSurface, () => SelectedCanvasViewMode,
            () => SelectedAreaOption.Display, value => StatusText = value,
            name => OnPropertyChanging(name), name => OnPropertyChanged(name));
    }

    private (bool Success, NotchApplySimulationImportedDataset Dataset, string Diagnostic) ImportCsvSources(
        IReadOnlyList<string> paths)
    {
        var success = _useCase.TryImportCsvSources(paths, _session.FwDiffGrid, out var dataset, out var diagnostic);
        return (success, dataset, diagnostic);
    }

    private SimulationCopperOwner CreateCopperOwner()
    {
        var centerX = (_session.FwDiffGrid.Bounds.MinX + _session.FwDiffGrid.Bounds.MaxX) * 0.5d;
        var centerY = (_session.FwDiffGrid.Bounds.MinY + _session.FwDiffGrid.Bounds.MaxY) * 0.5d;
        var seed = new SimulationCopperOwner.Seed(
            _sharedCopperContactModelOptions[0], centerX, centerY,
            ResolveDefaultCopperDiameter(_session.FwDiffGrid), DefaultCopperPeakValue, DefaultCopperBaselineValue,
            (decimal)_session.FwDiffGrid.Bounds.MinX, (decimal)centerY,
            (decimal)_session.FwDiffGrid.Bounds.MaxX, (decimal)centerY, 9m, null);
        return new SimulationCopperOwner(
            seed, _sharedCopperContactModelOptions, () => IsCopperSource,
            () => SelectedInputSourceOption = SourceOptions.First(option => option.Value == SimulationInputSourceMode.Copper),
            () => SelectedVersionOption.Value,
            (version, request) => SimulationWorkspaceUseCase.ReplayCopperPath(_session, version, request),
            BuildCopperPathReplaySummaryText, RefreshSnapshot, value => StatusText = value,
            name => OnPropertyChanging(name), name => OnPropertyChanged(name));
    }

    public void MoveCopperToWorldPoint(Point2 worldPoint) => _copper.MoveCopperToWorldPoint(worldPoint);

    public CopperPillarPathReplayResult ReplayCopperPath(Point2 start, Point2 end, int stepCount) =>
        _copper.ReplayCopperPath(start, end, stepCount);

    private void StopPlayback() => _playback.Stop();

    private static Dictionary<int, int> BuildCadOutputFwDiffByRegularPadId(SimulationWorkspaceSession session) =>
        session.CadOutputFwDiffGrid.Pads.ToDictionary(
            static pad => pad.RegularPadId,
            static pad => pad.DiffIndex);

    private static Dictionary<(int IcIndex, int DiffIndex), int[]> BuildRegularPadIdsByIcDiff(SimulationWorkspaceSession session) =>
        session.FwDiffGrid.Pads
            .GroupBy(static pad => (pad.IcIndex, pad.DiffIndex))
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static pad => pad.RegularPadId).ToArray());

    public event EventHandler? FitCanvasRequested;
    public Func<Task<IReadOnlyList<string>>>? PickOpenDiffCsvPathsAsync
    {
        get => _source.PickOpenDiffCsvPathsAsync;
        set => _source.PickOpenDiffCsvPathsAsync = value;
    }
    public Func<string, Task<bool>>? RequestSetClipboardTextAsync
    {
        get => _copper.RequestSetClipboardTextAsync;
        set => _copper.RequestSetClipboardTextAsync = value;
    }
    public Func<SimulationTextExportRequest, Task<bool>>? RequestSaveTextFileAsync
    {
        get => _copper.RequestSaveTextFileAsync;
        set => _copper.RequestSaveTextFileAsync = value;
    }

    public IReadOnlyList<SimulationInputSourceOption> SourceOptions => _source.SourceOptions;
    public IReadOnlyList<SimulationContactModelOption> CopperContactModelOptions => _copper.CopperContactModelOptions;
    public IReadOnlyList<NotchApplySimulationCanvasViewOption> CanvasViewOptions => _canvas.CanvasViewOptions;
    public IReadOnlyList<NotchApplySimulationCanvasColorOption> CanvasColorOptions => _canvas.CanvasColorOptions;
    public IReadOnlyList<NotchApplySimulationVersionOption> VersionOptions => _source.VersionOptions;
    public IReadOnlyList<NotchApplySimulationAreaOption> AreaOptions => _canvas.AreaOptions;
    public ReadOnlyObservableCollection<NotchApplySimulationSourceFileItemViewModel> CsvFiles => _source.CsvFiles;
    public ReadOnlyObservableCollection<NotchApplySimulationFrameOption> FrameOptions => _source.FrameOptions;
    public ReadOnlyObservableCollection<NotchApplySimulationImpactItemViewModel> SelectedImpactItems => _selection.SelectedImpactItems;
    public ReadOnlyObservableCollection<SimulationSafetyRiskDiffViewModel> SimulationHighRiskDiffs => _selection.SimulationHighRiskDiffs;
    public ReadOnlyObservableCollection<CopperPillarPathReplayArtifactRow> CopperPathReplayRows => _copper.CopperPathReplayRows;

    public IAsyncRelayCommand ImportCsvCommand => _source.ImportCsvCommand;
    public IRelayCommand ClearCsvCommand => _source.ClearCsvCommand;
    public IRelayCommand RandomizeVisibleValuesCommand => _source.RandomizeVisibleValuesCommand;
    public IRelayCommand<string?> ApplyManualPresetCommand => _source.ApplyManualPresetCommand;
    public IRelayCommand TogglePlaybackCommand => _playback.TogglePlaybackCommand;
    public IRelayCommand PreviousFrameCommand => _playback.PreviousFrameCommand;
    public IRelayCommand NextFrameCommand => _playback.NextFrameCommand;
    public IRelayCommand ApplySelectedOverrideCommand => _source.ApplySelectedOverrideCommand;
    public IRelayCommand ClearSelectedOverrideCommand => _source.ClearSelectedOverrideCommand;
    public IRelayCommand ClearAllManualOverridesCommand => _source.ClearAllManualOverridesCommand;
    public IRelayCommand FitCanvasCommand { get; }
    public IRelayCommand<SimulationSafetyRiskDiffViewModel?> SelectSimulationRiskDiffCommand => _selection.SelectSimulationRiskDiffCommand;
    public IRelayCommand ReplayCopperPathCommand => _copper.ReplayCopperPathCommand;
    public IRelayCommand UseCurrentCopperAsPathStartCommand => _copper.UseCurrentCopperAsPathStartCommand;
    public IRelayCommand UseCurrentCopperAsPathEndCommand => _copper.UseCurrentCopperAsPathEndCommand;
    public IRelayCommand<CopperPillarPathReplayArtifactRow?> SelectCopperPathReplayRowCommand => _copper.SelectCopperPathReplayRowCommand;
    public IAsyncRelayCommand CopyCopperPathReplayCommand => _copper.CopyCopperPathReplayCommand;
    public IAsyncRelayCommand ExportCopperPathReplayCsvCommand => _copper.ExportCopperPathReplayCsvCommand;
    public IAsyncRelayCommand ExportCopperPathReplayJsonCommand => _copper.ExportCopperPathReplayJsonCommand;

    public SimulationInputSourceOption SelectedInputSourceOption
    {
        get => _source.SelectedInputSourceOption;
        set => _source.SelectedInputSourceOption = value;
    }
    public SimulationContactModelOption SelectedCopperContactModelOption
    {
        get => _copper.SelectedCopperContactModelOption;
        set => _copper.SelectedCopperContactModelOption = value;
    }
    public NotchApplySimulationVersionOption SelectedVersionOption
    {
        get => _source.SelectedVersionOption;
        set => _source.SelectedVersionOption = value;
    }
    public NotchApplySimulationCanvasViewOption SelectedCanvasViewOption
    {
        get => _canvas.SelectedCanvasViewOption;
        set => _canvas.SelectedCanvasViewOption = value;
    }
    public NotchApplySimulationCanvasColorOption SelectedCanvasColorOption
    {
        get => _canvas.SelectedCanvasColorOption;
        set => _canvas.SelectedCanvasColorOption = value;
    }
    public NotchApplySimulationAreaOption SelectedAreaOption
    {
        get => _canvas.SelectedAreaOption;
        set => _canvas.SelectedAreaOption = value;
    }
    public double ColorThresholdValue
    {
        get => _canvas.ColorThresholdValue;
        set => _canvas.ColorThresholdValue = value;
    }
    public double GlobalValue
    {
        get => _source.GlobalValue;
        set => _source.GlobalValue = value;
    }
    public double CopperCenterX
    {
        get => _copper.CopperCenterX;
        set => _copper.CopperCenterX = value;
    }
    public double CopperCenterY
    {
        get => _copper.CopperCenterY;
        set => _copper.CopperCenterY = value;
    }
    public double CopperDiameter
    {
        get => _copper.CopperDiameter;
        set => _copper.CopperDiameter = value;
    }
    public double CopperPeakValue
    {
        get => _copper.CopperPeakValue;
        set => _copper.CopperPeakValue = value;
    }
    public double CopperBaselineValue
    {
        get => _copper.CopperBaselineValue;
        set => _copper.CopperBaselineValue = value;
    }
    public decimal CopperPathStartX
    {
        get => _copper.CopperPathStartX;
        set => _copper.CopperPathStartX = value;
    }
    public decimal CopperPathStartY
    {
        get => _copper.CopperPathStartY;
        set => _copper.CopperPathStartY = value;
    }
    public decimal CopperPathEndX
    {
        get => _copper.CopperPathEndX;
        set => _copper.CopperPathEndX = value;
    }
    public decimal CopperPathEndY
    {
        get => _copper.CopperPathEndY;
        set => _copper.CopperPathEndY = value;
    }
    public decimal CopperPathStepCount
    {
        get => _copper.CopperPathStepCount;
        set => _copper.CopperPathStepCount = value;
    }
    public CopperPillarPathReplayArtifactRow? SelectedCopperPathReplayRow
    {
        get => _copper.SelectedCopperPathReplayRow;
        set => _copper.SelectedCopperPathReplayRow = value;
    }
    public int SelectedFrameSliderIndex
    {
        get => _playback.SelectedFrameSliderIndex;
        set => _playback.SelectedFrameSliderIndex = value;
    }
    public bool IsPlaybackRunning
    {
        get => _playback.IsPlaybackRunning;
        set => _playback.IsPlaybackRunning = value;
    }
    public bool IsPlaybackLoopEnabled
    {
        get => _playback.IsPlaybackLoopEnabled;
        set => _playback.IsPlaybackLoopEnabled = value;
    }
    public bool IsSimulationSafetyExpanded
    {
        get => _selection.IsSimulationSafetyExpanded;
        set => _selection.IsSimulationSafetyExpanded = value;
    }
    public int PlaybackFramesPerSecond
    {
        get => _playback.PlaybackFramesPerSecond;
        set => _playback.PlaybackFramesPerSecond = value;
    }
    public int SelectedRegularPadId
    {
        get => _selection.SelectedRegularPadId;
        set => _selection.SelectedRegularPadId = value;
    }
    public double SelectedManualValue
    {
        get => _selection.SelectedManualValue;
        set => _selection.SelectedManualValue = value;
    }
    [ObservableProperty] private string _statusText;

    public IReadOnlyList<CadPad> CadPads => _cadPads;
    public IReadOnlyList<RegularPad> RegularPads => _session.FwDiffGrid.Pads;
    public IReadOnlyList<NotchApplySimulationAaDisplayCell> SimulationOverlayItems => _canvas.SimulationOverlayItems;
    public NotchApplySimulationCanvasViewMode SelectedCanvasViewMode => _canvas.SelectedCanvasViewMode;
    public NotchApplySimulationCanvasColorMode SelectedCanvasColorMode => _canvas.SelectedCanvasColorMode;
    public bool ShowCad => _showCad;
    public bool ShowRegular => _showRegular;
    public bool HighlightFreeform => _highlightFreeform;
    public bool HighlightUnmatched => _highlightUnmatched;
    public bool CanImportCsv => _source.CanImportCsv;
    public bool HasCsvFiles => _source.HasCsvFiles;
    public bool HasCsvData => _source.HasCsvData;
    public bool IsCsvSource => _source.IsCsvSource;
    public bool IsCopperSource => _source.IsCopperSource;
    public bool IsManualSource => _source.IsManualSource;
    public bool ShowSimulationDiagnostics => _snapshot is not null;
    public bool HasUserDerivedState => HasCsvFiles || HasManualOverrides || IsCopperSource;
    public bool CanAutoRefreshFromSourceChange => !HasUserDerivedState;
    public bool CanPlayFrames => _playback.CanPlayFrames;
    public bool CanStepFrames => _playback.CanStepFrames;
    public bool CanRandomizeVisibleValues => _source.CanRandomizeVisibleValues;
    public bool IsThresholdMode => _canvas.IsThresholdMode;
    public bool IsSignedCanvasView => _canvas.IsSignedCanvasView;
    public bool UsesSignedColorScale => _canvas.UsesSignedColorScale;
    public bool HasSelectedRegular => _selection.HasSelectedRegular;
    public bool HasManualOverrides => _source.HasManualOverrides;
    public bool HasSelectedOverride => _source.HasSelectedOverride;
    public int ActiveSurfaceRegularCount => _session.ActiveRegularPadIds.Count;
    public string SessionSummaryText => $"Grid {_session.RowCount} x {_session.ColumnCount} · REG {_session.PadCount} · Rows {_session.Table.Rows.Count}";
    public int SourceRevision => _session.SourceRevision;
    public string AreaSummaryText => _canvas.AreaSummaryText;
    public string SourceModeSummaryText => SelectedInputSourceOption.Value switch
    {
        SimulationInputSourceMode.Csv => "Source: CSV",
        SimulationInputSourceMode.Copper => "Source: Copper",
        _ => "Source: Manual",
    };
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
    public string FrameSummaryText => IsCsvSource && FrameOptions.Count > 0
        ? $"Frame {SelectedFrameSliderIndex + 1}/{FrameOptions.Count}"
        : IsCopperSource
            ? "Copper frame"
            : "Manual frame";
    public string VersionSummaryText => $"Version: {SelectedVersionOption.Display}";
    public string ViewModeSummaryText => _canvas.ViewModeSummaryText;
    public string ColorModeSummaryText => _canvas.ColorModeSummaryText;
    public string CanvasStatusSummaryText => HasSimulationData
        ? StatusText
        : $"Mode ready · {AreaSummaryText}";
    public string PlaybackButtonText => IsPlaybackRunning ? "Pause" : "Play";
    public string PlaybackButtonGlyph => IsPlaybackRunning ? IconGlyphs.Pause : IconGlyphs.PlayArrow;
    public int SelectedFrameSliderMaximum => Math.Max(FrameOptions.Count - 1, 0);
    public bool HasSimulationData => _canvas.HasSimulationData;
    public string PlaybackLoopText => IsPlaybackLoopEnabled ? "Loop on" : "Loop off";
    public string SelectedFrameDisplayText => IsCsvSource && FrameOptions.Count > 0 && SelectedFrameSliderIndex >= 0 && SelectedFrameSliderIndex < FrameOptions.Count
        ? FrameOptions[SelectedFrameSliderIndex].Display
        : IsCopperSource
            ? CopperSummaryText
            : "Manual baseline";
    public string ColorLegendTitleText => _canvas.ColorLegendTitleText;
    public string ColorLegendMeaningText => _canvas.ColorLegendMeaningText;
    public string ColorLegendStartText => _canvas.ColorLegendStartText;
    public string ColorLegendCenterText => _canvas.ColorLegendCenterText;
    public string ColorLegendEndText => _canvas.ColorLegendEndText;
    public bool HasDuplicateDiffResolutions => _snapshot?.SimulationResult.DiffIdentityContract.HasDuplicateResolutions ?? false;
    public int DuplicateDiffResolutionCount => _snapshot?.SimulationResult.DiffIdentityContract.DuplicateResolutionCount ?? 0;
    public string DuplicateDiffResolutionStrategyText => FormatDuplicateDiffResolutionStrategy(
        _snapshot?.SimulationResult.DiffIdentityContract.DuplicateResolutionStrategy
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

            var sample = _snapshot.SimulationResult.DiffIdentityContract.DuplicateResolutions[0];
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
    public bool ShowLegendCenterLabel => _canvas.ShowLegendCenterLabel;
    public bool ShowSequentialLegend => _canvas.ShowSequentialLegend;
    public bool HasSelectedImpactItems => _selection.HasSelectedImpactItems;
    public string SelectedImpactSummaryText => _selection.SelectedImpactSummaryText;
    public string SelectedRegularHeaderText => _selection.SelectedRegularHeaderText;
    public string SelectedRegularLocationText => _selection.SelectedRegularLocationText;
    public string SelectedBeforeText => _selection.SelectedBeforeText;
    public string SelectedAfterText => _selection.SelectedAfterText;
    public string SelectedDeltaText => _selection.SelectedDeltaText;
    public string SelectedPercentChangeText => _selection.SelectedPercentChangeText;

    public string SelectedSourceText => _selection.SelectedSourceText;
    public string CanvasHintText => _canvas.CanvasHintText;
    public bool HasCanvasHint => _canvas.HasCanvasHint;
    public bool ShowSelectedOverrideEditor => _selection.ShowSelectedOverrideEditor;
    public string CopperPositionText => _copper.CopperPositionText;
    public string CopperSummaryText => _copper.CopperSummaryText;
    public string CopperContactModelSummaryText => _copper.CopperContactModelSummaryText;
    public string CopperProjectionModelText => _copper.CopperProjectionModelText;
    public string CopperInstructionText => _copper.CopperInstructionText;
    public bool HasCopperPathReplay => _copper.HasCopperPathReplay;
    public string CopperPathReplaySummaryText => _copper.CopperPathReplaySummaryText;
    public string CopperPathReplayArtifactSummaryText => _copper.CopperPathReplayArtifactSummaryText;

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

    public void SetCanvasViewMode(NotchApplySimulationCanvasViewMode mode) => _canvas.SetCanvasViewMode(mode);

    public void ToggleBeforeAfterCanvasView() => _canvas.ToggleBeforeAfterCanvasView();

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
