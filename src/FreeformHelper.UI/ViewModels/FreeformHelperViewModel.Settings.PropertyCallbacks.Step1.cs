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
}
