using FreeformHelper.Infrastructure.Project;
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
            logLevel: () => SelectedLogLevelOption.Value, maskSourcePath: () => RegularMask.Workflow.SourcePath,
            useMask: () => RegularMask.IsEnabled),
        imagePreferences: () => _dxfLayerImageExportPreferences,
        layers: () => LayerToggles.Select(t => (t.Name, t.IsSelected)));

    private void ApplyViewSnapshot(UiViewSnapshot snapshot) => _settingsSnapshotMapper.ApplyViewSnapshot(snapshot);

    private void ApplyImportSnapshot(UiImportSnapshot snapshot, ProjectFile? projectFile = null) =>
        _settingsSnapshotMapper.ApplyImportSnapshot(snapshot,
            projectFile?.EmbeddedRegularVisibilityMask, projectFile?.EmbeddedRegularVisibilityMaskName);

    /// <summary>
    /// Determines if the application should prompt the user to save before exiting.
    /// This is true if there are unsaved changes or loaded CAD content has never been saved.
    /// </summary>
    /// <returns>True if a save prompt is needed, false otherwise.</returns>
    public bool ShouldPromptSaveOnExit()
    {
        return _projectSession.ShouldPromptSaveOnExit();
    }
}
