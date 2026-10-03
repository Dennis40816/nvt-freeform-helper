using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CadPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    /// <summary>
    /// Partial method invoked when <see cref="CustomValue" /> property changes.
    /// Tracks whether the custom value has diverged from its initial state.
    /// </summary>
    /// <param name="value">The new value of <see cref="CustomValue" />.</param>
    partial void OnCustomValueChanged(decimal value)
    {
        if (_isLoading)
        {
            return; // Ignore changes during initial loading.
        }

        _customValueDirty = value != _customValueInitial; // Mark as dirty if value changed.
        OnPropertyChanged(nameof(HasPendingChanges)); // Notify UI about pending changes.
    }

    partial void OnHasCadOutputFwDiffOverrideChanged(bool value)
    {
        OnPropertyChanged(nameof(CanClearCadOutputFwDiffOverride));
        OnPropertyChanged(nameof(CadOutputFwDiffAssignmentModeText));
    }

    partial void OnIsDxfIndexAnchorChanged(bool value)
    {
        OnPropertyChanged(nameof(CanClearDxfIndexAnchor));
        OnPropertyChanged(nameof(CadOutputFwDiffAssignmentModeText));
    }

    partial void OnShowNotchDiagnosticsChanged(bool value)
    {
        OnPropertyChanged(nameof(NotchDiagnosticsToggleText));
        OnPropertyChanged(nameof(ShowLowFrequencyDetailSection));
    }

    partial void OnShowRuleTraceChanged(bool value)
    {
        OnPropertyChanged(nameof(RuleTraceToggleText));
        OnPropertyChanged(nameof(ShowLowFrequencyDetailSection));
    }

    partial void OnShowGeometryDetailsChanged(bool value)
    {
        OnPropertyChanged(nameof(GeometryDetailsToggleText));
        OnPropertyChanged(nameof(ShowLowFrequencyDetailSection));
    }

    private void ToggleLowFrequencyDetailSection()
    {
        var shouldExpand = !ShowLowFrequencyDetailSection;
        ShowNotchDiagnostics = shouldExpand && HasToFullDiagnosticsText;
        ShowRuleTrace = shouldExpand && HasRuleTrace;
        ShowGeometryDetails = shouldExpand;
    }
}

