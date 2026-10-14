using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnActiveAreaWidthChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        OnPropertyChanged(nameof(RegularSensorArea)); // Update dependent property.
        OnPropertyChanged(nameof(PitchSizeX)); // Update dependent property.
        OnPropertyChanged(nameof(PitchSizeXSummary)); // Update dependent property.
        ApplySettingsOrchestration(nameof(ActiveAreaWidth));
    }

    partial void OnActiveAreaHeightChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        OnPropertyChanged(nameof(RegularSensorArea)); // Update dependent property.
        OnPropertyChanged(nameof(PitchSizeY)); // Update dependent property.
        OnPropertyChanged(nameof(PitchSizeYSummary)); // Update dependent property.
        ApplySettingsOrchestration(nameof(ActiveAreaHeight));
    }

    partial void OnPanelBiasXChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(PanelBiasX));
    }

    partial void OnPanelBiasYChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(PanelBiasY));
    }

    partial void OnSelectedScanOrderChanged(ScanOrder value)
    {
        if (_settingsSuppression.IsScanOrderSelectionSyncActive) return;

        using (_settingsSuppression.EnterScanOrderSelectionSync())
        {
            OnPropertyChanged(nameof(SelectedScanOrderOption));
        }

        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(SelectedScanOrder));
    }

    partial void OnRegularSourceModeChanged(RegularSourceMode value)
    {
        if (_settingsSuppression.IsRegularSourceModeSelectionSyncActive) return;

        using (_settingsSuppression.EnterRegularSourceModeSelectionSync())
        {
            OnPropertyChanged(nameof(SelectedRegularSourceModeOption));
        }

        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(RegularSourceMode));
    }

    partial void OnGridAlignmentModeChanged(GridAlignmentMode value)
    {
        if (_settingsSuppression.IsGridAlignmentSelectionSyncActive) return;

        using (_settingsSuppression.EnterGridAlignmentSelectionSync())
        {
            OnPropertyChanged(nameof(SelectedGridAlignmentOption));
        }

        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(GridAlignmentMode));
    }

    partial void OnSelectedBoundLayerOptionChanged(BoundLayerOption value)
    {
        if (_settingsSuppression.IsBoundLayerSelectionChangeActive)
        {
            return;
        }

        if (GridAlignmentMode == GridAlignmentMode.FromCadBounds)
        {
            _ = TriggerGridRebuildAsync(requestFit: true);
        }
    }

    partial void OnSelectedRegularSourceLayerOptionChanged(RegularSourceLayerOption value)
    {
        if (_settingsSuppression.IsRegularSourceLayerSelectionChangeActive)
        {
            return;
        }

        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(SelectedRegularSourceLayerOption));
    }

    partial void OnXChannelsChanged(decimal value)
    {
        OnPropertyChanged(nameof(PitchSizeX)); // Update dependent property.
        OnPropertyChanged(nameof(PitchSizeXSummary)); // Update dependent property.
        if (_isLoadingSettings) return;
        if (_settingsSuppression.IsCascadeRebuildActive)
        {
            ApplySettingsChangeEffects(
                SettingsChangeEffects.RefreshDxfRegularSourceHint |
                SettingsChangeEffects.TriggerGridRebuildWithFit);
            return;
        }

        ApplySettingsOrchestration(nameof(XChannels));
    }

    partial void OnYChannelsChanged(decimal value)
    {
        OnPropertyChanged(nameof(PitchSizeY)); // Update dependent property.
        OnPropertyChanged(nameof(PitchSizeYSummary)); // Update dependent property.
        if (_isLoadingSettings) return;
        if (_settingsSuppression.IsCascadeRebuildActive)
        {
            ApplySettingsChangeEffects(
                SettingsChangeEffects.RefreshDxfRegularSourceHint |
                SettingsChangeEffects.TriggerGridRebuildWithFit);
            return;
        }

        ApplySettingsOrchestration(nameof(YChannels));
    }

    partial void OnCoordinatePixelWidthChanged(decimal value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        var clamped = Math.Max(1m, Math.Round(value, MidpointRounding.AwayFromZero));
        if (clamped != value)
        {
            using (_undoSuppression.Enter())
            {
                CoordinatePixelWidth = clamped;
            }
        }
    }

    partial void OnCoordinatePixelHeightChanged(decimal value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        var clamped = Math.Max(1m, Math.Round(value, MidpointRounding.AwayFromZero));
        if (clamped != value)
        {
            using (_undoSuppression.Enter())
            {
                CoordinatePixelHeight = clamped;
            }
        }
    }

    partial void OnCoordinatePreferredAaOutlineLayerNameChanged(string value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        var normalized = value?.Trim() ?? string.Empty;
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            using (_undoSuppression.Enter())
            {
                CoordinatePreferredAaOutlineLayerName = normalized;
            }
        }
    }

    partial void OnCascadeNumChanged(decimal value)
    {
        if (_isLoadingSettings)
        {
            OnPropertyChanged(nameof(IsCascadeMulti)); // Update dependent property even if loading.
            return;
        }
        OnPropertyChanged(nameof(IsCascadeMulti)); // Update dependent property.
        ApplySettingsOrchestration(nameof(CascadeNum));
    }

    partial void OnGridPaddingPercentChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(GridPaddingPercent));
    }

    // The grid build options are stored once. The properties below forward to the record and raise
    // the same notifications and callbacks as the stored properties they replaced.
    private GridBuildOptions _gridBuild = GridBuildOptions.Initial;

    /// <summary>
    /// Gets or sets the preferred AA outline layer name used by Coordinate.
    /// </summary>
    public string CoordinatePreferredAaOutlineLayerName
    {
        get => _gridBuild.CoordinatePreferredAaOutlineLayerName;
        set
        {
            if (_gridBuild.CoordinatePreferredAaOutlineLayerName == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridBuild = _gridBuild with { CoordinatePreferredAaOutlineLayerName = value };
            OnCoordinatePreferredAaOutlineLayerNameChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnCoordinatePreferredAaOutlineLayerNameChanged(string value);

    /// <summary>
    /// Gets or sets the maximum number of area buckets to use for coloring pads.
    /// </summary>
    public decimal MaxAreaBuckets
    {
        get => _gridBuild.MaxAreaBuckets;
        set
        {
            if (_gridBuild.MaxAreaBuckets == value)
            {
                return;
            }

            OnMaxAreaBucketsChanging(value);
            OnPropertyChanging();
            _gridBuild = _gridBuild with { MaxAreaBuckets = value };
            OnPropertyChanged();
        }
    }

    partial void OnMaxAreaBucketsChanging(decimal value);

    /// <summary>
    /// Gets or sets the area bucket tolerance for coloring pads by area.
    /// </summary>
    public decimal AreaBucketTolerance
    {
        get => _gridBuild.AreaBucketTolerance;
        set
        {
            if (_gridBuild.AreaBucketTolerance == value)
            {
                return;
            }

            OnAreaBucketToleranceChanging(value);
            OnPropertyChanging();
            _gridBuild = _gridBuild with { AreaBucketTolerance = value };
            OnPropertyChanged();
        }
    }

    partial void OnAreaBucketToleranceChanging(decimal value);

    /// <summary>
    /// Gets or sets a value indicating whether height adjustment is column-local.
    /// </summary>
    public bool IsHeightColumnLocal
    {
        get => _gridBuild.IsHeightColumnLocal;
        set
        {
            if (_gridBuild.IsHeightColumnLocal == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridBuild = _gridBuild with { IsHeightColumnLocal = value };
            OnIsHeightColumnLocalChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnIsHeightColumnLocalChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether width adjustment is row-local.
    /// </summary>
    public bool IsWidthRowLocal
    {
        get => _gridBuild.IsWidthRowLocal;
        set
        {
            if (_gridBuild.IsWidthRowLocal == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridBuild = _gridBuild with { IsWidthRowLocal = value };
            OnIsWidthRowLocalChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnIsWidthRowLocalChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether local sizing (row-local width, column-local height) is enabled.
    /// </summary>
    public bool UseLocalSizing
    {
        get => _gridBuild.UseLocalSizing;
        set
        {
            if (_gridBuild.UseLocalSizing == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridBuild = _gridBuild with { UseLocalSizing = value };
            OnUseLocalSizingChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnUseLocalSizingChanged(bool value);

    /// <summary>
    /// Gets or sets the currently selected <see cref="RegularSourceMode"/> enumeration value.
    /// </summary>
    public RegularSourceMode RegularSourceMode
    {
        get => _gridBuild.RegularSourceMode;
        set
        {
            if (_gridBuild.RegularSourceMode == value)
            {
                return;
            }

            OnPropertyChanging();
            _gridBuild = _gridBuild with { RegularSourceMode = value };
            OnRegularSourceModeChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnRegularSourceModeChanged(RegularSourceMode value);

    /// <summary>
    /// Gets or sets the currently selected <see cref="GridAlignmentMode"/> enumeration value.
    /// </summary>
    public GridAlignmentMode GridAlignmentMode
    {
        get => _gridBuild.GridAlignmentMode;
        set
        {
            if (_gridBuild.GridAlignmentMode == value)
            {
                return;
            }

            OnGridAlignmentModeChanging(value);
            OnPropertyChanging();
            _gridBuild = _gridBuild with { GridAlignmentMode = value };
            OnGridAlignmentModeChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnGridAlignmentModeChanging(GridAlignmentMode value);

    partial void OnGridAlignmentModeChanged(GridAlignmentMode value);

    /// <summary>
    /// Gets or sets the currently selected <see cref="ScanOrder"/> enumeration value.
    /// </summary>
    public ScanOrder SelectedScanOrder
    {
        get => _gridBuild.SelectedScanOrder;
        set
        {
            if (_gridBuild.SelectedScanOrder == value)
            {
                return;
            }

            OnSelectedScanOrderChanging(value);
            OnPropertyChanging();
            _gridBuild = _gridBuild with { SelectedScanOrder = value };
            OnSelectedScanOrderChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnSelectedScanOrderChanging(ScanOrder value);

    partial void OnSelectedScanOrderChanged(ScanOrder value);

    // The layer source selection is stored once. The properties below forward to the record and raise
    // the same notifications and callbacks as the stored properties they replaced.
    private LayerSourceSelection _layerSource = LayerSourceSelection.Initial;

    /// <summary>
    /// Gets or sets helper text for DXF regular-source validation and suggested IC/grid settings.
    /// </summary>
    public string DxfRegularSourceHint
    {
        get => _layerSource.DxfRegularSourceHint;
        set
        {
            if (_layerSource.DxfRegularSourceHint == value)
            {
                return;
            }

            OnPropertyChanging();
            _layerSource = _layerSource with { DxfRegularSourceHint = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the currently selected layer option for DXF regular source mode.
    /// </summary>
    public RegularSourceLayerOption SelectedRegularSourceLayerOption
    {
        get => _layerSource.SelectedRegularSourceLayerOption;
        set
        {
            if (_layerSource.SelectedRegularSourceLayerOption == value)
            {
                return;
            }

            OnPropertyChanging();
            _layerSource = _layerSource with { SelectedRegularSourceLayerOption = value };
            OnSelectedRegularSourceLayerOptionChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnSelectedRegularSourceLayerOptionChanged(RegularSourceLayerOption value);

    /// <summary>
    /// Gets or sets the helper text shown under bounds layer selector,
    /// including truncation hint when available layers exceed dropdown cap.
    /// </summary>
    public string BoundLayerOptionSummary
    {
        get => _layerSource.BoundLayerOptionSummary;
        set
        {
            if (_layerSource.BoundLayerOptionSummary == value)
            {
                return;
            }

            OnPropertyChanging();
            _layerSource = _layerSource with { BoundLayerOptionSummary = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the currently selected layer option for bounding the grid.
    /// </summary>
    public BoundLayerOption SelectedBoundLayerOption
    {
        get => _layerSource.SelectedBoundLayerOption;
        set
        {
            if (_layerSource.SelectedBoundLayerOption == value)
            {
                return;
            }

            OnPropertyChanging();
            _layerSource = _layerSource with { SelectedBoundLayerOption = value };
            OnSelectedBoundLayerOptionChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnSelectedBoundLayerOptionChanged(BoundLayerOption value);

    /// <summary>
    /// Gets or sets a value indicating whether any CAD layers are available.
    /// Used to hide the layer section when no DXF is loaded.
    /// </summary>
    public bool HasCadLayers
    {
        get => _layerSource.HasCadLayers;
        set
        {
            if (_layerSource.HasCadLayers == value)
            {
                return;
            }

            OnPropertyChanging();
            _layerSource = _layerSource with { HasCadLayers = value };
            OnPropertyChanged();
        }
    }
}
