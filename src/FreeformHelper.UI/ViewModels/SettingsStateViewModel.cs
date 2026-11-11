// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using BoundLayerOption = FreeformHelper.UI.ViewModels.FreeformHelperViewModel.BoundLayerOption;
using RegularSourceLayerOption = FreeformHelper.UI.ViewModels.FreeformHelperViewModel.RegularSourceLayerOption;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the grid and layer settings; the shell remains the synchronous notification source.</summary>
internal sealed partial class SettingsStateViewModel
{
    private readonly ISettingsHost _host;

    // UI thread only after construction. Moved from the shell, with the same record defaults.
    private GridBuildOptions _gridBuild;
    private LayerSourceSelection _layerSource;

    internal SettingsStateViewModel(ISettingsHost host)
    {
        _host = host;
        _gridBuild = GridBuildOptions.Initial;
        _layerSource = LayerSourceSelection.Initial;
    }

    /// <summary>
    /// A lazy-initialized HashSet containing the names of properties that, when changed,
    /// should mark the project as having unsaved changes.
    /// </summary>
    private static readonly Lazy<HashSet<string>> _dirtySettingNames = new(() => new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(FreeformHelperViewModel.ShowCad),
        nameof(FreeformHelperViewModel.ShowRegular),
        nameof(FreeformHelperViewModel.ColorCadByArea),
        nameof(FreeformHelperViewModel.ShowNotchCanvasPreview),
        nameof(FreeformHelperViewModel.ShowNotchToRegularLabels),
        nameof(FreeformHelperViewModel.XChannels),
        nameof(FreeformHelperViewModel.YChannels),
        nameof(FreeformHelperViewModel.CascadeNum),
        nameof(FreeformHelperViewModel.CoordinatePixelWidth),
        nameof(FreeformHelperViewModel.CoordinatePixelHeight),
        nameof(FreeformHelperViewModel.CoordinatePreferredAaOutlineLayerName),
        nameof(FreeformHelperViewModel.SelectedScanOrder),
        nameof(FreeformHelperViewModel.SelectedScanOrderOption), // UI proxy; changes affect the underlying setting.
        nameof(FreeformHelperViewModel.GridAlignmentMode),
        nameof(FreeformHelperViewModel.SelectedGridAlignmentOption), // UI proxy
        nameof(FreeformHelperViewModel.RegularSourceMode),
        nameof(FreeformHelperViewModel.SelectedRegularSourceModeOption), // UI proxy
        nameof(FreeformHelperViewModel.GridPaddingPercent),
        nameof(FreeformHelperViewModel.ActiveAreaWidth),
        nameof(FreeformHelperViewModel.ActiveAreaHeight),
        nameof(FreeformHelperViewModel.ActiveAreaPixelsX),
        nameof(FreeformHelperViewModel.ActiveAreaPixelsY),
        nameof(FreeformHelperViewModel.PanelBiasX),
        nameof(FreeformHelperViewModel.PanelBiasY),
        nameof(FreeformHelperViewModel.MatchThreshold),
        nameof(FreeformHelperViewModel.FreeformAxisThreshold),
        nameof(FreeformHelperViewModel.EnableAutoDetectXy),
        nameof(FreeformHelperViewModel.EnableFreeformEdgeSpecialization),
        nameof(FreeformHelperViewModel.AutoReplayStep2AfterProjectLoad),
        nameof(FreeformHelperViewModel.CadFillOpacity),
        nameof(FreeformHelperViewModel.CadLineOpacity),
        nameof(FreeformHelperViewModel.RegularFillOpacity),
        nameof(FreeformHelperViewModel.RegularLineOpacity),
        nameof(FreeformHelperViewModel.AreaBucketTolerance),
        nameof(FreeformHelperViewModel.MaxAreaBuckets),
        nameof(FreeformHelperViewModel.GlobalFontSizePercent),
        nameof(FreeformHelperViewModel.UseLocalSizing),
        nameof(FreeformHelperViewModel.IsWidthRowLocal),
        nameof(FreeformHelperViewModel.IsHeightColumnLocal),
        nameof(FreeformHelperViewModel.ImportOnlyClosedPolylines),
        nameof(FreeformHelperViewModel.ImportBlockPolylines),
        nameof(FreeformHelperViewModel.MappingWeightIou),
        nameof(FreeformHelperViewModel.MappingWeightCentroidDistance),
        nameof(FreeformHelperViewModel.MappingWeightAreaRatio),
        nameof(FreeformHelperViewModel.MappingLowConfidenceThreshold),
        nameof(FreeformHelperViewModel.MappingAmbiguousMargin),
        nameof(FreeformHelperViewModel.MappingCandidateNumber),
        nameof(FreeformHelperViewModel.MappingCandidatePaddingCells),
        nameof(FreeformHelperViewModel.CadOutputFwDiffIndexStart),
        nameof(FreeformHelperViewModel.CadOutputFwDiffAutoMode),
        nameof(FreeformHelperViewModel.SelectedCadOutputFwDiffAutoModeOption),
        nameof(FreeformHelperViewModel.CadOutputFwDiffIndexAnchorCadId),
        nameof(FreeformHelperViewModel.EnableV21),
        nameof(FreeformHelperViewModel.EnableV22),
        nameof(FreeformHelperViewModel.LenScale),
        nameof(FreeformHelperViewModel.NullValue),
        nameof(FreeformHelperViewModel.NotchThresholdQ7),
        nameof(FreeformHelperViewModel.NotchThresholdPercent),
        nameof(FreeformHelperViewModel.LinkNotchThresholds),
        nameof(FreeformHelperViewModel.EnableToRegular),
        nameof(FreeformHelperViewModel.EnableToFull),
        nameof(FreeformHelperViewModel.SelectedNotchCompensationModelOption),
        nameof(FreeformHelperViewModel.ToFullStrictOverlapPercent),
        nameof(FreeformHelperViewModel.EnableToFullRuleEngine),
        nameof(FreeformHelperViewModel.EnableToFullRuleTrace),
        nameof(FreeformHelperViewModel.EnableBoundaryVirtualAreaCap),
        nameof(FreeformHelperViewModel.BoundaryVirtualAreaCapPercent),
        nameof(FreeformHelperViewModel.EnableTargetCoverageGuard),
        nameof(FreeformHelperViewModel.TargetCoverageCapPercent),
        nameof(FreeformHelperViewModel.SelectedNotchExportProfileOption),
        nameof(FreeformHelperViewModel.HighlightStrokeWidthAdjust),
        nameof(FreeformHelperViewModel.NotchPreviewAutoPlayEnabled),
        nameof(FreeformHelperViewModel.NotchPreviewAutoPlayIntervalMs)
    });

    internal static bool IsDirtySetting(string propertyName) => _dirtySettingNames.Value.Contains(propertyName);

    internal static decimal NormalizeLineOpacity(double value)
    {
        // Historical snapshots could deserialize missing line opacity as 0.
        // Treat non-positive values as default-visible (100%).
        if (value <= 0.0)
        {
            return 1.0m;
        }

        return (decimal)Math.Clamp(value, 0.0, 1.0);
    }

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

            _host.NotifyPropertyChanging(nameof(CoordinatePreferredAaOutlineLayerName));
            _gridBuild = _gridBuild with { CoordinatePreferredAaOutlineLayerName = value };
            _host.OnCoordinatePreferredAaOutlineLayerNameChanged(value);
            _host.NotifyPropertyChanged(nameof(CoordinatePreferredAaOutlineLayerName));
        }
    }

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

            _host.OnMaxAreaBucketsChanging(value);
            _host.NotifyPropertyChanging(nameof(MaxAreaBuckets));
            _gridBuild = _gridBuild with { MaxAreaBuckets = value };
            _host.NotifyPropertyChanged(nameof(MaxAreaBuckets));
        }
    }

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

            _host.OnAreaBucketToleranceChanging(value);
            _host.NotifyPropertyChanging(nameof(AreaBucketTolerance));
            _gridBuild = _gridBuild with { AreaBucketTolerance = value };
            _host.NotifyPropertyChanged(nameof(AreaBucketTolerance));
        }
    }

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

            _host.NotifyPropertyChanging(nameof(IsHeightColumnLocal));
            _gridBuild = _gridBuild with { IsHeightColumnLocal = value };
            _host.OnIsHeightColumnLocalChanged(value);
            _host.NotifyPropertyChanged(nameof(IsHeightColumnLocal));
        }
    }

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

            _host.NotifyPropertyChanging(nameof(IsWidthRowLocal));
            _gridBuild = _gridBuild with { IsWidthRowLocal = value };
            _host.OnIsWidthRowLocalChanged(value);
            _host.NotifyPropertyChanged(nameof(IsWidthRowLocal));
        }
    }

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

            _host.NotifyPropertyChanging(nameof(UseLocalSizing));
            _gridBuild = _gridBuild with { UseLocalSizing = value };
            _host.OnUseLocalSizingChanged(value);
            _host.NotifyPropertyChanged(nameof(UseLocalSizing));
        }
    }

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

            _host.NotifyPropertyChanging(nameof(RegularSourceMode));
            _gridBuild = _gridBuild with { RegularSourceMode = value };
            _host.OnRegularSourceModeChanged(value);
            _host.NotifyPropertyChanged(nameof(RegularSourceMode));
        }
    }

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

            _host.OnGridAlignmentModeChanging(value);
            _host.NotifyPropertyChanging(nameof(GridAlignmentMode));
            _gridBuild = _gridBuild with { GridAlignmentMode = value };
            _host.OnGridAlignmentModeChanged(value);
            _host.NotifyPropertyChanged(nameof(GridAlignmentMode));
        }
    }

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

            _host.OnSelectedScanOrderChanging(value);
            _host.NotifyPropertyChanging(nameof(SelectedScanOrder));
            _gridBuild = _gridBuild with { SelectedScanOrder = value };
            _host.OnSelectedScanOrderChanged(value);
            _host.NotifyPropertyChanged(nameof(SelectedScanOrder));
        }
    }

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

            _host.NotifyPropertyChanging(nameof(DxfRegularSourceHint));
            _layerSource = _layerSource with { DxfRegularSourceHint = value };
            _host.NotifyPropertyChanged(nameof(DxfRegularSourceHint));
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

            _host.NotifyPropertyChanging(nameof(SelectedRegularSourceLayerOption));
            _layerSource = _layerSource with { SelectedRegularSourceLayerOption = value };
            _host.OnSelectedRegularSourceLayerOptionChanged();
            _host.NotifyPropertyChanged(nameof(SelectedRegularSourceLayerOption));
        }
    }

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

            _host.NotifyPropertyChanging(nameof(BoundLayerOptionSummary));
            _layerSource = _layerSource with { BoundLayerOptionSummary = value };
            _host.NotifyPropertyChanged(nameof(BoundLayerOptionSummary));
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

            _host.NotifyPropertyChanging(nameof(SelectedBoundLayerOption));
            _layerSource = _layerSource with { SelectedBoundLayerOption = value };
            _host.OnSelectedBoundLayerOptionChanged();
            _host.NotifyPropertyChanged(nameof(SelectedBoundLayerOption));
        }
    }

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

            _host.NotifyPropertyChanging(nameof(HasCadLayers));
            _layerSource = _layerSource with { HasCadLayers = value };
            _host.NotifyPropertyChanged(nameof(HasCadLayers));
        }
    }
}
