// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Contracts.Settings;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns settings callback decisions while the existing Notch owner retains results and playback lifetime.</summary>
internal sealed class NotchSettingsCallbackAdapter(INotchSettingsCallbackHost host)
{
    private readonly INotchSettingsCallbackHost _host = host;

    internal void OnSelectedNotchCompensationModelOptionChanged(NotchCompensationModel value)
    {
        ApplyNotchCompensationModeToLegacySwitches(value);
        _host.NotifyNotchModelSummaryChanged();
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration("SelectedNotchCompensationModelOption");
    }

    internal void ApplyNotchCompensationModeToLegacySwitches(
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
        using (_host.EnterSettingsLoading())
        using (_host.EnterUndoSuppression())
        {
            _host.EnableToRegular = enableToRegular;
            _host.EnableToFull = enableToFull;
        }

        _host.NotifyPropertyChanged("CanShowToFullPreviewToggle");
        _host.NotifyNotchModelSummaryChanged();
    }

    internal void OnEnableToFullRuleEngineChanged()
    {
        _host.NotifyPropertyChanged("NotchToFullRuleEngineSummary");
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration("EnableToFullRuleEngine");
    }

    internal void OnEnableToFullRuleTraceChanged()
    {
        _host.NotifyPropertyChanged("NotchToFullRuleEngineSummary");
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration("EnableToFullRuleTrace");
    }

    internal void OnEnableToRegularChanged()
    {
        _host.NotifyNotchModelSummaryChanged();
        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration(nameof(_host.EnableToRegular));
    }

    internal void OnNotchCanvasPreviewItemsChanged()
    {
        NotifyNotchOverlayVisibilityChanged();
        SyncNotchPreviewPlaybackAndCanvas();
    }

    internal void OnToFullStrictOverlapPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 100m);
        if (clamped != value)
        {
            using (_host.EnterUndoSuppression())
            {
                _host.ToFullStrictOverlapPercent = clamped;
            }
            return;
        }

        if (_host.IsLoadingSettings)
        {
            return;
        }

        _host.ApplySettingsOrchestration(nameof(_host.ToFullStrictOverlapPercent));
    }

    internal void RefreshNotchCanvasPreviewAfterStep3SettingsChanged(bool canRefreshPreview)
    {
        if (canRefreshPreview)
        {
            _host.RefreshNotchCanvasPreview(showStatus: false);
            return;
        }

        SyncNotchPreviewPlaybackAndCanvas();
    }

    internal void OnNotchThresholdQ7Changed(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 128m);
        if (clamped != value)
        {
            using (_host.EnterUndoSuppression())
            {
                _host.NotchThresholdQ7 = clamped;
            }
            return;
        }

        if (_host.IsNotchThresholdSyncActive)
        {
            return;
        }

        if (_host.IsLoadingSettings)
        {
            return;
        }

        if (_host.LinkNotchThresholds)
        {
            using (_host.EnterNotchThresholdSync())
            {
                _host.NotchThresholdPercent = Math.Round(clamped * 100m / 128m, 2);
            }
        }
    }

    internal void OnNotchThresholdPercentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0m, 100m);
        if (clamped != value)
        {
            using (_host.EnterUndoSuppression())
            {
                _host.NotchThresholdPercent = clamped;
            }
            return;
        }

        if (_host.IsNotchThresholdSyncActive)
        {
            return;
        }

        if (_host.IsLoadingSettings)
        {
            return;
        }

        if (_host.LinkNotchThresholds)
        {
            var q7 = Math.Clamp((int)Math.Round(clamped * 128m / 100m, MidpointRounding.AwayFromZero), 0, 128);
            using (_host.EnterNotchThresholdSync())
            {
                _host.NotchThresholdQ7 = q7;
            }
        }
    }

    internal void OnLinkNotchThresholdsChanged(bool value)
    {
        if (_host.IsLoadingSettings || _host.IsNotchThresholdSyncActive)
        {
            return;
        }

        if (!value)
        {
            return;
        }

        using (_host.EnterNotchThresholdSync())
        {
            _host.NotchThresholdPercent = Math.Round(_host.NotchThresholdQ7 * 100m / 128m, 2);
        }
    }

    internal void OnNotchPreviewVisualizationStepChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1m, 3m);
        if (clamped != value)
        {
            using (_host.EnterUndoSuppression())
            {
                _host.NotchPreviewVisualizationStep = clamped;
            }
            return;
        }

        NotifyNotchOverlayVisibilityChanged();
        _host.InvalidateCanvas();
    }

    internal void OnNotchPreviewAutoPlayEnabledChanged(bool value)
    {
        _host.NotifyPropertyChanged("NotchPreviewAutoPlaySummary");
        SyncNotchPreviewPlaybackAndCanvas(forceAdvanceOnActivate: value);
    }

    internal void OnShowNotchCanvasPreviewChanged()
    {
        NotifyNotchOverlayVisibilityChanged();
        SyncNotchPreviewPlaybackAndCanvas();
    }

    internal void OnShowNotchToRegularLabelsChanged()
    {
        NotifyNotchOverlayVisibilityChanged();
        _host.InvalidateCanvas();
    }

    internal void NotifyNotchOverlayVisibilityChanged()
    {
        _host.NotifyPropertyChanged(nameof(_host.IsNotchCanvasPreviewVisible));
        _host.NotifyPropertyChanged("CanControlNotchPreviewStage");
        _host.NotifyPropertyChanged("NotchPreviewStageIndex");
        _host.NotifyPropertyChanged("NotchPreviewStageSummary");
        _host.NotifyPropertyChanged("NotchPreviewSeedLayerStateText");
        _host.NotifyPropertyChanged("NotchPreviewCandidateLayerStateText");
        _host.NotifyPropertyChanged("NotchPreviewFinalLayerStateText");
        _host.NotifyPropertyChanged("IsNotchToRegularPreviewVisible");
        _host.NotifyPropertyChanged("IsNotchToFullSeedPreviewVisible");
        _host.NotifyPropertyChanged("IsNotchToFullCandidatePreviewVisible");
        _host.NotifyPropertyChanged("IsNotchToFullFinalPreviewVisible");
    }

    internal void SyncNotchPreviewPlaybackAndCanvas()
    {
        SyncNotchPreviewPlaybackAndCanvas(forceAdvanceOnActivate: false);
    }

    internal void SyncNotchPreviewPlaybackAndCanvas(bool forceAdvanceOnActivate)
    {
        var isVisible = _host.IsNotchCanvasPreviewVisible;
        var becameVisible = isVisible && !_host.LastPreviewVisibleForPlayback;
        if (_host.NotchPreviewAutoPlayEnabled && isVisible)
        {
            if (forceAdvanceOnActivate || becameVisible)
            {
                _host.AdvanceNotchPreviewStageOnce();
            }

            _host.StartNotchPreviewAutoPlayLoop();
        }
        else
        {
            _host.StopNotchPreviewAutoPlayLoop();
        }

        _host.LastPreviewVisibleForPlayback = isVisible;
        _host.InvalidateCanvas();
    }

    internal void OnNotchPreviewAutoPlayIntervalMsChanged(decimal value)
    {
        var clamped = Math.Clamp(value, NotchPreviewTiming.AutoPlayIntervalMinMs, NotchPreviewTiming.AutoPlayIntervalMaxMs);
        if (clamped != value)
        {
            using (_host.EnterUndoSuppression())
            {
                _host.NotchPreviewAutoPlayIntervalMs = clamped;
            }
            return;
        }

        _host.NotifyPropertyChanged("NotchPreviewAutoPlaySummary");
        if (_host.NotchPreviewAutoPlayEnabled)
        {
            _host.RestartNotchPreviewAutoPlayLoop();
        }
    }

}
