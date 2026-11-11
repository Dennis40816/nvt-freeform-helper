// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Local setting transitions and notifications routed to existing workspace owners.</summary>
internal interface ISettingsStepCallbackHost
{
    bool IsLoadingSettings { get; }
    decimal BoundaryVirtualAreaCapPercent { get; set; }
    decimal TargetCoverageCapPercent { get; set; }
    IDisposable EnterUndoSuppression();
    void NotifyPropertyChanged(string propertyName);
    void NotifyNotchModelSummaryChanged();
    void ApplySettingsOrchestration(string changedPropertyName);
    void SetNotchExportProfile(NotchExportProfile profile);
}
