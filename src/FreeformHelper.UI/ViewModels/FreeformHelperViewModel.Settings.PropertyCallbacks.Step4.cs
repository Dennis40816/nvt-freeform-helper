using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnCadOutputFwDiffIndexStartChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        if (value < 0 || value > 1_000_000)
        {
            using (_undoSuppression.Enter())
            {
                CadOutputFwDiffIndexStart = Math.Clamp(value, 0, 1_000_000);
            }
            return;
        }

        ApplySettingsOrchestration(nameof(CadOutputFwDiffIndexStart));
    }

    partial void OnCadOutputFwDiffAutoModeChanged(CadOutputFwDiffAutoMode value)
    {
        if (_settingsSuppression.IsCadOutputFwDiffAutoModeSelectionSyncActive) return;

        using (_settingsSuppression.EnterCadOutputFwDiffAutoModeSelectionSync())
        {
            OnPropertyChanged(nameof(SelectedCadOutputFwDiffAutoModeOption));
        }

        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(CadOutputFwDiffAutoMode));
    }

    partial void OnCadOutputFwDiffIndexAnchorCadIdChanged(decimal value)
    {
        if (_isLoadingSettings || _settingsSuppression.IsDxfIndexAnchorChangeActive) return;
        if (value < -1 || value > 2_000_000_000)
        {
            using (_undoSuppression.Enter())
            {
                CadOutputFwDiffIndexAnchorCadId = Math.Clamp(value, -1, 2_000_000_000);
            }
            return;
        }

        if (!TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue())
        {
            return;
        }

        ApplySettingsOrchestration(nameof(CadOutputFwDiffIndexAnchorCadId));
    }
}
