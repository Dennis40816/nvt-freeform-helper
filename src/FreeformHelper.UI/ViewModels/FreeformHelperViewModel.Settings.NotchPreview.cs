using System.Collections.ObjectModel;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnSelectedNotchCompensationModelOptionChanged(NotchCompensationModelOption value)
    {
        ApplyNotchCompensationModeToLegacySwitches(value.Value);
        NotifyNotchModelSummaryChanged();
        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(SelectedNotchCompensationModelOption));
    }

    private void ApplyNotchCompensationModeToLegacySwitches(
        NotchCompensationModel mode,
        bool? currentGainEnableToRegular = null,
        bool? currentGainEnableToFull = null)
    {
        var (enableToRegular, enableToFull) = mode switch
        {
            NotchCompensationModel.CurrentGain => (
                currentGainEnableToRegular ?? true,
                currentGainEnableToFull ?? true),
            NotchCompensationModel.ConservativeNoGain => (true, true),
            NotchCompensationModel.Disabled => (false, false),
            _ => (currentGainEnableToRegular ?? true, currentGainEnableToFull ?? true),
        };
        var wasLoading = _isLoadingSettings;
        var wasSuppressUndo = _suppressUndo;
        _isLoadingSettings = true;
        _suppressUndo = true;
        try
        {
            EnableToRegular = enableToRegular;
            EnableToFull = enableToFull;
        }
        finally
        {
            _isLoadingSettings = wasLoading;
            _suppressUndo = wasSuppressUndo;
        }

        OnPropertyChanged(nameof(CanShowToFullPreviewToggle));
        NotifyNotchModelSummaryChanged();
    }

    partial void OnEnableToFullRuleEngineChanged(bool value)
    {
        OnPropertyChanged(nameof(NotchToFullRuleEngineSummary));
        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(EnableToFullRuleEngine));
    }

    partial void OnEnableToFullRuleTraceChanged(bool value)
    {
        OnPropertyChanged(nameof(NotchToFullRuleEngineSummary));
        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(EnableToFullRuleTrace));
    }

    partial void OnEnableToRegularChanged(bool value)
    {
        NotifyNotchModelSummaryChanged();
        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(EnableToRegular));
    }

    partial void OnNotchCanvasPreviewItemsChanged(ObservableCollection<NotchCanvasPreviewItem> value)
    {
        NotifyNotchOverlayVisibilityChanged();
        SyncNotchPreviewPlaybackAndCanvas();
    }

    partial void OnToFullStrictOverlapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 100m);
        if (clamped != value)
        {
            _suppressUndo = true;
            ToFullStrictOverlapPercent = clamped;
            _suppressUndo = false;
            return;
        }

        if (_isLoadingSettings)
        {
            return;
        }

        ApplySettingsOrchestration(nameof(ToFullStrictOverlapPercent));
    }

    private void RefreshNotchCanvasPreviewAfterStep3SettingsChanged()
    {
        var canRefreshPreview =
            _grid is not null &&
            _selectedCadIds.Count > 0 &&
            _latestPadMatchResult.CadToRegular.Count > 0;

        if (canRefreshPreview)
        {
            RefreshNotchCanvasPreview(showStatus: false);
            return;
        }

        SyncNotchPreviewPlaybackAndCanvas();
    }

    partial void OnNotchThresholdQ7Changed(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 128m);
        if (clamped != value)
        {
            _suppressUndo = true;
            NotchThresholdQ7 = clamped;
            _suppressUndo = false;
            return;
        }

        if (_suppressNotchThresholdSync)
        {
            return;
        }

        if (_isLoadingSettings)
        {
            return;
        }

        if (LinkNotchThresholds)
        {
            _suppressNotchThresholdSync = true;
            NotchThresholdPercent = Math.Round(clamped * 100m / 128m, 2);
            _suppressNotchThresholdSync = false;
        }
    }

    partial void OnNotchThresholdPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 100m);
        if (clamped != value)
        {
            _suppressUndo = true;
            NotchThresholdPercent = clamped;
            _suppressUndo = false;
            return;
        }

        if (_suppressNotchThresholdSync)
        {
            return;
        }

        if (_isLoadingSettings)
        {
            return;
        }

        if (LinkNotchThresholds)
        {
            var q7 = Math.Clamp((int)Math.Round(clamped * 128m / 100m, MidpointRounding.AwayFromZero), 0, 128);
            _suppressNotchThresholdSync = true;
            NotchThresholdQ7 = q7;
            _suppressNotchThresholdSync = false;
        }
    }

    partial void OnLinkNotchThresholdsChanged(bool value)
    {
        if (_isLoadingSettings || _suppressNotchThresholdSync)
        {
            return;
        }

        if (!value)
        {
            return;
        }

        _suppressNotchThresholdSync = true;
        NotchThresholdPercent = Math.Round(NotchThresholdQ7 * 100m / 128m, 2);
        _suppressNotchThresholdSync = false;
    }

    partial void OnNotchPreviewVisualizationStepChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1m, 3m);
        if (clamped != value)
        {
            _suppressUndo = true;
            NotchPreviewVisualizationStep = clamped;
            _suppressUndo = false;
            return;
        }

        NotifyNotchOverlayVisibilityChanged();
        CanvasHost?.Invalidate();
    }

    partial void OnNotchPreviewAutoPlayEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(NotchPreviewAutoPlaySummary));
        SyncNotchPreviewPlaybackAndCanvas(forceAdvanceOnActivate: value);
    }

    partial void OnShowNotchCanvasPreviewChanged(bool value)
    {
        NotifyNotchOverlayVisibilityChanged();
        SyncNotchPreviewPlaybackAndCanvas();
    }

    partial void OnShowNotchToRegularLabelsChanged(bool value)
    {
        NotifyNotchOverlayVisibilityChanged();
        CanvasHost?.Invalidate();
    }

    private void NotifyNotchOverlayVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsNotchCanvasPreviewVisible));
        OnPropertyChanged(nameof(CanControlNotchPreviewStage));
        OnPropertyChanged(nameof(NotchPreviewStageIndex));
        OnPropertyChanged(nameof(NotchPreviewStageSummary));
        OnPropertyChanged(nameof(NotchPreviewSeedLayerStateText));
        OnPropertyChanged(nameof(NotchPreviewCandidateLayerStateText));
        OnPropertyChanged(nameof(NotchPreviewFinalLayerStateText));
        OnPropertyChanged(nameof(IsNotchToRegularPreviewVisible));
        OnPropertyChanged(nameof(IsNotchToFullSeedPreviewVisible));
        OnPropertyChanged(nameof(IsNotchToFullCandidatePreviewVisible));
        OnPropertyChanged(nameof(IsNotchToFullFinalPreviewVisible));
    }

    private void SyncNotchPreviewPlaybackAndCanvas()
    {
        SyncNotchPreviewPlaybackAndCanvas(forceAdvanceOnActivate: false);
    }

    private void SyncNotchPreviewPlaybackAndCanvas(bool forceAdvanceOnActivate)
    {
        var isVisible = IsNotchCanvasPreviewVisible;
        var becameVisible = isVisible && !_lastNotchPreviewVisibleForPlayback;
        if (NotchPreviewAutoPlayEnabled && isVisible)
        {
            if (forceAdvanceOnActivate || becameVisible)
            {
                AdvanceNotchPreviewStageOnce();
            }

            StartNotchPreviewAutoPlayLoop();
        }
        else
        {
            StopNotchPreviewAutoPlayLoop();
        }

        _lastNotchPreviewVisibleForPlayback = isVisible;
        CanvasHost?.Invalidate();
    }

    partial void OnNotchPreviewAutoPlayIntervalMsChanged(decimal value)
    {
        var clamped = Math.Clamp(value, NotchPreviewAutoPlayIntervalMinMs, NotchPreviewAutoPlayIntervalMaxMs);
        if (clamped != value)
        {
            _suppressUndo = true;
            NotchPreviewAutoPlayIntervalMs = clamped;
            _suppressUndo = false;
            return;
        }

        OnPropertyChanged(nameof(NotchPreviewAutoPlaySummary));
        if (NotchPreviewAutoPlayEnabled)
        {
            RestartNotchPreviewAutoPlayLoop();
        }
    }

}
