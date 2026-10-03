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

    partial void OnSelectedScanOrderOptionChanged(ScanOrderOption value)
    {
        if (_isLoadingSettings || _suppressScanOrderSelectionSync) return;

        _suppressScanOrderSelectionSync = true;
        try
        {
            if (SelectedScanOrder != value.Value)
            {
                SelectedScanOrder = value.Value; // Update underlying enum property.
            }
        }
        finally
        {
            _suppressScanOrderSelectionSync = false;
        }

        ApplySettingsOrchestration(nameof(SelectedScanOrder));
    }

    partial void OnSelectedScanOrderChanged(ScanOrder value)
    {
        if (_isLoadingSettings || _suppressScanOrderSelectionSync) return;

        _suppressScanOrderSelectionSync = true;
        try
        {
            var match = ScanOrderOptions.FirstOrDefault(o => o.Value == value);
            if (!match.Equals(SelectedScanOrderOption)) // Update display option if it doesn't match.
            {
                SelectedScanOrderOption = match;
            }
        }
        finally
        {
            _suppressScanOrderSelectionSync = false;
        }

        ApplySettingsOrchestration(nameof(SelectedScanOrder));
    }

    partial void OnSelectedGridAlignmentOptionChanged(GridAlignmentOption value)
    {
        if (_isLoadingSettings || _suppressGridAlignmentSelectionSync) return;

        _suppressGridAlignmentSelectionSync = true;
        try
        {
            if (GridAlignmentMode != value.Value)
            {
                GridAlignmentMode = value.Value; // Update underlying enum property.
            }
        }
        finally
        {
            _suppressGridAlignmentSelectionSync = false;
        }

        ApplySettingsOrchestration(nameof(GridAlignmentMode));
    }

    partial void OnRegularSourceModeChanged(RegularSourceMode value)
    {
        if (_isLoadingSettings || _suppressRegularSourceModeSelectionSync) return;

        _suppressRegularSourceModeSelectionSync = true;
        try
        {
            var match = RegularSourceModeOptions.FirstOrDefault(o => o.Value == value);
            if (!match.Equals(SelectedRegularSourceModeOption))
            {
                SelectedRegularSourceModeOption = match;
            }
        }
        finally
        {
            _suppressRegularSourceModeSelectionSync = false;
        }

        ApplySettingsOrchestration(nameof(RegularSourceMode));
    }

    partial void OnSelectedRegularSourceModeOptionChanged(RegularSourceModeOption value)
    {
        if (_isLoadingSettings || _suppressRegularSourceModeSelectionSync) return;

        _suppressRegularSourceModeSelectionSync = true;
        try
        {
            if (RegularSourceMode != value.Value)
            {
                RegularSourceMode = value.Value;
            }
        }
        finally
        {
            _suppressRegularSourceModeSelectionSync = false;
        }

        ApplySettingsOrchestration(nameof(RegularSourceMode));
    }

    partial void OnGridAlignmentModeChanged(GridAlignmentMode value)
    {
        if (_isLoadingSettings || _suppressGridAlignmentSelectionSync) return;

        _suppressGridAlignmentSelectionSync = true;
        try
        {
            var match = GridAlignmentOptions.FirstOrDefault(o => o.Value == value);
            if (!match.Equals(SelectedGridAlignmentOption)) // Update display option if it doesn't match.
            {
                SelectedGridAlignmentOption = match;
            }
        }
        finally
        {
            _suppressGridAlignmentSelectionSync = false;
        }

        ApplySettingsOrchestration(nameof(GridAlignmentMode));
    }

    partial void OnSelectedBoundLayerOptionChanged(BoundLayerOption value)
    {
        if (_suppressBoundLayerSelectionChange)
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
        if (_suppressRegularSourceLayerSelectionChange)
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
        if (_suppressCascadeRebuild)
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
        if (_suppressCascadeRebuild)
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
            _suppressUndo = true;
            CoordinatePixelWidth = clamped;
            _suppressUndo = false;
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
            _suppressUndo = true;
            CoordinatePixelHeight = clamped;
            _suppressUndo = false;
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
            _suppressUndo = true;
            CoordinatePreferredAaOutlineLayerName = normalized;
            _suppressUndo = false;
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
}
