namespace FreeformHelper.Infrastructure.Project;

/// <summary>
/// Declares the persistence contract for UI snapshot models.
/// This contract is used by tests to ensure all settable snapshot fields
/// are explicitly tracked and reviewed when the schema evolves.
/// </summary>
public static class UiSnapshotPersistenceContract
{
    /// <summary>
    /// Gets the root snapshot property names that must be persisted.
    /// </summary>
    public static IReadOnlyCollection<string> RootProperties { get; } =
        new[]
        {
            nameof(ProjectUiSnapshot.Grid),
            nameof(ProjectUiSnapshot.Matching),
            nameof(ProjectUiSnapshot.View),
            nameof(ProjectUiSnapshot.Notch),
            nameof(ProjectUiSnapshot.Import),
        };

    /// <summary>
    /// Gets the expected persisted fields for each UI snapshot section type.
    /// </summary>
    public static IReadOnlyDictionary<Type, IReadOnlyCollection<string>> SectionProperties { get; } =
        new Dictionary<Type, IReadOnlyCollection<string>>
        {
            [typeof(UiGridSnapshot)] = new[]
            {
                nameof(UiGridSnapshot.CascadeCount),
                nameof(UiGridSnapshot.IcX),
                nameof(UiGridSnapshot.IcY),
                nameof(UiGridSnapshot.TotalX),
                nameof(UiGridSnapshot.TotalY),
                nameof(UiGridSnapshot.ScanOrder),
                nameof(UiGridSnapshot.AlignmentMode),
                nameof(UiGridSnapshot.GridPaddingPercent),
                nameof(UiGridSnapshot.ActiveAreaWidthMm),
                nameof(UiGridSnapshot.ActiveAreaHeightMm),
                nameof(UiGridSnapshot.PitchXmm),
                nameof(UiGridSnapshot.PitchYmm),
            },
            [typeof(UiMatchingSnapshot)] = new[]
            {
                nameof(UiMatchingSnapshot.MatchMode),
                nameof(UiMatchingSnapshot.MatchThreshold),
                nameof(UiMatchingSnapshot.FreeformAxisThreshold),
                nameof(UiMatchingSnapshot.EnableAutoDetectXy),
                nameof(UiMatchingSnapshot.EnableFreeformEdgeSpecialization),
                nameof(UiMatchingSnapshot.EnableCentroidFallback),
                nameof(UiMatchingSnapshot.NearestK),
            },
            [typeof(UiViewSnapshot)] = new[]
            {
                nameof(UiViewSnapshot.ShowCad),
                nameof(UiViewSnapshot.ShowRegular),
                nameof(UiViewSnapshot.HighlightUnmatched),
                nameof(UiViewSnapshot.HighlightFreeform),
                nameof(UiViewSnapshot.ColorCadByArea),
                nameof(UiViewSnapshot.CadLineWidth),
                nameof(UiViewSnapshot.CadFillOpacity),
                nameof(UiViewSnapshot.CadLineOpacity),
                nameof(UiViewSnapshot.CadLineColor),
                nameof(UiViewSnapshot.RegularLineWidth),
                nameof(UiViewSnapshot.HighlightStrokeWidthAdjust),
                nameof(UiViewSnapshot.RegularFillOpacity),
                nameof(UiViewSnapshot.RegularLineOpacity),
                nameof(UiViewSnapshot.RegularLineColor),
                nameof(UiViewSnapshot.RegularSelectedColor),
                nameof(UiViewSnapshot.RegularSelectedFillOpacity),
                nameof(UiViewSnapshot.AreaBucketTolerance),
                nameof(UiViewSnapshot.MaxAreaBuckets),
                nameof(UiViewSnapshot.LayerSelections),
                nameof(UiViewSnapshot.ShowDiffIndexOverlay),
                nameof(UiViewSnapshot.ShowNotchCanvasPreview),
                nameof(UiViewSnapshot.ShowNotchToRegularLabels),
                nameof(UiViewSnapshot.GlobalFontSizePercent),
                nameof(UiViewSnapshot.NotchPreviewVisualizationStep),
                nameof(UiViewSnapshot.NotchPreviewAutoPlayEnabled),
                nameof(UiViewSnapshot.NotchPreviewAutoPlayIntervalMs),
                nameof(UiViewSnapshot.NotchExportFileType),
                nameof(UiViewSnapshot.DxfLayerImagePreferredLayer),
                nameof(UiViewSnapshot.DxfLayerImageWidthPixels),
                nameof(UiViewSnapshot.DxfLayerImageHeightPixels),
                nameof(UiViewSnapshot.DxfLayerImageLineWidthPixels),
                nameof(UiViewSnapshot.DxfLayerImagePaddingXPixels),
                nameof(UiViewSnapshot.DxfLayerImagePaddingYPixels),
                nameof(UiViewSnapshot.DxfLayerImageFormat),
                nameof(UiViewSnapshot.DxfLayerImageUseDarkTheme),
                nameof(UiViewSnapshot.CoordinatePixelWidth),
                nameof(UiViewSnapshot.CoordinatePixelHeight),
                nameof(UiViewSnapshot.CoordinatePreferredAaOutlineLayerName),
            },
            [typeof(UiNotchSnapshot)] = new[]
            {
                nameof(UiNotchSnapshot.LenScale),
                nameof(UiNotchSnapshot.NullValue),
                nameof(UiNotchSnapshot.ThresholdQ7),
                nameof(UiNotchSnapshot.ThresholdPercentV22),
                nameof(UiNotchSnapshot.LinkThresholds),
                nameof(UiNotchSnapshot.EnableToFull),
            },
            [typeof(UiImportSnapshot)] = new[]
            {
                nameof(UiImportSnapshot.OnlyClosedPolylines),
                nameof(UiImportSnapshot.IncludeBlockPolylines),
                nameof(UiImportSnapshot.LogLevel),
                nameof(UiImportSnapshot.RegularVisibilityMaskSourcePath),
                nameof(UiImportSnapshot.UseRegularVisibilityMask),
            },
            [typeof(LayerSelectionSnapshot)] = new[]
            {
                nameof(LayerSelectionSnapshot.Name),
                nameof(LayerSelectionSnapshot.IsSelected),
            },
        };
}
