using FreeformHelper.Application.Services;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Logging;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> handles the creation
/// of a <see cref="ProjectUiSnapshot"/> from the current ViewModel state.
/// This snapshot captures non-critical UI settings for persistence, allowing the application
/// to restore its visual and interactive state across sessions.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Builds a <see cref="ProjectUiSnapshot"/> from the current state of the ViewModel.
    /// This method consolidates various UI-related properties into a serializable snapshot object.
    /// </summary>
    /// <returns>A new <see cref="ProjectUiSnapshot"/> instance.</returns>
    private ProjectUiSnapshot BuildUiSnapshot()
    {
        var persistedMatching = _projectFile.UiSnapshot.Matching;
        var snapshot = new ProjectUiSnapshot
        {
            // --- Grid UI Snapshot ---
            Grid = BuildGridSnapshot(),
            // --- Matching UI Snapshot ---
            Matching = new UiMatchingSnapshot
            {
                MatchMode = persistedMatching.MatchMode,
                MatchThreshold = (double)MatchThreshold,
                FreeformAxisThreshold = (double)FreeformAxisThreshold,
                EnableAutoDetectXy = EnableAutoDetectXy,
                EnableFreeformEdgeSpecialization = EnableFreeformEdgeSpecialization,
                EnableCentroidFallback = persistedMatching.EnableCentroidFallback,
                NearestK = persistedMatching.NearestK
            },
            // --- View UI Snapshot ---
            View = BuildViewSnapshot(),
            // --- Notch UI Snapshot ---
            Notch = new UiNotchSnapshot
            {
                LenScale = (int)Math.Round(LenScale),
                NullValue = (int)Math.Round(NullValue),
                ThresholdQ7 = (int)Math.Round(NotchThresholdQ7),
                ThresholdPercentV22 = (double)NotchThresholdPercent,
                LinkThresholds = LinkNotchThresholds,
                EnableToFull = EnableToFull
            },
            // --- Import UI Snapshot ---
            Import = new UiImportSnapshot
            {
                OnlyClosedPolylines = ImportOnlyClosedPolylines,
                IncludeBlockPolylines = ImportBlockPolylines,
                LogLevel = SelectedLogLevelOption.Value,
                RegularVisibilityMaskSourcePath = _regularVisibilityMask.SourcePath,
                UseRegularVisibilityMask = RegularVisibilityMaskUi.IsEnabled
            }
        };

        return snapshot;
    }

    private UiGridSnapshot BuildGridSnapshot() =>
        new UiGridSnapshot
        {
            CascadeCount = (int)Math.Round(CascadeNum),
            // Capture per-IC channel settings.
            IcX = CascadeIcSettings.Select(s => Math.Max(1, (int)Math.Round(s.XChannels))).ToList(),
            IcY = CascadeIcSettings.Select(s => Math.Max(1, (int)Math.Round(s.YChannels))).ToList(),
            TotalX = (int)Math.Round(XChannels),
            TotalY = (int)Math.Round(YChannels),
            // Store display strings for selected options.
            ScanOrder = SelectedScanOrderOption.Display ?? SelectedScanOrder.ToString(),
            AlignmentMode = SelectedGridAlignmentOption.Display ?? GridAlignmentMode.ToString(),
            GridPaddingPercent = (double)GridPaddingPercent,
            ActiveAreaWidthMm = (double)ActiveAreaWidth,
            ActiveAreaHeightMm = (double)ActiveAreaHeight,
            PitchXmm = PitchSizeX,
            PitchYmm = PitchSizeY
        };

    private UiViewSnapshot BuildViewSnapshot() =>
        new UiViewSnapshot
        {
            ShowCad = ShowCad,
            ShowRegular = ShowRegular,
            HighlightUnmatched = HighlightUnmatched,
            HighlightFreeform = HighlightFreeform,
            ColorCadByArea = ColorCadByArea,
            CadLineWidth = (double)CadLineWidth,
            CadLineColor = CadLineColorHex, // Store as hex string.
            CadFillOpacity = (double)CadFillOpacity,
            CadLineOpacity = (double)CadLineOpacity,
            RegularLineWidth = (double)RegularLineWidth,
            HighlightStrokeWidthAdjust = (double)HighlightStrokeWidthAdjust,
            RegularLineColor = RegularLineColorHex, // Store as hex string.
            RegularFillOpacity = (double)RegularFillOpacity,
            RegularLineOpacity = (double)RegularLineOpacity,
            RegularSelectedColor = RegularSelectedColorHex,
            RegularSelectedFillOpacity = (double)RegularSelectedFillOpacity,
            AreaBucketTolerance = (double)AreaBucketTolerance,
            MaxAreaBuckets = (int)Math.Round(MaxAreaBuckets),
            ShowDiffIndexOverlay = ShowDiffIndexOverlay,
            ShowNotchCanvasPreview = ShowNotchCanvasPreview,
            ShowNotchToRegularLabels = ShowNotchToRegularLabels,
            GlobalFontSizePercent = (double)GlobalFontSizePercent,
            NotchPreviewVisualizationStep = (double)NotchPreviewVisualizationStep,
            NotchPreviewAutoPlayEnabled = NotchPreviewAutoPlayEnabled,
            NotchPreviewAutoPlayIntervalMs = (double)NotchPreviewAutoPlayIntervalMs,
            NotchExportFileType = SelectedNotchExportFileTypeOption.Value.ToString(),
            DxfLayerImagePreferredLayer = _dxfLayerImageExportPreferences.LayerName,
            DxfLayerImageWidthPixels = _dxfLayerImageExportPreferences.WidthPixels,
            DxfLayerImageHeightPixels = _dxfLayerImageExportPreferences.HeightPixels,
            DxfLayerImageLineWidthPixels = (double)_dxfLayerImageExportPreferences.LineWidthPixels,
            DxfLayerImagePaddingXPixels = _dxfLayerImageExportPreferences.PaddingXPixels,
            DxfLayerImagePaddingYPixels = _dxfLayerImageExportPreferences.PaddingYPixels,
            DxfLayerImageFormat = _dxfLayerImageExportPreferences.Format.ToString(),
            DxfLayerImageUseDarkTheme = _dxfLayerImageExportPreferences.UseDarkTheme,
            CoordinatePixelWidth = (double)CoordinatePixelWidth,
            CoordinatePixelHeight = (double)CoordinatePixelHeight,
            CoordinatePreferredAaOutlineLayerName = CoordinatePreferredAaOutlineLayerName,
            // Capture the selection state of each layer toggle.
            LayerSelections = LayerToggles.Select(t => new LayerSelectionSnapshot { Name = t.Name, IsSelected = t.IsSelected }).ToList()
        };

    /// <summary>
    /// Applies persisted UI view settings from a <see cref="UiViewSnapshot"/>.
    /// </summary>
    /// <param name="snapshot">The snapshot containing view settings.</param>
    private void ApplyViewSnapshot(UiViewSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        using (_settingsSuppression.EnterSettingsLoading())
        using (_undoSuppression.Enter())
        {

            ShowCad = snapshot.ShowCad;
            ShowRegular = snapshot.ShowRegular;
            HighlightUnmatched = snapshot.HighlightUnmatched;
            HighlightFreeform = snapshot.HighlightFreeform;
            ColorCadByArea = snapshot.ColorCadByArea;

            CadLineWidth = (decimal)snapshot.CadLineWidth;
            CadFillOpacity = (decimal)Math.Clamp(snapshot.CadFillOpacity, 0.0, 1.0);
            CadLineOpacity = snapshot.CadLineOpacity <= 0
                ? 1.0m
                : (decimal)Math.Clamp(snapshot.CadLineOpacity, 0.0, 1.0);
            if (!string.IsNullOrWhiteSpace(snapshot.CadLineColor))
            {
                CadLineColorHex = snapshot.CadLineColor;
            }

            RegularLineWidth = (decimal)snapshot.RegularLineWidth;
            HighlightStrokeWidthAdjust = (decimal)Math.Clamp(snapshot.HighlightStrokeWidthAdjust, -2.0, 4.0);
            RegularFillOpacity = (decimal)Math.Clamp(snapshot.RegularFillOpacity, 0.0, 1.0);
            RegularLineOpacity = snapshot.RegularLineOpacity <= 0
                ? 1.0m
                : (decimal)Math.Clamp(snapshot.RegularLineOpacity, 0.0, 1.0);
            if (!string.IsNullOrWhiteSpace(snapshot.RegularLineColor))
            {
                RegularLineColorHex = snapshot.RegularLineColor;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.RegularSelectedColor))
            {
                RegularSelectedColorHex = snapshot.RegularSelectedColor;
            }
            if (snapshot.RegularSelectedFillOpacity > 0)
            {
                RegularSelectedFillOpacity = (decimal)Math.Clamp(snapshot.RegularSelectedFillOpacity, 0.0, 1.0);
            }

            AreaBucketTolerance = (decimal)snapshot.AreaBucketTolerance;
            MaxAreaBuckets = snapshot.MaxAreaBuckets;
            ShowDiffIndexOverlay = snapshot.ShowDiffIndexOverlay;
            ShowNotchCanvasPreview = snapshot.ShowNotchCanvasPreview;
            ShowNotchToRegularLabels = snapshot.ShowNotchToRegularLabels;
            GlobalFontSizePercent = (decimal)Math.Clamp(snapshot.GlobalFontSizePercent, 80.0, 140.0);
            NotchPreviewVisualizationStep = (decimal)Math.Clamp(snapshot.NotchPreviewVisualizationStep, 1.0, 3.0);
            NotchPreviewAutoPlayIntervalMs = (decimal)Math.Clamp(snapshot.NotchPreviewAutoPlayIntervalMs, 200.0, 5000.0);
            NotchPreviewAutoPlayEnabled = snapshot.NotchPreviewAutoPlayEnabled;
            if (NotchExportFileTypeMetadata.TryParseStoredValue(snapshot.NotchExportFileType, out var exportFileType))
            {
                var exportOption = NotchExportFileTypeOptions.FirstOrDefault(o => o.Value == exportFileType);
                if (!string.IsNullOrWhiteSpace(exportOption.Display))
                {
                    SelectedNotchExportFileTypeOption = exportOption;
                }
            }

            ApplyDxfLayerImageExportSnapshot(snapshot);
            CoordinatePixelWidth = (decimal)Math.Max(
                1.0,
                snapshot.CoordinatePixelWidth > 0.0
                    ? snapshot.CoordinatePixelWidth
                    : (double)XChannels);
            CoordinatePixelHeight = (decimal)Math.Max(
                1.0,
                snapshot.CoordinatePixelHeight > 0.0
                    ? snapshot.CoordinatePixelHeight
                    : (double)YChannels);
            CoordinatePreferredAaOutlineLayerName = snapshot.CoordinatePreferredAaOutlineLayerName?.Trim() ?? string.Empty;

        }
    }

    private void ApplyImportSnapshot(UiImportSnapshot snapshot, ProjectFile? projectFile = null)
    {
        if (snapshot is null)
        {
            return;
        }

        var allowRuntimeRefresh = !_isLoadingSettings;
        using var settingsLoading = _settingsSuppression.EnterSettingsLoading();
        ImportOnlyClosedPolylines = snapshot.OnlyClosedPolylines;
        ImportBlockPolylines = snapshot.IncludeBlockPolylines;
        if (!string.IsNullOrWhiteSpace(snapshot.LogLevel))
        {
            var currentLevel = LoggingBootstrapper.GetCurrentMinimumLevelName();
            var normalizedLevel = snapshot.LogLevel.Trim();
            if (!string.Equals(currentLevel, normalizedLevel, StringComparison.OrdinalIgnoreCase))
            {
                var appliedLevel = LoggingBootstrapper.ApplyMinimumLevel(normalizedLevel);
                var normalized = LogLevelOptions.FirstOrDefault(option =>
                    string.Equals(option.Value, appliedLevel, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(normalized.Display))
                {
                    SelectedLogLevelOption = normalized;
                }
            }
        }

        ApplyRegularVisibilityMaskSnapshot(
            sourcePath: snapshot.RegularVisibilityMaskSourcePath,
            isEnabled: snapshot.UseRegularVisibilityMask,
            allowRuntimeRefresh: allowRuntimeRefresh,
            embeddedMask: projectFile?.EmbeddedRegularVisibilityMask,
            embeddedMaskName: projectFile?.EmbeddedRegularVisibilityMaskName);
    }

    /// <summary>
    /// Determines if the application should prompt the user to save before exiting.
    /// This is true if there are unsaved changes or loaded CAD content has never been saved.
    /// </summary>
    /// <returns>True if a save prompt is needed, false otherwise.</returns>
    public bool ShouldPromptSaveOnExit()
    {
        return ProjectClosePolicy.ShouldPromptSave(HasUnsavedChanges, _cad is not null, _lastSavedPath);
    }
}
