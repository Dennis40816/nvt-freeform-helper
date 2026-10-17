using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
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
    // The grid, coordinate, active area, panel offset and match settings are stored once. The properties
    // below forward to the record and raise the same notifications and callbacks as the stored properties
    // they replaced.
    private GridAndMatchSettings _gridAndMatch = GridAndMatchSettings.Initial;

    /// <summary>
    /// Gets or sets the number of X channels (columns) in the grid.
    /// </summary>
    public decimal XChannels
    {
        get => _gridAndMatch.XChannels;
        set
        {
            if (_gridAndMatch.XChannels == value)
            {
                return;
            }

            OnXChannelsChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { XChannels = value };
            OnXChannelsChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnXChannelsChanging(decimal value);

    partial void OnXChannelsChanged(decimal value);

    /// <summary>
    /// Gets or sets the number of Y channels (rows) in the grid.
    /// </summary>
    public decimal YChannels
    {
        get => _gridAndMatch.YChannels;
        set
        {
            if (_gridAndMatch.YChannels == value)
            {
                return;
            }

            OnYChannelsChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { YChannels = value };
            OnYChannelsChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnYChannelsChanging(decimal value);

    partial void OnYChannelsChanged(decimal value);

    /// <summary>
    /// Gets or sets the number of cascaded ICs.
    /// </summary>
    public decimal CascadeNum
    {
        get => _gridAndMatch.CascadeNum;
        set
        {
            if (_gridAndMatch.CascadeNum == value)
            {
                return;
            }

            OnCascadeNumChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { CascadeNum = value };
            OnCascadeNumChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnCascadeNumChanging(decimal value);

    partial void OnCascadeNumChanged(decimal value);

    /// <summary>
    /// Gets or sets the default coordinate-planner pixel width.
    /// </summary>
    public decimal CoordinatePixelWidth
    {
        get => _gridAndMatch.CoordinatePixelWidth;
        set
        {
            if (_gridAndMatch.CoordinatePixelWidth == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { CoordinatePixelWidth = value };
            OnCoordinatePixelWidthChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnCoordinatePixelWidthChanged(decimal value);

    /// <summary>
    /// Gets or sets the default coordinate-planner pixel height.
    /// </summary>
    public decimal CoordinatePixelHeight
    {
        get => _gridAndMatch.CoordinatePixelHeight;
        set
        {
            if (_gridAndMatch.CoordinatePixelHeight == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { CoordinatePixelHeight = value };
            OnCoordinatePixelHeightChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnCoordinatePixelHeightChanged(decimal value);

    /// <summary>
    /// Gets or sets the currently selected <see cref="ScanOrderOption"/> object.
    /// </summary>
    public ScanOrderOption SelectedScanOrderOption
    {
        get => ScanOrderOptions.FirstOrDefault(option => option.Value == SelectedScanOrder);
        set
        {
            if (_settingsSuppression.IsScanOrderSelectionSyncActive || value.Value == SelectedScanOrder)
            {
                return;
            }

            using (_settingsSuppression.EnterScanOrderSelectionSync())
            {
                SelectedScanOrder = value.Value;
            }

            if (!_isLoadingSettings)
            {
                ApplySettingsOrchestration(nameof(SelectedScanOrder));
            }

            OnPropertyChanged(nameof(SelectedScanOrderOption));
        }
    }
    /// <summary>
    /// Gets or sets the currently selected <see cref="GridAlignmentOption"/> object.
    /// </summary>
    public GridAlignmentOption SelectedGridAlignmentOption
    {
        get => GridAlignmentOptions.FirstOrDefault(option => option.Value == GridAlignmentMode);
        set
        {
            if (_settingsSuppression.IsGridAlignmentSelectionSyncActive || value.Value == GridAlignmentMode)
            {
                return;
            }

            using (_settingsSuppression.EnterGridAlignmentSelectionSync())
            {
                GridAlignmentMode = value.Value;
            }

            if (!_isLoadingSettings)
            {
                ApplySettingsOrchestration(nameof(GridAlignmentMode));
            }

            OnPropertyChanged(nameof(SelectedGridAlignmentOption));
        }
    }
    /// <summary>
    /// Gets or sets the currently selected <see cref="RegularSourceModeOption"/> object.
    /// </summary>
    public RegularSourceModeOption SelectedRegularSourceModeOption
    {
        get => RegularSourceModeOptions.FirstOrDefault(option => option.Value == RegularSourceMode);
        set
        {
            if (_settingsSuppression.IsRegularSourceModeSelectionSyncActive || value.Value == RegularSourceMode)
            {
                return;
            }

            using (_settingsSuppression.EnterRegularSourceModeSelectionSync())
            {
                RegularSourceMode = value.Value;
            }

            if (!_isLoadingSettings)
            {
                ApplySettingsOrchestration(nameof(RegularSourceMode));
            }

            OnPropertyChanged(nameof(SelectedRegularSourceModeOption));
        }
    }
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
    public decimal GridPaddingPercent
    {
        get => _gridAndMatch.GridPaddingPercent;
        set
        {
            if (_gridAndMatch.GridPaddingPercent == value)
            {
                return;
            }

            OnGridPaddingPercentChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { GridPaddingPercent = value };
            OnGridPaddingPercentChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnGridPaddingPercentChanging(decimal value);

    partial void OnGridPaddingPercentChanged(decimal value);

    /// <summary>
    /// Gets or sets the active area width.
    /// </summary>
    public decimal ActiveAreaWidth
    {
        get => _gridAndMatch.ActiveAreaWidth;
        set
        {
            if (_gridAndMatch.ActiveAreaWidth == value)
            {
                return;
            }

            OnActiveAreaWidthChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { ActiveAreaWidth = value };
            OnActiveAreaWidthChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnActiveAreaWidthChanging(decimal value);

    partial void OnActiveAreaWidthChanged(decimal value);

    /// <summary>
    /// Gets or sets the active area height.
    /// </summary>
    public decimal ActiveAreaHeight
    {
        get => _gridAndMatch.ActiveAreaHeight;
        set
        {
            if (_gridAndMatch.ActiveAreaHeight == value)
            {
                return;
            }

            OnActiveAreaHeightChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { ActiveAreaHeight = value };
            OnActiveAreaHeightChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnActiveAreaHeightChanging(decimal value);

    partial void OnActiveAreaHeightChanged(decimal value);

    /// <summary>
    /// Gets or sets the panel bias X.
    /// </summary>
    public decimal PanelBiasX
    {
        get => _gridAndMatch.PanelBiasX;
        set
        {
            if (_gridAndMatch.PanelBiasX == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { PanelBiasX = value };
            OnPanelBiasXChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnPanelBiasXChanged(decimal value);

    /// <summary>
    /// Gets or sets the panel bias Y.
    /// </summary>
    public decimal PanelBiasY
    {
        get => _gridAndMatch.PanelBiasY;
        set
        {
            if (_gridAndMatch.PanelBiasY == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { PanelBiasY = value };
            OnPanelBiasYChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnPanelBiasYChanged(decimal value);

    // Matching settings
    /// <summary>
    /// Gets or sets the diagnostic threshold for highlighting low-coverage regular pads.
    /// </summary>
    public decimal MatchThreshold
    {
        get => _gridAndMatch.MatchThreshold;
        set
        {
            if (_gridAndMatch.MatchThreshold == value)
            {
                return;
            }

            OnMatchThresholdChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { MatchThreshold = value };
            OnPropertyChanged();
        }
    }

    partial void OnMatchThresholdChanging(decimal value);

    /// <summary>
    /// Gets or sets the directional spread threshold used for freeform auto-detection.
    /// </summary>
    public decimal FreeformAxisThreshold
    {
        get => _gridAndMatch.FreeformAxisThreshold;
        set
        {
            if (_gridAndMatch.FreeformAxisThreshold == value)
            {
                return;
            }

            OnFreeformAxisThresholdChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { FreeformAxisThreshold = value };
            OnFreeformAxisThresholdChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnFreeformAxisThresholdChanging(decimal value);

    partial void OnFreeformAxisThresholdChanged(decimal value);

    /// <summary>
    /// Gets or sets a value indicating whether auto-detect can classify XYWay.
    /// </summary>
    public bool EnableAutoDetectXy
    {
        get => _gridAndMatch.EnableAutoDetectXy;
        set
        {
            if (_gridAndMatch.EnableAutoDetectXy == value)
            {
                return;
            }

            OnEnableAutoDetectXyChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { EnableAutoDetectXy = value };
            OnEnableAutoDetectXyChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableAutoDetectXyChanging(bool value);

    partial void OnEnableAutoDetectXyChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether freeform auto-detect should keep one-side
    /// boundary spill as freeform when TH would otherwise suppress it.
    /// </summary>
    public bool EnableFreeformEdgeSpecialization
    {
        get => _gridAndMatch.EnableFreeformEdgeSpecialization;
        set
        {
            if (_gridAndMatch.EnableFreeformEdgeSpecialization == value)
            {
                return;
            }

            OnEnableFreeformEdgeSpecializationChanging(value);
            OnPropertyChanging();
            _gridAndMatch = _gridAndMatch with { EnableFreeformEdgeSpecialization = value };
            OnEnableFreeformEdgeSpecializationChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnEnableFreeformEdgeSpecializationChanging(bool value);

    partial void OnEnableFreeformEdgeSpecializationChanged(bool value);

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
    /// Gets or sets the latest freeform auto-detect statistics. The view model replaces the whole
    /// value. The properties below forward to it and raise their change notification when their
    /// own value changed.
    /// </summary>
    [ObservableProperty] private FreeformAutoDetectResult _freeformAutoDetectResult = FreeformAutoDetectResult.CreateNotRun();
    /// <summary>
    /// Gets the summary text for the latest freeform auto-detect statistics.
    /// </summary>
    public string FreeformAutoDetectSummary => FreeformAutoDetectResult.Summary;
    /// <summary>
    /// Gets the rows shown in the freeform auto-detect statistics table.
    /// </summary>
    public ObservableCollection<FreeformAutoDetectStatRow> FreeformAutoDetectRows => FreeformAutoDetectResult.Rows;
    /// <summary>
    /// Gets a value indicating whether freeform auto-detect statistics are available.
    /// </summary>
    public bool HasFreeformAutoDetectRows => FreeformAutoDetectResult.HasRows;
    // Index mapping settings
    /// <summary>
    /// Holds the index mapping numbers. The view model replaces the whole value. The properties
    /// below forward to it and raise their change notification when their own value changed.
    /// </summary>
    private IndexMappingTuning _indexMappingTuning = IndexMappingTuning.Initial;
    /// <summary>
    /// Gets or sets the IoU weight used for DXF/Regular mapping scoring.
    /// </summary>
    public decimal MappingWeightIou
    {
        get => _indexMappingTuning.WeightIou;
        set => SetIndexMappingTuning(_indexMappingTuning with { WeightIou = value });
    }
    /// <summary>
    /// Gets or sets the centroid distance weight used for DXF/Regular mapping scoring.
    /// </summary>
    public decimal MappingWeightCentroidDistance
    {
        get => _indexMappingTuning.WeightCentroidDistance;
        set => SetIndexMappingTuning(_indexMappingTuning with { WeightCentroidDistance = value });
    }
    /// <summary>
    /// Gets or sets the area ratio weight used for DXF/Regular mapping scoring.
    /// </summary>
    public decimal MappingWeightAreaRatio
    {
        get => _indexMappingTuning.WeightAreaRatio;
        set => SetIndexMappingTuning(_indexMappingTuning with { WeightAreaRatio = value });
    }
    /// <summary>
    /// Gets or sets the low-confidence score threshold for mapping suggestions.
    /// </summary>
    public decimal MappingLowConfidenceThreshold
    {
        get => _indexMappingTuning.LowConfidenceThreshold;
        set => SetIndexMappingTuning(_indexMappingTuning with { LowConfidenceThreshold = value });
    }
    /// <summary>
    /// Gets or sets the ambiguity margin threshold (best - secondBest) for mapping suggestions.
    /// </summary>
    public decimal MappingAmbiguousMargin
    {
        get => _indexMappingTuning.AmbiguousMargin;
        set => SetIndexMappingTuning(_indexMappingTuning with { AmbiguousMargin = value });
    }
    /// <summary>
    /// Gets or sets the legacy number of candidate padding cells used for mapping.
    /// Retained for project compatibility; UI uses <see cref="MappingCandidateNumber"/>.
    /// </summary>
    public decimal MappingCandidatePaddingCells
    {
        get => _indexMappingTuning.CandidatePaddingCells;
        set => SetIndexMappingTuning(_indexMappingTuning with { CandidatePaddingCells = value });
    }
    /// <summary>
    /// Gets or sets the number of retained candidates per CAD pad used for mapping.
    /// </summary>
    public decimal MappingCandidateNumber
    {
        get => _indexMappingTuning.CandidateNumber;
        set => SetIndexMappingTuning(_indexMappingTuning with { CandidateNumber = value });
    }
    private void SetIndexMappingTuning(IndexMappingTuning next, [CallerMemberName] string? propertyName = null)
    {
        if (_indexMappingTuning == next)
        {
            return;
        }

        OnPropertyChanging(propertyName);
        _indexMappingTuning = next;
        OnPropertyChanged(propertyName);
    }
    /// <summary>
    /// Gets or sets the selected CAD auto CAD Output FW Diff assignment mode option.
    /// </summary>
    public CadOutputFwDiffAutoModeOption SelectedCadOutputFwDiffAutoModeOption
    {
        get => CadOutputFwDiffAutoModeOptions.FirstOrDefault(option => option.Value == CadOutputFwDiffAutoMode);
        set
        {
            if (_settingsSuppression.IsCadOutputFwDiffAutoModeSelectionSyncActive || value.Value == CadOutputFwDiffAutoMode)
            {
                return;
            }

            using (_settingsSuppression.EnterCadOutputFwDiffAutoModeSelectionSync())
            {
                CadOutputFwDiffAutoMode = value.Value;
            }

            if (!_isLoadingSettings)
            {
                ApplySettingsOrchestration(nameof(CadOutputFwDiffAutoMode));
            }

            OnPropertyChanged(nameof(SelectedCadOutputFwDiffAutoModeOption));
        }
    }
    /// <summary>
    /// Gets or sets the CAD output ids that currently use manual CAD Output FW Diff override.
    /// </summary>
    [ObservableProperty] private ObservableCollection<int> _cadOutputFwDiffIndexOverrideCadIds = new();
    /// <summary>
    /// Gets or sets CAD output ids currently used as per-IC auto-numbering anchors.
    /// </summary>
    [ObservableProperty] private ObservableCollection<int> _cadOutputFwDiffIndexAnchorCadIdsForCanvas = new();

    // Sizing scopes for manual adjustments.

    /// <summary>
    /// Partial method invoked when <see cref="UseLocalSizing"/> property changes.
    /// Synchronizes <see cref="IsWidthRowLocal"/> and <see cref="IsHeightColumnLocal"/>
    /// and triggers a grid rebuild.
    /// </summary>
    /// <param name="value">The new value of <see cref="UseLocalSizing"/>.</param>
    partial void OnUseLocalSizingChanged(bool value)
    {
        if (_settingsSuppression.IsScopeSyncActive) return;
        using (_settingsSuppression.EnterScopeSync())
        {
            IsWidthRowLocal = value;
            IsHeightColumnLocal = value;
        }
        _ = TriggerGridRebuildAsync(requestFit: false);
    }

    /// <summary>
    /// Partial method invoked when <see cref="IsWidthRowLocal"/> property changes.
    /// Synchronizes <see cref="UseLocalSizing"/> and triggers a grid rebuild.
    /// </summary>
    /// <param name="value">The new value of <see cref="IsWidthRowLocal"/>.</param>
    partial void OnIsWidthRowLocalChanged(bool value)
    {
        if (_settingsSuppression.IsScopeSyncActive) return;
        using (_settingsSuppression.EnterScopeSync())
        {
            UseLocalSizing = value && IsHeightColumnLocal;
        }
        _ = TriggerGridRebuildAsync(requestFit: false);
    }

    /// <summary>
    /// Partial method invoked when <see cref="IsHeightColumnLocal"/> property changes.
    /// Synchronizes <see cref="UseLocalSizing"/> and triggers a grid rebuild.
    /// </summary>
    /// <param name="value">The new value of <see cref="IsHeightColumnLocal"/>.</param>
    partial void OnIsHeightColumnLocalChanged(bool value)
    {
        if (_settingsSuppression.IsScopeSyncActive) return;
        using (_settingsSuppression.EnterScopeSync())
        {
            UseLocalSizing = IsWidthRowLocal && value;
        }
        _ = TriggerGridRebuildAsync(requestFit: false);
    }

    partial void OnGlobalFontSizePercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, GlobalFontSizePercentMin, GlobalFontSizePercentMax);
        if (clamped != value)
        {
            using (_undoSuppression.Enter())
            {
                GlobalFontSizePercent = clamped;
            }
            return;
        }

        OnPropertyChanged(nameof(GlobalFontScaleFactor));
        OnPropertyChanged(nameof(GlobalFontBaseSize));
    }

    private void OnIsNotchExportingChanged(bool _)
    {
        OnPropertyChanged(nameof(ShouldShowNotchExportProgress));
    }

    private void OnNotchExportProgressChanged(double _)
    {
        OnPropertyChanged(nameof(ShouldShowNotchExportProgress));
    }
}
