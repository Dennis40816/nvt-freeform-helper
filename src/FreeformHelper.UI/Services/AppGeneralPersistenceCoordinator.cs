// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.Infrastructure.Project;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.Services;

/// <summary>Coordinates app preference persistence using the workspace's existing holder and snapshot path.</summary>
internal sealed class AppGeneralPersistenceCoordinator
{
    private static readonly TimeSpan _appGeneralSettingsPersistDebounce = TimeSpan.FromMilliseconds(450);
    private readonly IAppGeneralPersistenceHost _host;
    // UI thread only after construction. The port refers to the original holder, never a second snapshot cache.
    private readonly IAppGeneralPersistenceState _state;

    internal AppGeneralPersistenceCoordinator(IAppGeneralPersistenceHost host, IAppGeneralPersistenceState state)
    {
        _host = host;
        _state = state;
    }

    internal void InitializeAppGeneralSettingsPersistence()
    {
        _ = EnsureAppGeneralSettingsPersistTimer();
    }

    private DispatcherTimer? EnsureAppGeneralSettingsPersistTimer()
    {
        if (_state.PersistTimer is not null)
        {
            return _state.PersistTimer;
        }

        if (!UiThread.IsCurrent(out _, out _))
        {
            return null;
        }

        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = _appGeneralSettingsPersistDebounce,
        };
        timer.Tick += OnAppGeneralSettingsPersistTimerTick;
        _state.PersistTimer = timer;
        return timer;
    }

    private void OnAppGeneralSettingsPersistTimerTick(object? sender, EventArgs e)
    {
        _state.PersistTimer?.Stop();
        PersistAppGeneralSettingsNow();
    }

    internal void SchedulePersistAppGeneralSettings()
    {
        if (_state.SuppressPersistence || _host.IsLoadingSettings)
        {
            return;
        }

        if (ShouldDeferAppGeneralSettingsPersistence())
        {
            _state.HasDeferredChanges = true;
            return;
        }

        var timer = EnsureAppGeneralSettingsPersistTimer();
        timer?.Stop();
        timer?.Start();
    }

    internal void PersistAppGeneralSettingsNow(bool forceWhenDeferred = false)
    {
        if (_state.SuppressPersistence || _host.IsLoadingSettings)
        {
            return;
        }

        if (!forceWhenDeferred && ShouldDeferAppGeneralSettingsPersistence())
        {
            _state.HasDeferredChanges = true;
            return;
        }

        try
        {
            var uiSnapshot = _host.BuildUiSnapshot();
            var document = new AppGeneralSettingsDocument
            {
                View = CloneViewSnapshot(uiSnapshot.View),
                Import = CloneImportSnapshot(uiSnapshot.Import),
                Behavior = new AppGeneralBehaviorSettings
                {
                    ApplyVisualPreferencesOnProjectLoad = _host.ApplyAppVisualPreferencesOnProjectLoad
                }
            };
            _host.SaveAppGeneralSettings(document);
            _state.ViewSnapshot = CloneViewSnapshot(document.View);
            _state.ImportSnapshot = CloneImportSnapshot(document.Import);
            _state.HasDeferredChanges = false;
            _host.ReportAppGeneralSettingsPersisted();
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            _host.ReportAppGeneralPersistenceFailure(ex);
        }
    }

    private bool ShouldDeferAppGeneralSettingsPersistence()
    {
        return _state.DeferAfterProjectLoad;
    }

    internal void MarkProjectLoadedForAppGeneralPersistence()
    {
        _state.DeferAfterProjectLoad = true;
        _state.PersistTimer?.Stop();
    }

    internal void FlushDeferredAppGeneralSettingsIfNeeded()
    {
        if (!_state.DeferAfterProjectLoad || !_state.HasDeferredChanges)
        {
            return;
        }

        PersistAppGeneralSettingsNow(forceWhenDeferred: true);
    }

    internal void TryApplyAppGeneralSettingsLayer()
    {
        var document = _host.LoadAppGeneralSettings();
        if (document is null)
        {
            return;
        }

        _state.ViewSnapshot = CloneViewSnapshot(document.View);
        _state.ImportSnapshot = CloneImportSnapshot(document.Import);
        using (_host.EnterSettingsLoading())
        {
            _host.ApplyAppVisualPreferencesOnProjectLoad = document.Behavior.ApplyVisualPreferencesOnProjectLoad;
        }
        _host.ReplaceProjectUiSnapshot(new ProjectUiSnapshot
        {
            View = CloneViewSnapshot(_state.ViewSnapshot),
            Import = CloneImportSnapshot(_state.ImportSnapshot)
        });
        _host.ReportAppGeneralSettingsLoaded();
    }

    internal void TryApplyAppGeneralVisualPreferencesAfterProjectLoad()
    {
        if (!_host.ApplyAppVisualPreferencesOnProjectLoad)
        {
            return;
        }

        _host.ApplyViewSnapshot(CloneViewSnapshot(_state.ViewSnapshot));
        _host.ReportAppVisualPreferencesApplied();
    }

    private static UiViewSnapshot CloneViewSnapshot(UiViewSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new UiViewSnapshot
        {
            ShowCad = source.ShowCad,
            ShowRegular = source.ShowRegular,
            HighlightUnmatched = source.HighlightUnmatched,
            HighlightFreeform = source.HighlightFreeform,
            ColorCadByArea = source.ColorCadByArea,
            CadLineWidth = source.CadLineWidth,
            CadFillOpacity = source.CadFillOpacity,
            CadLineOpacity = source.CadLineOpacity,
            CadLineColor = source.CadLineColor,
            RegularLineWidth = source.RegularLineWidth,
            HighlightStrokeWidthAdjust = source.HighlightStrokeWidthAdjust,
            RegularFillOpacity = source.RegularFillOpacity,
            RegularLineOpacity = source.RegularLineOpacity,
            RegularLineColor = source.RegularLineColor,
            RegularSelectedColor = source.RegularSelectedColor,
            RegularSelectedFillOpacity = source.RegularSelectedFillOpacity,
            AreaBucketTolerance = source.AreaBucketTolerance,
            MaxAreaBuckets = source.MaxAreaBuckets,
            ShowDiffIndexOverlay = source.ShowDiffIndexOverlay,
            ShowNotchCanvasPreview = source.ShowNotchCanvasPreview,
            ShowNotchToRegularLabels = source.ShowNotchToRegularLabels,
            GlobalFontSizePercent = source.GlobalFontSizePercent,
            NotchPreviewVisualizationStep = source.NotchPreviewVisualizationStep,
            NotchPreviewAutoPlayEnabled = source.NotchPreviewAutoPlayEnabled,
            NotchPreviewAutoPlayIntervalMs = source.NotchPreviewAutoPlayIntervalMs,
            NotchExportFileType = source.NotchExportFileType,
            DxfLayerImagePreferredLayer = source.DxfLayerImagePreferredLayer,
            DxfLayerImageWidthPixels = source.DxfLayerImageWidthPixels,
            DxfLayerImageHeightPixels = source.DxfLayerImageHeightPixels,
            DxfLayerImageLineWidthPixels = source.DxfLayerImageLineWidthPixels,
            DxfLayerImagePaddingXPixels = source.DxfLayerImagePaddingXPixels,
            DxfLayerImagePaddingYPixels = source.DxfLayerImagePaddingYPixels,
            DxfLayerImageFormat = source.DxfLayerImageFormat,
            DxfLayerImageUseDarkTheme = source.DxfLayerImageUseDarkTheme,
            CoordinatePixelWidth = source.CoordinatePixelWidth,
            CoordinatePixelHeight = source.CoordinatePixelHeight,
            CoordinatePreferredAaOutlineLayerName = source.CoordinatePreferredAaOutlineLayerName,
            LayerSelections = source.LayerSelections
                .Select(static layer => new LayerSelectionSnapshot
                {
                    Name = layer.Name,
                    IsSelected = layer.IsSelected
                })
                .ToList()
        };
    }

    private static UiImportSnapshot CloneImportSnapshot(UiImportSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new UiImportSnapshot
        {
            OnlyClosedPolylines = source.OnlyClosedPolylines,
            IncludeBlockPolylines = source.IncludeBlockPolylines,
            LogLevel = source.LogLevel
        };
    }
}
