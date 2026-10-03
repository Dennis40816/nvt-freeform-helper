using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnCadOutputFwDiffIndexStartChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        if (value < 0 || value > 1_000_000)
        {
            _suppressUndo = true;
            CadOutputFwDiffIndexStart = Math.Clamp(value, 0, 1_000_000);
            _suppressUndo = false;
            return;
        }

        ApplySettingsOrchestration(nameof(CadOutputFwDiffIndexStart));
    }

    partial void OnCadOutputFwDiffAutoModeChanged(CadOutputFwDiffAutoMode value)
    {
        if (_isLoadingSettings || _suppressCadOutputFwDiffAutoModeSelectionSync) return;

        _suppressCadOutputFwDiffAutoModeSelectionSync = true;
        try
        {
            var match = CadOutputFwDiffAutoModeOptions.FirstOrDefault(option => option.Value == value);
            if (!match.Equals(SelectedCadOutputFwDiffAutoModeOption))
            {
                SelectedCadOutputFwDiffAutoModeOption = match;
            }
        }
        finally
        {
            _suppressCadOutputFwDiffAutoModeSelectionSync = false;
        }

        ApplySettingsOrchestration(nameof(CadOutputFwDiffAutoMode));
    }

    partial void OnSelectedCadOutputFwDiffAutoModeOptionChanged(CadOutputFwDiffAutoModeOption value)
    {
        if (_isLoadingSettings || _suppressCadOutputFwDiffAutoModeSelectionSync) return;

        _suppressCadOutputFwDiffAutoModeSelectionSync = true;
        try
        {
            if (CadOutputFwDiffAutoMode != value.Value)
            {
                CadOutputFwDiffAutoMode = value.Value;
            }
        }
        finally
        {
            _suppressCadOutputFwDiffAutoModeSelectionSync = false;
        }

        ApplySettingsOrchestration(nameof(CadOutputFwDiffAutoMode));
    }

    partial void OnCadOutputFwDiffIndexAnchorCadIdChanged(decimal value)
    {
        if (_isLoadingSettings || _suppressDxfIndexAnchorChange) return;
        if (value < -1 || value > 2_000_000_000)
        {
            _suppressUndo = true;
            CadOutputFwDiffIndexAnchorCadId = Math.Clamp(value, -1, 2_000_000_000);
            _suppressUndo = false;
            return;
        }

        if (!TryCommitCadOutputFwDiffIndexAnchorCadIdFromCurrentValue())
        {
            return;
        }

        ApplySettingsOrchestration(nameof(CadOutputFwDiffIndexAnchorCadId));
    }
}
