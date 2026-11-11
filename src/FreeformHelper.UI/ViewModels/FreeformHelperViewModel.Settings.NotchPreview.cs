// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Contracts.Settings;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel : INotchSettingsCallbackHost
{
    private readonly NotchSettingsCallbackAdapter _notchSettingsCallbacks;

    partial void OnSelectedNotchCompensationModelOptionChanged(NotchCompensationModelOption value) =>
        _notchSettingsCallbacks.OnSelectedNotchCompensationModelOptionChanged(value.Value);

    private void ApplyNotchCompensationModeToLegacySwitches(
        NotchCompensationModel mode, bool? currentGainEnableToRegular = null, bool? currentGainEnableToFull = null) =>
        _notchSettingsCallbacks.ApplyNotchCompensationModeToLegacySwitches(mode, currentGainEnableToRegular, currentGainEnableToFull);

    partial void OnEnableToFullRuleEngineChanged(bool value) => _notchSettingsCallbacks.OnEnableToFullRuleEngineChanged();
    partial void OnEnableToFullRuleTraceChanged(bool value) => _notchSettingsCallbacks.OnEnableToFullRuleTraceChanged();
    partial void OnEnableToRegularChanged(bool value) => _notchSettingsCallbacks.OnEnableToRegularChanged();

    // Controls-typed generated callback stays here for stage 5; the adapter does not consume the collection.
    partial void OnNotchCanvasPreviewItemsChanged(ObservableCollection<NotchCanvasPreviewItem> value) =>
        _notchSettingsCallbacks.OnNotchCanvasPreviewItemsChanged();

    partial void OnToFullStrictOverlapPercentChanged(decimal value) => _notchSettingsCallbacks.OnToFullStrictOverlapPercentChanged(value);

    private void RefreshNotchCanvasPreviewAfterStep3SettingsChanged() =>
        _notchSettingsCallbacks.RefreshNotchCanvasPreviewAfterStep3SettingsChanged(
            _grid is not null && _selectedCadIds.Count > 0 && _workflowData.LatestPadMatch.CadToRegular.Count > 0);

    partial void OnNotchThresholdQ7Changed(decimal value) => _notchSettingsCallbacks.OnNotchThresholdQ7Changed(value);
    partial void OnNotchThresholdPercentChanged(decimal value) => _notchSettingsCallbacks.OnNotchThresholdPercentChanged(value);
    partial void OnLinkNotchThresholdsChanged(bool value) => _notchSettingsCallbacks.OnLinkNotchThresholdsChanged(value);
    partial void OnNotchPreviewVisualizationStepChanged(decimal value) => _notchSettingsCallbacks.OnNotchPreviewVisualizationStepChanged(value);
    partial void OnNotchPreviewAutoPlayEnabledChanged(bool value) => _notchSettingsCallbacks.OnNotchPreviewAutoPlayEnabledChanged(value);
    partial void OnShowNotchCanvasPreviewChanged(bool value) => _notchSettingsCallbacks.OnShowNotchCanvasPreviewChanged();
    partial void OnShowNotchToRegularLabelsChanged(bool value) => _notchSettingsCallbacks.OnShowNotchToRegularLabelsChanged();
    private void NotifyNotchOverlayVisibilityChanged() => _notchSettingsCallbacks.NotifyNotchOverlayVisibilityChanged();
    partial void OnNotchPreviewAutoPlayIntervalMsChanged(decimal value) => _notchSettingsCallbacks.OnNotchPreviewAutoPlayIntervalMsChanged(value);

    bool INotchSettingsCallbackHost.IsLoadingSettings => _isLoadingSettings;
    bool INotchSettingsCallbackHost.IsNotchThresholdSyncActive => _settingsSuppression.IsNotchThresholdSyncActive;
    bool INotchSettingsCallbackHost.LastPreviewVisibleForPlayback
    {
        get => _notchCache.LastPreviewVisibleForPlayback;
        set => _notchCache.LastPreviewVisibleForPlayback = value;
    }
    IDisposable INotchSettingsCallbackHost.EnterSettingsLoading() => _settingsSuppression.EnterSettingsLoading();
    IDisposable INotchSettingsCallbackHost.EnterUndoSuppression() => _undoSuppression.Enter();
    IDisposable INotchSettingsCallbackHost.EnterNotchThresholdSync() => _settingsSuppression.EnterNotchThresholdSync();
    void INotchSettingsCallbackHost.NotifyPropertyChanged(string propertyName) => OnPropertyChanged(propertyName);
    void INotchSettingsCallbackHost.NotifyNotchModelSummaryChanged() => NotifyNotchModelSummaryChanged();
    void INotchSettingsCallbackHost.ApplySettingsOrchestration(string propertyName) => ApplySettingsOrchestration(propertyName);
    void INotchSettingsCallbackHost.InvalidateCanvas() => CanvasHost?.Invalidate();
    void INotchSettingsCallbackHost.RefreshNotchCanvasPreview(bool showStatus) => RefreshNotchCanvasPreview(showStatus);
    void INotchSettingsCallbackHost.AdvanceNotchPreviewStageOnce() => AdvanceNotchPreviewStageOnce();
    void INotchSettingsCallbackHost.StartNotchPreviewAutoPlayLoop() => StartNotchPreviewAutoPlayLoop();
    void INotchSettingsCallbackHost.StopNotchPreviewAutoPlayLoop() => StopNotchPreviewAutoPlayLoop();
    void INotchSettingsCallbackHost.RestartNotchPreviewAutoPlayLoop() => RestartNotchPreviewAutoPlayLoop();
}
