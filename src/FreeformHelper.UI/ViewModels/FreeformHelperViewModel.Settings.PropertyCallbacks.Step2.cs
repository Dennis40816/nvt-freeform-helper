namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnFreeformAxisThresholdChanged(decimal value)
    {
        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(FreeformAxisThreshold));
    }

    partial void OnEnableAutoDetectXyChanged(bool value)
    {
        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(EnableAutoDetectXy));
    }

    partial void OnEnableFreeformEdgeSpecializationChanged(bool value)
    {
        if (_isLoadingSettings) return;
        ApplySettingsOrchestration(nameof(EnableFreeformEdgeSpecialization));
    }
}
