// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels.Project;

/// <summary>Live snapshot inputs; constructing readers does not capture workspace values.</summary>
internal sealed class ProjectSnapshotReaders(
    ProjectSnapshotReaders.GridReaders grid,
    ProjectSnapshotReaders.MatchingReaders matching,
    ProjectSnapshotReaders.NotchReaders notch,
    ProjectSnapshotReaders.DisplayReaders display,
    ProjectSnapshotReaders.AppearanceReaders appearance,
    ProjectSnapshotReaders.ViewOptionsReaders viewOptions,
    ProjectSnapshotReaders.ImportReaders import,
    Func<DxfLayerImageExportRequest> imagePreferences,
    Func<IEnumerable<(string Name, bool IsSelected)>> layers)
{
    public GridReaders Grid { get; } = grid;
    public MatchingReaders Matching { get; } = matching;
    public NotchReaders Notch { get; } = notch;
    public DisplayReaders Display { get; } = display;
    public AppearanceReaders Appearance { get; } = appearance;
    public ViewOptionsReaders ViewOptions { get; } = viewOptions;
    public ImportReaders Import { get; } = import;
    public Func<DxfLayerImageExportRequest> ImagePreferences { get; } = imagePreferences;
    public Func<IEnumerable<(string Name, bool IsSelected)>> Layers { get; } = layers;

    public sealed class GridReaders(
        Func<decimal> cascadeCount,
        Func<IEnumerable<decimal>> icX,
        Func<IEnumerable<decimal>> icY,
        Func<decimal> totalX,
        Func<decimal> totalY,
        Func<string?> scanOrderDisplay,
        Func<string> scanOrderFallback,
        Func<string?> alignmentDisplay,
        Func<string> alignmentFallback,
        Func<decimal> paddingPercent,
        Func<decimal> activeAreaWidth,
        Func<decimal> activeAreaHeight,
        Func<double> pitchX,
        Func<double> pitchY)
    {
        public Func<decimal> CascadeCount { get; } = cascadeCount;
        public Func<IEnumerable<decimal>> IcX { get; } = icX;
        public Func<IEnumerable<decimal>> IcY { get; } = icY;
        public Func<decimal> TotalX { get; } = totalX;
        public Func<decimal> TotalY { get; } = totalY;
        public Func<string?> ScanOrderDisplay { get; } = scanOrderDisplay;
        public Func<string> ScanOrderFallback { get; } = scanOrderFallback;
        public Func<string?> AlignmentDisplay { get; } = alignmentDisplay;
        public Func<string> AlignmentFallback { get; } = alignmentFallback;
        public Func<decimal> PaddingPercent { get; } = paddingPercent;
        public Func<decimal> ActiveAreaWidth { get; } = activeAreaWidth;
        public Func<decimal> ActiveAreaHeight { get; } = activeAreaHeight;
        public Func<double> PitchX { get; } = pitchX;
        public Func<double> PitchY { get; } = pitchY;
    }

    public sealed class MatchingReaders(
        Func<UiMatchingSnapshot> persisted,
        Func<decimal> threshold,
        Func<decimal> axisThreshold,
        Func<bool> autoDetectXy,
        Func<bool> edgeSpecialization)
    {
        public Func<UiMatchingSnapshot> Persisted { get; } = persisted;
        public Func<decimal> Threshold { get; } = threshold;
        public Func<decimal> AxisThreshold { get; } = axisThreshold;
        public Func<bool> AutoDetectXy { get; } = autoDetectXy;
        public Func<bool> EdgeSpecialization { get; } = edgeSpecialization;
    }

    public sealed class NotchReaders(
        Func<decimal> lenScale,
        Func<decimal> nullValue,
        Func<decimal> thresholdQ7,
        Func<decimal> thresholdPercent,
        Func<bool> linkThresholds,
        Func<bool> enableToFull)
    {
        public Func<decimal> LenScale { get; } = lenScale;
        public Func<decimal> NullValue { get; } = nullValue;
        public Func<decimal> ThresholdQ7 { get; } = thresholdQ7;
        public Func<decimal> ThresholdPercent { get; } = thresholdPercent;
        public Func<bool> LinkThresholds { get; } = linkThresholds;
        public Func<bool> EnableToFull { get; } = enableToFull;
    }

    public sealed class DisplayReaders(
        Func<bool> showCad,
        Func<bool> showRegular,
        Func<bool> highlightUnmatched,
        Func<bool> highlightFreeform,
        Func<bool> colorCadByArea)
    {
        public Func<bool> ShowCad { get; } = showCad;
        public Func<bool> ShowRegular { get; } = showRegular;
        public Func<bool> HighlightUnmatched { get; } = highlightUnmatched;
        public Func<bool> HighlightFreeform { get; } = highlightFreeform;
        public Func<bool> ColorCadByArea { get; } = colorCadByArea;
    }

    public sealed class AppearanceReaders(
        Func<decimal> cadLineWidth,
        Func<string> cadLineColor,
        Func<decimal> cadFillOpacity,
        Func<decimal> cadLineOpacity,
        Func<decimal> regularLineWidth,
        Func<decimal> highlightStrokeWidthAdjust,
        Func<string> regularLineColor,
        Func<decimal> regularFillOpacity,
        Func<decimal> regularLineOpacity,
        Func<string> regularSelectedColor,
        Func<decimal> regularSelectedFillOpacity)
    {
        public Func<decimal> CadLineWidth { get; } = cadLineWidth;
        public Func<string> CadLineColor { get; } = cadLineColor;
        public Func<decimal> CadFillOpacity { get; } = cadFillOpacity;
        public Func<decimal> CadLineOpacity { get; } = cadLineOpacity;
        public Func<decimal> RegularLineWidth { get; } = regularLineWidth;
        public Func<decimal> HighlightStrokeWidthAdjust { get; } = highlightStrokeWidthAdjust;
        public Func<string> RegularLineColor { get; } = regularLineColor;
        public Func<decimal> RegularFillOpacity { get; } = regularFillOpacity;
        public Func<decimal> RegularLineOpacity { get; } = regularLineOpacity;
        public Func<string> RegularSelectedColor { get; } = regularSelectedColor;
        public Func<decimal> RegularSelectedFillOpacity { get; } = regularSelectedFillOpacity;
    }

    public sealed class ViewOptionsReaders(
        Func<decimal> areaBucketTolerance,
        Func<decimal> maxAreaBuckets,
        Func<bool> showDiffIndexOverlay,
        Func<bool> showNotchCanvasPreview,
        Func<bool> showNotchToRegularLabels,
        Func<decimal> globalFontSizePercent,
        Func<decimal> notchPreviewVisualizationStep,
        Func<bool> notchPreviewAutoPlayEnabled,
        Func<decimal> notchPreviewAutoPlayIntervalMs,
        Func<FreeformHelperViewModel.NotchExportFileType> notchExportFileType,
        Func<decimal> coordinatePixelWidth,
        Func<decimal> coordinatePixelHeight,
        Func<string> coordinatePreferredAaOutlineLayerName)
    {
        public Func<decimal> AreaBucketTolerance { get; } = areaBucketTolerance;
        public Func<decimal> MaxAreaBuckets { get; } = maxAreaBuckets;
        public Func<bool> ShowDiffIndexOverlay { get; } = showDiffIndexOverlay;
        public Func<bool> ShowNotchCanvasPreview { get; } = showNotchCanvasPreview;
        public Func<bool> ShowNotchToRegularLabels { get; } = showNotchToRegularLabels;
        public Func<decimal> GlobalFontSizePercent { get; } = globalFontSizePercent;
        public Func<decimal> NotchPreviewVisualizationStep { get; } = notchPreviewVisualizationStep;
        public Func<bool> NotchPreviewAutoPlayEnabled { get; } = notchPreviewAutoPlayEnabled;
        public Func<decimal> NotchPreviewAutoPlayIntervalMs { get; } = notchPreviewAutoPlayIntervalMs;
        public Func<FreeformHelperViewModel.NotchExportFileType> NotchExportFileType { get; } = notchExportFileType;
        public Func<decimal> CoordinatePixelWidth { get; } = coordinatePixelWidth;
        public Func<decimal> CoordinatePixelHeight { get; } = coordinatePixelHeight;
        public Func<string> CoordinatePreferredAaOutlineLayerName { get; } = coordinatePreferredAaOutlineLayerName;
    }

    public sealed class ImportReaders(
        Func<bool> onlyClosedPolylines,
        Func<bool> includeBlockPolylines,
        Func<string> logLevel,
        Func<string?> maskSourcePath,
        Func<bool> useMask)
    {
        public Func<bool> OnlyClosedPolylines { get; } = onlyClosedPolylines;
        public Func<bool> IncludeBlockPolylines { get; } = includeBlockPolylines;
        public Func<string> LogLevel { get; } = logLevel;
        public Func<string?> MaskSourcePath { get; } = maskSourcePath;
        public Func<bool> UseMask { get; } = useMask;
    }
}
