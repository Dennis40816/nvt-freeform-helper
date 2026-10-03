namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnEnableToFullChanged(bool value)
    {
        NotifyNotchModelSummaryChanged();
        if (_isLoadingSettings)
        {
            return;
        }

        OnPropertyChanged(nameof(CanShowToFullPreviewToggle));
        ApplySettingsOrchestration(nameof(EnableToFull));
    }

    partial void OnEnableBoundaryVirtualAreaCapChanged(bool value)
    {
        NotifyNotchModelSummaryChanged();
        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(EnableBoundaryVirtualAreaCap));
    }

    partial void OnBoundaryVirtualAreaCapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 1000m);
        if (clamped != value)
        {
            _suppressUndo = true;
            BoundaryVirtualAreaCapPercent = clamped;
            _suppressUndo = false;
            return;
        }

        NotifyNotchModelSummaryChanged();
        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(BoundaryVirtualAreaCapPercent));
    }

    partial void OnEnableTargetCoverageGuardChanged(bool value)
    {
        NotifyNotchModelSummaryChanged();
        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(EnableTargetCoverageGuard));
    }

    partial void OnTargetCoverageCapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 255m);
        if (clamped != value)
        {
            _suppressUndo = true;
            TargetCoverageCapPercent = clamped;
            _suppressUndo = false;
            return;
        }

        NotifyNotchModelSummaryChanged();
        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(TargetCoverageCapPercent));
    }

}
