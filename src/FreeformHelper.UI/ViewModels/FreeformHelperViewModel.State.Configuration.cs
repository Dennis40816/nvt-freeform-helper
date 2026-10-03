using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // --- Static/Constant UI Options ---

    /// <summary>
    /// Gets a read-only list of all possible <see cref="ScanOrder"/> enumeration values.
    /// </summary>
    public IReadOnlyList<ScanOrder> ScanOrders { get; }
    /// <summary>
    /// Gets a read-only list of <see cref="ScanOrderOption"/> objects for display in UI dropdowns.
    /// </summary>
    public IReadOnlyList<ScanOrderOption> ScanOrderOptions { get; }
    /// <summary>
    /// Gets a read-only list of <see cref="GridAlignmentOption"/> objects for display in UI dropdowns.
    /// </summary>
    public IReadOnlyList<GridAlignmentOption> GridAlignmentOptions { get; }
    /// <summary>
    /// Gets a read-only list of <see cref="RegularSourceModeOption"/> objects for display in UI dropdowns.
    /// </summary>
    public IReadOnlyList<RegularSourceModeOption> RegularSourceModeOptions { get; }
    /// <summary>
    /// Gets a read-only list of <see cref="DxfEditRotationScopeOption"/> objects for DXF rotation scope selection.
    /// </summary>
    public IReadOnlyList<DxfEditRotationScopeOption> DxfEditRotationScopeOptions { get; }
    /// <summary>
    /// Gets a read-only list of <see cref="LogLevelOption"/> objects for display in UI dropdowns.
    /// </summary>
    public IReadOnlyList<LogLevelOption> LogLevelOptions { get; }
    /// <summary>
    /// Gets a read-only list of <see cref="CadOutputFwDiffAutoModeOption"/> objects for Step 4 auto CAD Output FW Diff mode.
    /// </summary>
    public IReadOnlyList<CadOutputFwDiffAutoModeOption> CadOutputFwDiffAutoModeOptions { get; }
    /// <summary>
    /// Gets a read-only list of notch export file-type options.
    /// </summary>
    public IReadOnlyList<NotchExportFileTypeOption> NotchExportFileTypeOptions { get; }
    /// <summary>
    /// Gets a read-only list of notch C-export profile options.
    /// </summary>
    public IReadOnlyList<NotchExportProfileOption> NotchExportProfileOptions { get; }
    /// <summary>
    /// Gets a read-only list of Step3 compensation model options.
    /// </summary>
    public IReadOnlyList<NotchCompensationModelOption> NotchCompensationModelOptions { get; }

    // --- Settings Exposed in UI (persisted via project settings) ---

    // View settings
    /// <summary>
    /// Gets or sets the area bucket tolerance for coloring pads by area.
    /// </summary>
    [ObservableProperty] private decimal _areaBucketTolerance = 0.001m;
    /// <summary>
    /// Gets or sets the maximum number of area buckets to use for coloring pads.
    /// </summary>
    [ObservableProperty] private decimal _maxAreaBuckets = 32m;
    /// <summary>
    /// Gets or sets the global UI font size scale in percent.
    /// </summary>
    [ObservableProperty] private decimal _globalFontSizePercent = 120m;
    /// <summary>
    /// Gets the global UI scale factor derived from <see cref="GlobalFontSizePercent"/>.
    /// </summary>
    public double GlobalFontScaleFactor => Math.Clamp((double)GlobalFontSizePercent / 100.0, 0.8, 1.4);
    /// <summary>
    /// Gets the inherited base font size used by shell/window (layout-safe, no render zoom).
    /// </summary>
    public double GlobalFontBaseSize => 12.0 * GlobalFontScaleFactor;

    // Grid settings
    /// <summary>
    /// Gets or sets the number of X channels (columns) in the grid.
    /// </summary>
    [ObservableProperty] private decimal _xChannels;
    /// <summary>
    /// Gets or sets the number of Y channels (rows) in the grid.
    /// </summary>
    [ObservableProperty] private decimal _yChannels;
    /// <summary>
    /// Gets or sets the number of cascaded ICs.
    /// </summary>
    [ObservableProperty] private decimal _cascadeNum;
    /// <summary>
    /// Gets or sets the default coordinate-planner pixel width.
    /// </summary>
    [ObservableProperty] private decimal _coordinatePixelWidth = 1m;
    /// <summary>
    /// Gets or sets the default coordinate-planner pixel height.
    /// </summary>
    [ObservableProperty] private decimal _coordinatePixelHeight = 1m;
    /// <summary>
    /// Gets or sets the preferred AA outline layer name used by Coordinate.
    /// </summary>
    [ObservableProperty] private string _coordinatePreferredAaOutlineLayerName = string.Empty;
    /// <summary>
    /// Gets or sets the currently selected <see cref="ScanOrder"/> enumeration value.
    /// </summary>
    [ObservableProperty] private ScanOrder _selectedScanOrder;
    /// <summary>
    /// Gets or sets the currently selected <see cref="ScanOrderOption"/> object.
    /// </summary>
    [ObservableProperty] private ScanOrderOption _selectedScanOrderOption;
    /// <summary>
    /// Gets or sets the currently selected <see cref="GridAlignmentMode"/> enumeration value.
    /// </summary>
    [ObservableProperty] private GridAlignmentMode _gridAlignmentMode;
    /// <summary>
    /// Gets or sets the currently selected <see cref="RegularSourceMode"/> enumeration value.
    /// </summary>
    [ObservableProperty] private RegularSourceMode _regularSourceMode;
    /// <summary>
    /// Gets or sets the currently selected <see cref="GridAlignmentOption"/> object.
    /// </summary>
    [ObservableProperty] private GridAlignmentOption _selectedGridAlignmentOption;
    /// <summary>
    /// Gets or sets the currently selected <see cref="RegularSourceModeOption"/> object.
    /// </summary>
    [ObservableProperty] private RegularSourceModeOption _selectedRegularSourceModeOption;
    /// <summary>
    /// Gets or sets the currently selected application log level option.
    /// </summary>
    [ObservableProperty] private LogLevelOption _selectedLogLevelOption;

    /// <summary>
    /// Gets a value indicating whether the current <see cref="GridAlignmentMode"/> is <see cref="GridAlignmentMode.FromPanelAa"/>.
    /// </summary>
    public bool IsPanelAlignment => GridAlignmentMode == GridAlignmentMode.FromPanelAa;
    /// <summary>
    /// Gets a value indicating whether regular pads are sourced from a DXF layer.
    /// </summary>
    public bool IsDxfLayerRegularSource => RegularSourceMode == RegularSourceMode.FromDxfLayer;

    /// <summary>
    /// Gets or sets the grid padding percentage.
    /// </summary>
    [ObservableProperty] private decimal _gridPaddingPercent;
    /// <summary>
    /// Gets or sets the active area width.
    /// </summary>
    [ObservableProperty] private decimal _activeAreaWidth = 310m;
    /// <summary>
    /// Gets or sets the active area height.
    /// </summary>
    [ObservableProperty] private decimal _activeAreaHeight = 174m;
    /// <summary>
    /// Gets or sets the panel bias X.
    /// </summary>
    [ObservableProperty] private decimal _panelBiasX;
    /// <summary>
    /// Gets or sets the panel bias Y.
    /// </summary>
    [ObservableProperty] private decimal _panelBiasY;

    // Matching settings
    /// <summary>
    /// Gets or sets the diagnostic threshold for highlighting low-coverage regular pads.
    /// </summary>
    [ObservableProperty] private decimal _matchThreshold;
    /// <summary>
    /// Gets or sets the directional spread threshold used for freeform auto-detection.
    /// </summary>
    [ObservableProperty] private decimal _freeformAxisThreshold = 0.55m;
    /// <summary>
    /// Gets or sets a value indicating whether auto-detect can classify XYWay.
    /// </summary>
    [ObservableProperty] private bool _enableAutoDetectXy;
    /// <summary>
    /// Gets or sets a value indicating whether freeform auto-detect should keep one-side
    /// boundary spill as freeform when TH would otherwise suppress it.
    /// </summary>
    [ObservableProperty] private bool _enableFreeformEdgeSpecialization;
    /// <summary>
    /// Gets or sets a value indicating whether project load should auto-run Step 2
    /// freeform auto-detect after Step 1 is restored.
    /// </summary>
    [ObservableProperty] private bool _autoReplayStep2AfterProjectLoad = true;
    /// <summary>
    /// Gets or sets a value indicating whether loading a project should re-apply
    /// app-level visual preferences (view snapshot whitelist) on top of project UI view.
    /// </summary>
    [ObservableProperty] private bool _applyAppVisualPreferencesOnProjectLoad = true;
    /// <summary>
    /// Gets or sets the summary text for the latest freeform auto-detect statistics.
    /// </summary>
    [ObservableProperty] private string _freeformAutoDetectSummary = "Freeform stats: not run.";
    /// <summary>
    /// Gets or sets the rows shown in the freeform auto-detect statistics table.
    /// </summary>
    [ObservableProperty] private ObservableCollection<FreeformAutoDetectStatRow> _freeformAutoDetectRows = new();
    /// <summary>
    /// Gets or sets a value indicating whether freeform auto-detect statistics are available.
    /// </summary>
    [ObservableProperty] private bool _hasFreeformAutoDetectRows;
    // Index mapping settings
    /// <summary>
    /// Gets or sets the IoU weight used for DXF/Regular mapping scoring.
    /// </summary>
    [ObservableProperty] private decimal _mappingWeightIou;
    /// <summary>
    /// Gets or sets the centroid distance weight used for DXF/Regular mapping scoring.
    /// </summary>
    [ObservableProperty] private decimal _mappingWeightCentroidDistance;
    /// <summary>
    /// Gets or sets the area ratio weight used for DXF/Regular mapping scoring.
    /// </summary>
    [ObservableProperty] private decimal _mappingWeightAreaRatio;
    /// <summary>
    /// Gets or sets the low-confidence score threshold for mapping suggestions.
    /// </summary>
    [ObservableProperty] private decimal _mappingLowConfidenceThreshold;
    /// <summary>
    /// Gets or sets the ambiguity margin threshold (best - secondBest) for mapping suggestions.
    /// </summary>
    [ObservableProperty] private decimal _mappingAmbiguousMargin;
    /// <summary>
    /// Gets or sets the legacy number of candidate padding cells used for mapping.
    /// Retained for project compatibility; UI uses <see cref="MappingCandidateNumber"/>.
    /// </summary>
    [ObservableProperty] private decimal _mappingCandidatePaddingCells;
    /// <summary>
    /// Gets or sets the number of retained candidates per CAD pad used for mapping.
    /// </summary>
    [ObservableProperty] private decimal _mappingCandidateNumber = 9m;
    /// <summary>
    /// Gets or sets the starting expected diff idx value for CAD scan-order indexing.
    /// </summary>
    [ObservableProperty] private decimal _cadOutputFwDiffIndexStart;
    /// <summary>
    /// Gets or sets the CAD auto CAD Output FW Diff assignment mode.
    /// </summary>
    [ObservableProperty] private CadOutputFwDiffAutoMode _cadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
    /// <summary>
    /// Gets or sets the selected CAD auto CAD Output FW Diff assignment mode option.
    /// </summary>
    [ObservableProperty] private CadOutputFwDiffAutoModeOption _selectedCadOutputFwDiffAutoModeOption;
    /// <summary>
    /// Gets or sets the CAD id used as diff idx start anchor.
    /// Use -1 to indicate "first CAD in scan order".
    /// </summary>
    [ObservableProperty] private decimal _cadOutputFwDiffIndexAnchorCadId = -1;
    /// <summary>
    /// Gets or sets the summary text for current CAD Output FW Diff assignment policy.
    /// </summary>
    [ObservableProperty] private string _cadOutputFwDiffAssignmentSummary = "CAD Output FW Diff assignment (per-IC): auto, scan-order based.";
    /// <summary>
    /// Gets or sets a value indicating whether to display CAD Output FW Diff overlay hints on canvas.
    /// </summary>
    [ObservableProperty] private bool _showDiffIndexOverlay = true;
    /// <summary>
    /// Gets or sets the CAD output ids that currently use manual CAD Output FW Diff override.
    /// </summary>
    [ObservableProperty] private ObservableCollection<int> _cadOutputFwDiffIndexOverrideCadIds = new();
    /// <summary>
    /// Gets or sets CAD output ids currently used as per-IC auto-numbering anchors.
    /// </summary>
    [ObservableProperty] private ObservableCollection<int> _cadOutputFwDiffIndexAnchorCadIdsForCanvas = new();

    // Sizing scopes for manual adjustments.
    private bool _suppressScopeSync; // Flag to prevent infinite loops during scope changes.
    private bool _suppressNotchThresholdSync; // Flag to prevent threshold synchronization loops.

    /// <summary>
    /// Gets or sets a value indicating whether local sizing (row-local width, column-local height) is enabled.
    /// </summary>
    [ObservableProperty] private bool _useLocalSizing = true;
    /// <summary>
    /// Gets or sets a value indicating whether width adjustment is row-local.
    /// </summary>
    [ObservableProperty] private bool _isWidthRowLocal = true;
    /// <summary>
    /// Gets or sets a value indicating whether height adjustment is column-local.
    /// </summary>
    [ObservableProperty] private bool _isHeightColumnLocal = true;

    /// <summary>
    /// Partial method invoked when <see cref="UseLocalSizing"/> property changes.
    /// Synchronizes <see cref="IsWidthRowLocal"/> and <see cref="IsHeightColumnLocal"/>
    /// and triggers a grid rebuild.
    /// </summary>
    /// <param name="value">The new value of <see cref="UseLocalSizing"/>.</param>
    partial void OnUseLocalSizingChanged(bool value)
    {
        if (_suppressScopeSync) return;
        _suppressScopeSync = true;
        IsWidthRowLocal = value;
        IsHeightColumnLocal = value;
        _suppressScopeSync = false;
        _ = TriggerGridRebuildAsync(requestFit: false);
    }

    /// <summary>
    /// Partial method invoked when <see cref="IsWidthRowLocal"/> property changes.
    /// Synchronizes <see cref="UseLocalSizing"/> and triggers a grid rebuild.
    /// </summary>
    /// <param name="value">The new value of <see cref="IsWidthRowLocal"/>.</param>
    partial void OnIsWidthRowLocalChanged(bool value)
    {
        if (_suppressScopeSync) return;
        _suppressScopeSync = true;
        UseLocalSizing = value && IsHeightColumnLocal;
        _suppressScopeSync = false;
        _ = TriggerGridRebuildAsync(requestFit: false);
    }

    /// <summary>
    /// Partial method invoked when <see cref="IsHeightColumnLocal"/> property changes.
    /// Synchronizes <see cref="UseLocalSizing"/> and triggers a grid rebuild.
    /// </summary>
    /// <param name="value">The new value of <see cref="IsHeightColumnLocal"/>.</param>
    partial void OnIsHeightColumnLocalChanged(bool value)
    {
        if (_suppressScopeSync) return;
        _suppressScopeSync = true;
        UseLocalSizing = IsWidthRowLocal && value;
        _suppressScopeSync = false;
        _ = TriggerGridRebuildAsync(requestFit: false);
    }

    partial void OnGlobalFontSizePercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, GlobalFontSizePercentMin, GlobalFontSizePercentMax);
        if (clamped != value)
        {
            _suppressUndo = true;
            GlobalFontSizePercent = clamped;
            _suppressUndo = false;
            return;
        }

        OnPropertyChanged(nameof(GlobalFontScaleFactor));
        OnPropertyChanged(nameof(GlobalFontBaseSize));
    }

    partial void OnIsNotchExportingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShouldShowNotchExportProgress));
    }

    partial void OnNotchExportProgressChanged(double value)
    {
        OnPropertyChanged(nameof(ShouldShowNotchExportProgress));
    }

    partial void OnNotchValidationDirectItemsChanged(ObservableCollection<NotchValidationDisplayItem> value)
    {
        OnPropertyChanged(nameof(HasNotchValidationDirectItems));
        OnPropertyChanged(nameof(IsNotchValidationDirectEmpty));
    }

    partial void OnNotchValidationItemsChanged(ObservableCollection<NotchValidationDisplayItem> value)
    {
        OnPropertyChanged(nameof(HasNotchValidationItems));
        OnPropertyChanged(nameof(IsNotchValidationItemsEmpty));
    }

    partial void OnNotchValidationIncomingItemsChanged(ObservableCollection<NotchValidationDisplayItem> value)
    {
        OnPropertyChanged(nameof(HasNotchValidationIncomingItems));
        OnPropertyChanged(nameof(IsNotchValidationIncomingEmpty));
    }

    partial void OnNotchValidationOutgoingItemsChanged(ObservableCollection<NotchValidationDisplayItem> value)
    {
        OnPropertyChanged(nameof(HasNotchValidationOutgoingItems));
        OnPropertyChanged(nameof(IsNotchValidationOutgoingEmpty));
    }
}
