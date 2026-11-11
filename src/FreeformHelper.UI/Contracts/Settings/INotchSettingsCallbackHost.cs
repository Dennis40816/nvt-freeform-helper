// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Contracts.Settings;

/// <summary>Original Notch scopes, playback history and actions; the adapter stores no preview state.</summary>
internal interface INotchSettingsCallbackHost
{
    bool IsLoadingSettings { get; }
    bool IsNotchThresholdSyncActive { get; }
    bool EnableToRegular { get; set; }
    bool EnableToFull { get; set; }
    decimal ToFullStrictOverlapPercent { get; set; }
    decimal NotchThresholdQ7 { get; set; }
    decimal NotchThresholdPercent { get; set; }
    bool LinkNotchThresholds { get; }
    decimal NotchPreviewVisualizationStep { get; set; }
    decimal NotchPreviewAutoPlayIntervalMs { get; set; }
    bool NotchPreviewAutoPlayEnabled { get; }
    bool IsNotchCanvasPreviewVisible { get; }
    bool LastPreviewVisibleForPlayback { get; set; }
    IDisposable EnterSettingsLoading();
    IDisposable EnterUndoSuppression();
    IDisposable EnterNotchThresholdSync();
    void NotifyPropertyChanged(string propertyName);
    void NotifyNotchModelSummaryChanged();
    void ApplySettingsOrchestration(string propertyName);
    void InvalidateCanvas();
    void RefreshNotchCanvasPreview(bool showStatus);
    void AdvanceNotchPreviewStageOnce();
    void StartNotchPreviewAutoPlayLoop();
    void StopNotchPreviewAutoPlayLoop();
    void RestartNotchPreviewAutoPlayLoop();
}
