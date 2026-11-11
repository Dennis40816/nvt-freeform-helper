// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.UI.Contracts.Settings;

namespace FreeformHelper.UI.Services;

/// <summary>Preserves Step 2, Step 3 and export callback ordering without owning their values.</summary>
internal sealed class SettingsStepCallbackAdapter(ISettingsStepCallbackHost host)
{
    private readonly ISettingsStepCallbackHost _host = host;

    internal void OnDetectionChanged(string propertyName)
    {
        if (_host.IsLoadingSettings) return;
        _host.ApplySettingsOrchestration(propertyName);
    }

    internal void OnEnableToFullChanged()
    {
        _host.NotifyNotchModelSummaryChanged();
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.NotifyPropertyChanged("CanShowToFullPreviewToggle");
        _host.ApplySettingsOrchestration("EnableToFull");
    }

    internal void OnEnableBoundaryVirtualAreaCapChanged()
    {
        _host.NotifyNotchModelSummaryChanged();
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration("EnableBoundaryVirtualAreaCap");
    }

    internal void OnBoundaryVirtualAreaCapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 1000m);
        if (clamped != value)
        {
            using (_host.EnterUndoSuppression())
            {
                _host.BoundaryVirtualAreaCapPercent = clamped;
            }
            return;
        }

        _host.NotifyNotchModelSummaryChanged();
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration(nameof(_host.BoundaryVirtualAreaCapPercent));
    }

    internal void OnEnableTargetCoverageGuardChanged()
    {
        _host.NotifyNotchModelSummaryChanged();
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration("EnableTargetCoverageGuard");
    }

    internal void OnTargetCoverageCapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 255m);
        if (clamped != value)
        {
            using (_host.EnterUndoSuppression())
            {
                _host.TargetCoverageCapPercent = clamped;
            }
            return;
        }

        _host.NotifyNotchModelSummaryChanged();
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration(nameof(_host.TargetCoverageCapPercent));
    }

    internal void OnSelectedNotchExportFileTypeOptionChanged()
    {
        _host.NotifyPropertyChanged("NotchExportSafetyPolicySummary");
        _host.NotifyPropertyChanged("NotchExportHandoffFileText");
        _host.NotifyPropertyChanged("NotchExportHandoffVersionText");
    }

    internal void OnSelectedNotchExportProfileOptionChanged(NotchExportProfile value)
    {
        _host.NotifyPropertyChanged("NotchExportHandoffProfileText");

        if (_host.IsLoadingSettings)
        {
            return;
        }

        // Keep settings snapshot in sync for immediate export usage.
        _host.SetNotchExportProfile(value);
    }
}
