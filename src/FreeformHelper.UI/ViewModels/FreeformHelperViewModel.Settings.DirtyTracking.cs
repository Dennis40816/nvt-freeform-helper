namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// A lazy-initialized HashSet containing the names of properties that, when changed,
    /// should mark the project as having unsaved changes.
    /// </summary>
    private static readonly Lazy<HashSet<string>> DirtySettingNames = new(() => new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(ShowCad),
        nameof(ShowRegular),
        nameof(ColorCadByArea),
        nameof(ShowNotchCanvasPreview),
        nameof(ShowNotchToRegularLabels),
        nameof(XChannels),
        nameof(YChannels),
        nameof(CascadeNum),
        nameof(CoordinatePixelWidth),
        nameof(CoordinatePixelHeight),
        nameof(CoordinatePreferredAaOutlineLayerName),
        nameof(SelectedScanOrder),
        nameof(SelectedScanOrderOption), // This is a UI-specific proxy for SelectedScanOrder, changes to it impact the underlying setting
        nameof(GridAlignmentMode),
        nameof(SelectedGridAlignmentOption), // UI proxy
        nameof(GridPaddingPercent),
        nameof(ActiveAreaWidth),
        nameof(ActiveAreaHeight),
        nameof(PanelBiasX),
        nameof(PanelBiasY),
        nameof(MatchThreshold),
        nameof(FreeformAxisThreshold),
        nameof(EnableAutoDetectXy),
        nameof(EnableFreeformEdgeSpecialization),
        nameof(AutoReplayStep2AfterProjectLoad),
        nameof(CadFillOpacity),
        nameof(CadLineOpacity),
        nameof(RegularFillOpacity),
        nameof(RegularLineOpacity),
        nameof(AreaBucketTolerance),
        nameof(MaxAreaBuckets),
        nameof(GlobalFontSizePercent),
        nameof(UseLocalSizing),
        nameof(IsWidthRowLocal),
        nameof(IsHeightColumnLocal),
        nameof(ImportOnlyClosedPolylines),
        nameof(ImportBlockPolylines),
        nameof(MappingWeightIou),
        nameof(MappingWeightCentroidDistance),
        nameof(MappingWeightAreaRatio),
        nameof(MappingLowConfidenceThreshold),
        nameof(MappingAmbiguousMargin),
        nameof(MappingCandidateNumber),
        nameof(MappingCandidatePaddingCells),
        nameof(CadOutputFwDiffIndexStart),
        nameof(CadOutputFwDiffAutoMode),
        nameof(SelectedCadOutputFwDiffAutoModeOption),
        nameof(CadOutputFwDiffIndexAnchorCadId),
        nameof(EnableV21),
        nameof(EnableV22),
        nameof(LenScale),
        nameof(NullValue),
        nameof(NotchThresholdQ7),
        nameof(NotchThresholdPercent),
        nameof(LinkNotchThresholds),
        nameof(EnableToRegular),
        nameof(EnableToFull),
        nameof(SelectedNotchCompensationModelOption),
        nameof(ToFullStrictOverlapPercent),
        nameof(EnableToFullRuleEngine),
        nameof(EnableToFullRuleTrace),
        nameof(EnableBoundaryVirtualAreaCap),
        nameof(BoundaryVirtualAreaCapPercent),
        nameof(EnableTargetCoverageGuard),
        nameof(TargetCoverageCapPercent),
        nameof(SelectedNotchExportProfileOption),
        nameof(HighlightStrokeWidthAdjust),
        nameof(NotchPreviewAutoPlayEnabled),
        nameof(NotchPreviewAutoPlayIntervalMs)
    });

    private static decimal NormalizeLineOpacity(double value)
    {
        // Historical snapshots could deserialize missing line opacity as 0.
        // Treat non-positive values as default-visible (100%).
        if (value <= 0.0)
        {
            return 1.0m;
        }

        return (decimal)Math.Clamp(value, 0.0, 1.0);
    }
}
