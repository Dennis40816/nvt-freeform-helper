namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnEnableV21Changed(bool value)
    {
        OnPropertyChanged(nameof(IsLenScaleVisible));
    }

    partial void OnSelectedNotchExportFileTypeOptionChanged(NotchExportFileTypeOption value)
    {
        OnPropertyChanged(nameof(IsNotchCExportType));
        OnPropertyChanged(nameof(NotchExportSafetyPolicySummary));
        OnPropertyChanged(nameof(NotchExportHandoffFileText));
        OnPropertyChanged(nameof(NotchExportHandoffVersionText));
    }

    partial void OnSelectedNotchExportProfileOptionChanged(NotchExportProfileOption value)
    {
        OnPropertyChanged(nameof(NotchExportHandoffProfileText));

        if (_isLoadingSettings)
        {
            return;
        }

        // Keep settings snapshot in sync for immediate export usage.
        _projectFile.Settings.Notch.ExportProfile = value.Value;
    }
}
