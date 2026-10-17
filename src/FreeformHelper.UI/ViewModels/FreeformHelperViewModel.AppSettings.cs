using System.Globalization;
using Avalonia.Threading;
using FreeformHelper.Application.Services;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private static readonly TimeSpan AppGeneralSettingsPersistDebounce = TimeSpan.FromMilliseconds(450);
    private readonly AppGeneralSettingsStore _appGeneralSettingsStore;
    private readonly AppGeneralPersistenceState _appGeneralPersistence = new(); // Holds the timer, the flags and the snapshots of the app general settings persistence.

    /// <summary>Gets the app general settings persistence holder for tests.</summary>
    internal AppGeneralPersistenceState AppGeneralPersistenceForTests => _appGeneralPersistence;

    private void InitializeAppGeneralSettingsPersistence()
    {
        _ = EnsureAppGeneralSettingsPersistTimer();
    }

    private DispatcherTimer? EnsureAppGeneralSettingsPersistTimer()
    {
        if (_appGeneralPersistence.PersistTimer is not null)
        {
            return _appGeneralPersistence.PersistTimer;
        }

        if (!UiThread.IsCurrent(out _, out _))
        {
            return null;
        }

        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = AppGeneralSettingsPersistDebounce,
        };
        timer.Tick += OnAppGeneralSettingsPersistTimerTick;
        _appGeneralPersistence.PersistTimer = timer;
        return timer;
    }

    private void OnAppGeneralSettingsPersistTimerTick(object? sender, EventArgs e)
    {
        _appGeneralPersistence.PersistTimer?.Stop();
        PersistAppGeneralSettingsNow();
    }

    private void SchedulePersistAppGeneralSettings()
    {
        if (_appGeneralPersistence.SuppressPersistence || _isLoadingSettings)
        {
            return;
        }

        if (ShouldDeferAppGeneralSettingsPersistence())
        {
            _appGeneralPersistence.HasDeferredChanges = true;
            return;
        }

        var timer = EnsureAppGeneralSettingsPersistTimer();
        timer?.Stop();
        timer?.Start();
    }

    internal void PersistAppGeneralSettingsNow(bool forceWhenDeferred = false)
    {
        if (_appGeneralPersistence.SuppressPersistence || _isLoadingSettings)
        {
            return;
        }

        if (!forceWhenDeferred && ShouldDeferAppGeneralSettingsPersistence())
        {
            _appGeneralPersistence.HasDeferredChanges = true;
            return;
        }

        try
        {
            var uiSnapshot = BuildUiSnapshot();
            var document = new AppGeneralSettingsDocument
            {
                View = CloneViewSnapshot(uiSnapshot.View),
                Import = CloneImportSnapshot(uiSnapshot.Import),
                Behavior = new AppGeneralBehaviorSettings
                {
                    ApplyVisualPreferencesOnProjectLoad = ApplyAppVisualPreferencesOnProjectLoad
                }
            };
            _appGeneralSettingsStore.Save(document);
            _appGeneralPersistence.ViewSnapshot = CloneViewSnapshot(document.View);
            _appGeneralPersistence.ImportSnapshot = CloneImportSnapshot(document.Import);
            _appGeneralPersistence.HasDeferredChanges = false;
            Logger.Debug(CultureInfo.InvariantCulture, "App general settings persisted: {0}", _appGeneralSettingsStore.SettingsPath);
        }
        catch (Exception ex) when (ExceptionPolicy.IsRecoverable(ex))
        {
            Logger.Warn(ex, "Persist app general settings failed.");
        }
    }

    private bool ShouldDeferAppGeneralSettingsPersistence()
    {
        return _appGeneralPersistence.DeferAfterProjectLoad;
    }

    private void MarkProjectLoadedForAppGeneralPersistence()
    {
        _appGeneralPersistence.DeferAfterProjectLoad = true;
        _appGeneralPersistence.PersistTimer?.Stop();
    }

    private void FlushDeferredAppGeneralSettingsIfNeeded()
    {
        if (!_appGeneralPersistence.DeferAfterProjectLoad || !_appGeneralPersistence.HasDeferredChanges)
        {
            return;
        }

        PersistAppGeneralSettingsNow(forceWhenDeferred: true);
    }

    private void TryApplyAppGeneralSettingsLayer()
    {
        var document = _appGeneralSettingsStore.TryLoad();
        if (document is null)
        {
            return;
        }

        _appGeneralPersistence.ViewSnapshot = CloneViewSnapshot(document.View);
        _appGeneralPersistence.ImportSnapshot = CloneImportSnapshot(document.Import);
        using (_settingsSuppression.EnterSettingsLoading())
        {
            ApplyAppVisualPreferencesOnProjectLoad = document.Behavior.ApplyVisualPreferencesOnProjectLoad;
        }
        _projectFile.UiSnapshot = new ProjectUiSnapshot
        {
            View = CloneViewSnapshot(_appGeneralPersistence.ViewSnapshot),
            Import = CloneImportSnapshot(_appGeneralPersistence.ImportSnapshot)
        };
        Logger.Info(CultureInfo.InvariantCulture, "App general settings loaded: {0}", _appGeneralSettingsStore.SettingsPath);
    }

    private void TryApplyAppGeneralVisualPreferencesAfterProjectLoad()
    {
        if (!ApplyAppVisualPreferencesOnProjectLoad)
        {
            return;
        }

        ApplyViewSnapshot(CloneViewSnapshot(_appGeneralPersistence.ViewSnapshot));
        Logger.Info(CultureInfo.InvariantCulture, "Load project: app visual preferences layer applied.");
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

    /// <summary>
    /// Holds the debounce timer, the suppression and deferral flags and the last loaded or saved snapshots of the
    /// app general settings persistence, for the main view model.
    /// </summary>
    internal sealed class AppGeneralPersistenceState
    {
        /// <summary>Gets or sets the debounce timer that writes the app general settings. It is created on first use on the UI thread.</summary>
        internal DispatcherTimer? PersistTimer { get; set; }

        /// <summary>Gets or sets a value indicating whether writes wait for the next successful project save after a project load.</summary>
        internal bool DeferAfterProjectLoad { get; set; }

        /// <summary>Gets or sets a value indicating whether changes are not persisted, for example while settings are applied in bulk.</summary>
        internal bool SuppressPersistence { get; set; }

        /// <summary>Gets or sets a value indicating whether a change waited for the next successful project save.</summary>
        internal bool HasDeferredChanges { get; set; }

        /// <summary>Gets or sets the view snapshot of the last loaded or saved app general settings.</summary>
        internal UiViewSnapshot ViewSnapshot { get; set; } = new();

        /// <summary>Gets or sets the import snapshot of the last loaded or saved app general settings.</summary>
        internal UiImportSnapshot ImportSnapshot { get; set; } = new();
    }
}

