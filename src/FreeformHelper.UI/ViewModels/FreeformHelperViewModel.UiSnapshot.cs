using FreeformHelper.Application.Services;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels.Project;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> handles the creation
/// of a <see cref="ProjectUiSnapshot"/> from the current ViewModel state.
/// This snapshot captures non-critical UI settings for persistence, allowing the application
/// to restore its visual and interactive state across sessions.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private readonly ProjectUiSnapshotMapper _projectUiSnapshotMapper;

    private ProjectUiSnapshot BuildUiSnapshot() => _projectUiSnapshotMapper.Build();

    private ProjectSnapshotReaders CreateProjectSnapshotReaders() => new(
        grid: new ProjectSnapshotReaders.GridReaders(
            cascadeCount: () => CascadeNum, icX: () => CascadeIcSettings.Select(s => s.XChannels),
            icY: () => CascadeIcSettings.Select(s => s.YChannels), totalX: () => XChannels,
            totalY: () => YChannels, scanOrderDisplay: () => SelectedScanOrderOption.Display,
            scanOrderFallback: () => SelectedScanOrder.ToString(), alignmentDisplay: () => SelectedGridAlignmentOption.Display,
            alignmentFallback: () => GridAlignmentMode.ToString(), paddingPercent: () => GridPaddingPercent,
            activeAreaWidth: () => ActiveAreaWidth, activeAreaHeight: () => ActiveAreaHeight,
            pitchX: () => PitchSizeX, pitchY: () => PitchSizeY),
        matching: new ProjectSnapshotReaders.MatchingReaders(
            persisted: () => _projectFile.UiSnapshot.Matching, threshold: () => MatchThreshold,
            axisThreshold: () => FreeformAxisThreshold, autoDetectXy: () => EnableAutoDetectXy,
            edgeSpecialization: () => EnableFreeformEdgeSpecialization),
        notch: new ProjectSnapshotReaders.NotchReaders(
            lenScale: () => LenScale, nullValue: () => NullValue,
            thresholdQ7: () => NotchThresholdQ7, thresholdPercent: () => NotchThresholdPercent,
            linkThresholds: () => LinkNotchThresholds, enableToFull: () => EnableToFull),
        display: new ProjectSnapshotReaders.DisplayReaders(
            showCad: () => ShowCad, showRegular: () => ShowRegular,
            highlightUnmatched: () => HighlightUnmatched, highlightFreeform: () => HighlightFreeform,
            colorCadByArea: () => ColorCadByArea),
        appearance: new ProjectSnapshotReaders.AppearanceReaders(
            cadLineWidth: () => CadLineWidth, cadLineColor: () => CadLineColorHex,
            cadFillOpacity: () => CadFillOpacity, cadLineOpacity: () => CadLineOpacity,
            regularLineWidth: () => RegularLineWidth, highlightStrokeWidthAdjust: () => HighlightStrokeWidthAdjust,
            regularLineColor: () => RegularLineColorHex, regularFillOpacity: () => RegularFillOpacity,
            regularLineOpacity: () => RegularLineOpacity, regularSelectedColor: () => RegularSelectedColorHex,
            regularSelectedFillOpacity: () => RegularSelectedFillOpacity),
        viewOptions: new ProjectSnapshotReaders.ViewOptionsReaders(
            areaBucketTolerance: () => AreaBucketTolerance, maxAreaBuckets: () => MaxAreaBuckets,
            showDiffIndexOverlay: () => ShowDiffIndexOverlay, showNotchCanvasPreview: () => ShowNotchCanvasPreview,
            showNotchToRegularLabels: () => ShowNotchToRegularLabels, globalFontSizePercent: () => GlobalFontSizePercent,
            notchPreviewVisualizationStep: () => NotchPreviewVisualizationStep, notchPreviewAutoPlayEnabled: () => NotchPreviewAutoPlayEnabled,
            notchPreviewAutoPlayIntervalMs: () => NotchPreviewAutoPlayIntervalMs, notchExportFileType: () => SelectedNotchExportFileTypeOption.Value,
            coordinatePixelWidth: () => CoordinatePixelWidth, coordinatePixelHeight: () => CoordinatePixelHeight,
            coordinatePreferredAaOutlineLayerName: () => CoordinatePreferredAaOutlineLayerName),
        import: new ProjectSnapshotReaders.ImportReaders(
            onlyClosedPolylines: () => ImportOnlyClosedPolylines, includeBlockPolylines: () => ImportBlockPolylines,
            logLevel: () => SelectedLogLevelOption.Value, maskSourcePath: () => _regularVisibilityMask.SourcePath,
            useMask: () => RegularVisibilityMaskUi.IsEnabled),
        imagePreferences: () => _dxfLayerImageExportPreferences,
        layers: () => LayerToggles.Select(t => (t.Name, t.IsSelected)));

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
