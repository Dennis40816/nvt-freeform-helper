// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnActiveAreaWidthChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        OnPropertyChanged(nameof(PitchSizeX)); // Update dependent property.
        OnPropertyChanged(nameof(PitchSizeXSummary)); // Update dependent property.
        ApplySettingsOrchestration(nameof(ActiveAreaWidth));
    }

    partial void OnActiveAreaHeightChanged(decimal value)
    {
        if (_isLoadingSettings) return;
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

        var clamped = CoordinatePixelSizePolicy.Normalize(value);
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

        var clamped = CoordinatePixelSizePolicy.Normalize(value);
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
            return;
        }
        ApplySettingsOrchestration(nameof(CascadeNum));
    }

    partial void OnGridPaddingPercentChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(GridPaddingPercent));
    }

    /// <summary>
    /// Gets or sets the preferred AA outline layer name used by Coordinate.
    /// </summary>
    public string CoordinatePreferredAaOutlineLayerName
    {
        get => SettingsState.CoordinatePreferredAaOutlineLayerName;
        set => SettingsState.CoordinatePreferredAaOutlineLayerName = value;
    }

    partial void OnCoordinatePreferredAaOutlineLayerNameChanged(string value);

    /// <summary>
    /// Gets or sets the maximum number of area buckets to use for coloring pads.
    /// </summary>
    public decimal MaxAreaBuckets
    {
        get => SettingsState.MaxAreaBuckets;
        set => SettingsState.MaxAreaBuckets = value;
    }

    partial void OnMaxAreaBucketsChanging(decimal value);

    /// <summary>
    /// Gets or sets the area bucket tolerance for coloring pads by area.
    /// </summary>
    public decimal AreaBucketTolerance
    {
        get => SettingsState.AreaBucketTolerance;
        set => SettingsState.AreaBucketTolerance = value;
    }

    partial void OnAreaBucketToleranceChanging(decimal value);

    /// <summary>
    /// Gets or sets a value indicating whether height adjustment is column-local.
    /// </summary>
    public bool IsHeightColumnLocal
    {
        get => SettingsState.IsHeightColumnLocal;
        set => SettingsState.IsHeightColumnLocal = value;
    }

    partial void OnIsHeightColumnLocalChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether width adjustment is row-local.
    /// </summary>
    public bool IsWidthRowLocal
    {
        get => SettingsState.IsWidthRowLocal;
        set => SettingsState.IsWidthRowLocal = value;
    }

    partial void OnIsWidthRowLocalChanged(bool value);

    /// <summary>
    /// Gets or sets a value indicating whether local sizing (row-local width, column-local height) is enabled.
    /// </summary>
    public bool UseLocalSizing
    {
        get => SettingsState.UseLocalSizing;
        set => SettingsState.UseLocalSizing = value;
    }

    partial void OnUseLocalSizingChanged(bool value);

    /// <summary>
    /// Gets or sets the currently selected <see cref="RegularSourceMode"/> enumeration value.
    /// </summary>
    public RegularSourceMode RegularSourceMode
    {
        get => SettingsState.RegularSourceMode;
        set => SettingsState.RegularSourceMode = value;
    }

    partial void OnRegularSourceModeChanged(RegularSourceMode value);

    /// <summary>
    /// Gets or sets the currently selected <see cref="GridAlignmentMode"/> enumeration value.
    /// </summary>
    public GridAlignmentMode GridAlignmentMode
    {
        get => SettingsState.GridAlignmentMode;
        set => SettingsState.GridAlignmentMode = value;
    }

    partial void OnGridAlignmentModeChanging(GridAlignmentMode value);

    partial void OnGridAlignmentModeChanged(GridAlignmentMode value);

    /// <summary>
    /// Gets or sets the currently selected <see cref="ScanOrder"/> enumeration value.
    /// </summary>
    public ScanOrder SelectedScanOrder
    {
        get => SettingsState.SelectedScanOrder;
        set => SettingsState.SelectedScanOrder = value;
    }

    partial void OnSelectedScanOrderChanging(ScanOrder value);

    partial void OnSelectedScanOrderChanged(ScanOrder value);

    /// <summary>
    /// Gets or sets helper text for DXF regular-source validation and suggested IC/grid settings.
    /// </summary>
    public string DxfRegularSourceHint
    {
        get => SettingsState.DxfRegularSourceHint;
        set => SettingsState.DxfRegularSourceHint = value;
    }

    /// <summary>
    /// Gets or sets the currently selected layer option for DXF regular source mode.
    /// </summary>
    public RegularSourceLayerOption SelectedRegularSourceLayerOption
    {
        get => SettingsState.SelectedRegularSourceLayerOption;
        set => SettingsState.SelectedRegularSourceLayerOption = value;
    }

    partial void OnSelectedRegularSourceLayerOptionChanged(RegularSourceLayerOption value);

    /// <summary>
    /// Gets or sets the helper text shown under bounds layer selector,
    /// including truncation hint when available layers exceed dropdown cap.
    /// </summary>
    public string BoundLayerOptionSummary
    {
        get => SettingsState.BoundLayerOptionSummary;
        set => SettingsState.BoundLayerOptionSummary = value;
    }

    /// <summary>
    /// Gets or sets the currently selected layer option for bounding the grid.
    /// </summary>
    public BoundLayerOption SelectedBoundLayerOption
    {
        get => SettingsState.SelectedBoundLayerOption;
        set => SettingsState.SelectedBoundLayerOption = value;
    }

    partial void OnSelectedBoundLayerOptionChanged(BoundLayerOption value);

    /// <summary>
    /// Gets or sets a value indicating whether any CAD layers are available.
    /// Used to hide the layer section when no DXF is loaded.
    /// </summary>
    public bool HasCadLayers
    {
        get => SettingsState.HasCadLayers;
        set => SettingsState.HasCadLayers = value;
    }

    /// <summary>
    /// Gets or sets the panel pixel count across the active area width. Zero means not set.
    /// The project settings hold the value, so no second copy exists.
    /// </summary>
    public decimal ActiveAreaPixelsX
    {
        get => _projectFile.Settings.ActiveAreaPixels.X ?? 0;
        set
        {
            var next = NormalizeActiveAreaPixels(value);
            if (_projectFile.Settings.ActiveAreaPixels.X == next)
            {
                return;
            }

            var previous = ActiveAreaPixelsX;
            _projectFile.Settings.ActiveAreaPixels.X = next;
            TrackUndo(previous, next ?? 0, RestoreActiveAreaPixelsX, "AA pixels X");
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the panel pixel count across the active area height. Zero means not set.
    /// The project settings hold the value, so no second copy exists.
    /// </summary>
    public decimal ActiveAreaPixelsY
    {
        get => _projectFile.Settings.ActiveAreaPixels.Y ?? 0;
        set
        {
            var next = NormalizeActiveAreaPixels(value);
            if (_projectFile.Settings.ActiveAreaPixels.Y == next)
            {
                return;
            }

            var previous = ActiveAreaPixelsY;
            _projectFile.Settings.ActiveAreaPixels.Y = next;
            TrackUndo(previous, next ?? 0, RestoreActiveAreaPixelsY, "AA pixels Y");
            OnPropertyChanged();
        }
    }

    private void RestoreActiveAreaPixelsX(decimal value) => ActiveAreaPixelsX = value;

    private void RestoreActiveAreaPixelsY(decimal value) => ActiveAreaPixelsY = value;

    internal void ApplyActiveAreaPixelsDraft(ActiveAreaPixelsDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!IsProjectEditingEnabled) return;

        // The settings window outlives a project load. A draft that belongs to a replaced project is dropped.
        if (!ReferenceEquals(draft.ProjectIdentity, _projectFile)) return;

        // Write only what the user edited. The settings window outlives a project load, so an untouched
        // field must not overwrite the counts of the project that replaced the one the window was opened on.
        if (draft.IsPixelsXEdited) ActiveAreaPixelsX = draft.PixelsX;
        if (draft.IsPixelsYEdited) ActiveAreaPixelsY = draft.PixelsY;
    }

    private static int? NormalizeActiveAreaPixels(decimal value)
    {
        return value < 1m
            ? null
            : (int)Math.Min(Math.Round(value), FreeformHelper.Application.Services.ActiveAreaPixelPitch.MaxPixels);
    }

    partial void OnAreaBucketToleranceChanging(decimal value) => TrackUndo(AreaBucketTolerance, value, v => AreaBucketTolerance = v, "Area bucket tolerance");
    partial void OnMaxAreaBucketsChanging(decimal value) => TrackUndo(MaxAreaBuckets, value, v => MaxAreaBuckets = v, "Max area buckets");
    partial void OnSelectedScanOrderChanging(ScanOrder value) => TrackUndo(SelectedScanOrder, value, v => SelectedScanOrder = v, "Scan order");
    partial void OnGridAlignmentModeChanging(GridAlignmentMode value) => TrackUndo(GridAlignmentMode, value, v => GridAlignmentMode = v, "Alignment mode");
}
