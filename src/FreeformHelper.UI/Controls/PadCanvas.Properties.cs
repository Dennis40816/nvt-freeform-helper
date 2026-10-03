using Avalonia;
using Avalonia.Media;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    // --- Styled Properties ---

    /// <summary>
    /// Defines the <see cref="CadPads"/> AvaloniaProperty.
    /// The collection of CAD pads to be displayed on the canvas.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<CadPad>?> CadPadsProperty =
        AvaloniaProperty.Register<PadCanvas, IReadOnlyList<CadPad>?>(nameof(CadPads));

    /// <summary>
    /// Defines the <see cref="RegularPads"/> AvaloniaProperty.
    /// The collection of regular grid pads to be displayed on the canvas.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<RegularPad>?> RegularPadsProperty =
        AvaloniaProperty.Register<PadCanvas, IReadOnlyList<RegularPad>?>(nameof(RegularPads));

    /// <summary>
    /// Defines the <see cref="FitBoundsOverride"/> AvaloniaProperty.
    /// When set, FitToContent uses this world-bounds rectangle instead of recomputing from CAD/regular pads.
    /// </summary>
    public static readonly StyledProperty<Rect2?> FitBoundsOverrideProperty =
        AvaloniaProperty.Register<PadCanvas, Rect2?>(nameof(FitBoundsOverride));

    /// <summary>
    /// Defines the <see cref="ShowCad"/> AvaloniaProperty.
    /// Controls the visibility of CAD pads.
    /// </summary>
    public static readonly StyledProperty<bool> ShowCadProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowCad), true);

    /// <summary>
    /// Defines the <see cref="ShowRegular"/> AvaloniaProperty.
    /// Controls the visibility of regular pads.
    /// </summary>
    public static readonly StyledProperty<bool> ShowRegularProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowRegular), true);

    /// <summary>
    /// Defines the <see cref="ShowAxisLabels"/> AvaloniaProperty.
    /// Controls whether regular row/column/IC labels are drawn around the grid.
    /// </summary>
    public static readonly StyledProperty<bool> ShowAxisLabelsProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowAxisLabels), true);

    /// <summary>
    /// Defines the <see cref="HighlightUnmatched"/> AvaloniaProperty.
    /// Controls whether unmatched regular pads are highlighted.
    /// </summary>
    public static readonly StyledProperty<bool> HighlightUnmatchedProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(HighlightUnmatched), true);

    /// <summary>
    /// Defines the <see cref="HighlightFreeform"/> AvaloniaProperty.
    /// Controls whether freeform regular pads are highlighted.
    /// </summary>
    public static readonly StyledProperty<bool> HighlightFreeformProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(HighlightFreeform), true);

    /// <summary>
    /// Defines the <see cref="SimulationRegularOverlayItems"/> AvaloniaProperty.
    /// Carries per-regular simulation values for the AA-first Simulation workspace.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<NotchApplySimulationAaDisplayCell>?> SimulationRegularOverlayItemsProperty =
        AvaloniaProperty.Register<PadCanvas, IReadOnlyList<NotchApplySimulationAaDisplayCell>?>(nameof(SimulationRegularOverlayItems));

    /// <summary>
    /// Defines the <see cref="ShowSimulationRegularOverlay"/> AvaloniaProperty.
    /// Controls whether simulation values/colors are rendered on regular pads.
    /// </summary>
    public static readonly StyledProperty<bool> ShowSimulationRegularOverlayProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowSimulationRegularOverlay), false);

    /// <summary>
    /// Defines the <see cref="SimulationRegularOverlayColorMode"/> AvaloniaProperty.
    /// Controls how simulation values are mapped to pad colors.
    /// </summary>
    public static readonly StyledProperty<NotchApplySimulationCanvasColorMode> SimulationRegularOverlayColorModeProperty =
        AvaloniaProperty.Register<PadCanvas, NotchApplySimulationCanvasColorMode>(
            nameof(SimulationRegularOverlayColorMode),
            NotchApplySimulationCanvasColorMode.Auto);

    /// <summary>
    /// Defines the <see cref="SimulationRegularOverlayViewMode"/> AvaloniaProperty.
    /// Controls which simulation scalar is shown on the AA canvas.
    /// </summary>
    public static readonly StyledProperty<NotchApplySimulationCanvasViewMode> SimulationRegularOverlayViewModeProperty =
        AvaloniaProperty.Register<PadCanvas, NotchApplySimulationCanvasViewMode>(
            nameof(SimulationRegularOverlayViewMode),
            NotchApplySimulationCanvasViewMode.Before);

    /// <summary>
    /// Defines the <see cref="SimulationRegularOverlayThresholdValue"/> AvaloniaProperty.
    /// The threshold used when SimulationRegularOverlayColorMode is Threshold.
    /// </summary>
    public static readonly StyledProperty<double> SimulationRegularOverlayThresholdValueProperty =
        AvaloniaProperty.Register<PadCanvas, double>(
            nameof(SimulationRegularOverlayThresholdValue),
            5d);

    /// <summary>
    /// Defines the <see cref="ShowSimulationCopperPillar"/> AvaloniaProperty.
    /// Controls whether the movable copper-pillar footprint is rendered on the simulation canvas.
    /// </summary>
    public static readonly StyledProperty<bool> ShowSimulationCopperPillarProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowSimulationCopperPillar), false);

    /// <summary>
    /// Defines the <see cref="SimulationCopperCenterX"/> AvaloniaProperty.
    /// Copper-pillar center X in world coordinates.
    /// </summary>
    public static readonly StyledProperty<double> SimulationCopperCenterXProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(SimulationCopperCenterX));

    /// <summary>
    /// Defines the <see cref="SimulationCopperCenterY"/> AvaloniaProperty.
    /// Copper-pillar center Y in world coordinates.
    /// </summary>
    public static readonly StyledProperty<double> SimulationCopperCenterYProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(SimulationCopperCenterY));

    /// <summary>
    /// Defines the <see cref="SimulationCopperDiameter"/> AvaloniaProperty.
    /// Copper-pillar diameter in world units.
    /// </summary>
    public static readonly StyledProperty<double> SimulationCopperDiameterProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(SimulationCopperDiameter));

    /// <summary>
    /// Defines the <see cref="ColorCadByArea"/> AvaloniaProperty.
    /// Controls whether CAD pads are colored based on their area.
    /// </summary>
    public static readonly StyledProperty<bool> ColorCadByAreaProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ColorCadByArea), false);

    /// <summary>
    /// Defines the <see cref="MatchThreshold"/> AvaloniaProperty.
    /// The score threshold used for determining if a regular pad is considered matched.
    /// </summary>
    public static readonly StyledProperty<double> MatchThresholdProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(MatchThreshold), 0.35);

    /// <summary>
    /// Defines the <see cref="AreaBucketTolerance"/> AvaloniaProperty.
    /// Tolerance for grouping CAD pads by area for colorization.
    /// </summary>
    public static readonly StyledProperty<double> AreaBucketToleranceProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(AreaBucketTolerance), 0.0);

    /// <summary>
    /// Defines the <see cref="MaxAreaBuckets"/> AvaloniaProperty.
    /// Maximum number of distinct area buckets for colorization.
    /// </summary>
    public static readonly StyledProperty<double> MaxAreaBucketsProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(MaxAreaBuckets), 24);

    /// <summary>
    /// Defines the <see cref="CadLineWidth"/> AvaloniaProperty.
    /// Line width for drawing CAD pads.
    /// </summary>
    public static readonly StyledProperty<double> CadLineWidthProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(CadLineWidth));

    /// <summary>
    /// Defines the <see cref="CadLineColor"/> AvaloniaProperty.
    /// Line color for drawing CAD pads.
    /// </summary>
    public static readonly StyledProperty<Color> CadLineColorProperty =
        AvaloniaProperty.Register<PadCanvas, Color>(nameof(CadLineColor));

    /// <summary>
    /// Defines the <see cref="CadLineOpacity"/> AvaloniaProperty.
    /// Opacity multiplier for CAD pad lines (0-1).
    /// </summary>
    public static readonly StyledProperty<double> CadLineOpacityProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(CadLineOpacity), 1.0);

    /// <summary>
    /// Defines the <see cref="CadFillOpacity"/> AvaloniaProperty.
    /// Opacity multiplier for CAD pad fills (0-1).
    /// </summary>
    public static readonly StyledProperty<double> CadFillOpacityProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(CadFillOpacity), 1.0);

    /// <summary>
    /// Defines the <see cref="RegularLineWidth"/> AvaloniaProperty.
    /// Line width for drawing regular pads.
    /// </summary>
    public static readonly StyledProperty<double> RegularLineWidthProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(RegularLineWidth));

    /// <summary>
    /// Defines the <see cref="HighlightStrokeWidthAdjust"/> AvaloniaProperty.
    /// Adds an adjustable delta (can be negative) to highlight/selection stroke widths.
    /// </summary>
    public static readonly StyledProperty<double> HighlightStrokeWidthAdjustProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(HighlightStrokeWidthAdjust), 0.0);

    /// <summary>
    /// Defines the <see cref="RegularLineColor"/> AvaloniaProperty.
    /// Line color for drawing regular pads.
    /// </summary>
    public static readonly StyledProperty<Color> RegularLineColorProperty =
        AvaloniaProperty.Register<PadCanvas, Color>(nameof(RegularLineColor));

    /// <summary>
    /// Defines the <see cref="RegularLineOpacity"/> AvaloniaProperty.
    /// Opacity multiplier for regular pad lines (0-1).
    /// </summary>
    public static readonly StyledProperty<double> RegularLineOpacityProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(RegularLineOpacity), 1.0);

    /// <summary>
    /// Defines the <see cref="RegularFillOpacity"/> AvaloniaProperty.
    /// Opacity multiplier for regular pad fills (0-1).
    /// </summary>
    public static readonly StyledProperty<double> RegularFillOpacityProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(RegularFillOpacity), 1.0);

    /// <summary>
    /// Defines the <see cref="RegularSelectedColor"/> AvaloniaProperty.
    /// Overrides the color used for selected regular pads (null means use theme token).
    /// </summary>
    public static readonly StyledProperty<Color?> RegularSelectedColorProperty =
        AvaloniaProperty.Register<PadCanvas, Color?>(nameof(RegularSelectedColor));

    /// <summary>
    /// Defines the <see cref="RegularSelectedFillOpacity"/> AvaloniaProperty.
    /// Overrides the fill opacity (0-1) for selected regular pads (null means use theme token).
    /// </summary>
    public static readonly StyledProperty<double?> RegularSelectedFillOpacityProperty =
        AvaloniaProperty.Register<PadCanvas, double?>(nameof(RegularSelectedFillOpacity));

    /// <summary>
    /// Defines the <see cref="HighlightedCadPadIds"/> AvaloniaProperty.
    /// CAD pad IDs in this collection are rendered with overlap-highlight style.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> HighlightedCadPadIdsProperty =
        AvaloniaProperty.Register<PadCanvas, IReadOnlyList<int>?>(nameof(HighlightedCadPadIds));

    /// <summary>
    /// Defines the <see cref="DiffIndexOverrideCadPadIds"/> AvaloniaProperty.
    /// CAD pad IDs in this collection are rendered with diff-idx override hint style.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> DiffIndexOverrideCadPadIdsProperty =
        AvaloniaProperty.Register<PadCanvas, IReadOnlyList<int>?>(nameof(DiffIndexOverrideCadPadIds));

    /// <summary>
    /// Defines the <see cref="DiffIndexAnchorCadPadIds"/> AvaloniaProperty.
    /// CAD pad IDs used as per-IC diff-idx auto-numbering anchors.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> DiffIndexAnchorCadPadIdsProperty =
        AvaloniaProperty.Register<PadCanvas, IReadOnlyList<int>?>(nameof(DiffIndexAnchorCadPadIds));

    /// <summary>
    /// Defines the <see cref="ShowDiffIndexOverlay"/> AvaloniaProperty.
    /// Controls whether diff-idx marker overlays are shown on CAD pads.
    /// </summary>
    public static readonly StyledProperty<bool> ShowDiffIndexOverlayProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowDiffIndexOverlay), true);

    /// <summary>
    /// Defines the <see cref="NotchCanvasPreviewItems"/> AvaloniaProperty.
    /// Carries To Regular ratio and To Full polygon previews for selected CAD pads.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<NotchCanvasPreviewItem>?> NotchCanvasPreviewItemsProperty =
        AvaloniaProperty.Register<PadCanvas, IReadOnlyList<NotchCanvasPreviewItem>?>(nameof(NotchCanvasPreviewItems));

    /// <summary>
    /// Defines the <see cref="ShowNotchCanvasPreview"/> AvaloniaProperty.
    /// Controls whether Notch 2.2 preview overlays are rendered.
    /// </summary>
    public static readonly StyledProperty<bool> ShowNotchCanvasPreviewProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowNotchCanvasPreview), false);

    /// <summary>
    /// Defines the <see cref="ShowNotchToRegularLabels"/> AvaloniaProperty.
    /// Controls whether To Regular ratio labels are rendered on the canvas.
    /// </summary>
    public static readonly StyledProperty<bool> ShowNotchToRegularLabelsProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowNotchToRegularLabels), true);

    /// <summary>
    /// Defines the <see cref="ShowNotchToFullSeedOverlay"/> AvaloniaProperty.
    /// Controls whether Step 3 seed overlay should render on the canvas.
    /// </summary>
    public static readonly StyledProperty<bool> ShowNotchToFullSeedOverlayProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowNotchToFullSeedOverlay), false);

    /// <summary>
    /// Defines the <see cref="ShowNotchToFullCandidateOverlay"/> AvaloniaProperty.
    /// Controls whether Step 3 candidate overlay should render on the canvas.
    /// </summary>
    public static readonly StyledProperty<bool> ShowNotchToFullCandidateOverlayProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowNotchToFullCandidateOverlay), false);

    /// <summary>
    /// Defines the <see cref="ShowNotchToFullFinalOverlay"/> AvaloniaProperty.
    /// Controls whether Step 3 final overlay should render on the canvas.
    /// </summary>
    public static readonly StyledProperty<bool> ShowNotchToFullFinalOverlayProperty =
        AvaloniaProperty.Register<PadCanvas, bool>(nameof(ShowNotchToFullFinalOverlay), false);

    /// <summary>
    /// Defines the <see cref="NotchPreviewStage"/> AvaloniaProperty.
    /// Controls the staged visualization of To Full logic:
    /// 1 = seed (polygon overlap), 2 = boundary candidates, 3 = final To Full overlay.
    /// </summary>
    public static readonly StyledProperty<double> NotchPreviewStageProperty =
        AvaloniaProperty.Register<PadCanvas, double>(nameof(NotchPreviewStage), 3.0);

    /// <summary>
    /// Defines the <see cref="PadMatchLinks"/> AvaloniaProperty.
    /// Stores CAD↔Regular overlap relations used for on-canvas match-ratio labels.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<PadMatchLink>?> PadMatchLinksProperty =
        AvaloniaProperty.Register<PadCanvas, IReadOnlyList<PadMatchLink>?>(nameof(PadMatchLinks));

    // --- Public Properties (Wrappers for Styled Properties) ---

    public IReadOnlyList<CadPad>? CadPads
    {
        get => GetValue(CadPadsProperty);
        set => SetValue(CadPadsProperty, value);
    }

    public IReadOnlyList<RegularPad>? RegularPads
    {
        get => GetValue(RegularPadsProperty);
        set => SetValue(RegularPadsProperty, value);
    }

    public Rect2? FitBoundsOverride
    {
        get => GetValue(FitBoundsOverrideProperty);
        set => SetValue(FitBoundsOverrideProperty, value);
    }

    public bool ShowCad
    {
        get => GetValue(ShowCadProperty);
        set => SetValue(ShowCadProperty, value);
    }

    public bool ShowRegular
    {
        get => GetValue(ShowRegularProperty);
        set => SetValue(ShowRegularProperty, value);
    }

    public bool ShowAxisLabels
    {
        get => GetValue(ShowAxisLabelsProperty);
        set => SetValue(ShowAxisLabelsProperty, value);
    }

    public bool HighlightUnmatched
    {
        get => GetValue(HighlightUnmatchedProperty);
        set => SetValue(HighlightUnmatchedProperty, value);
    }

    public bool HighlightFreeform
    {
        get => GetValue(HighlightFreeformProperty);
        set => SetValue(HighlightFreeformProperty, value);
    }

    public IReadOnlyList<NotchApplySimulationAaDisplayCell>? SimulationRegularOverlayItems
    {
        get => GetValue(SimulationRegularOverlayItemsProperty);
        set => SetValue(SimulationRegularOverlayItemsProperty, value);
    }

    public bool ShowSimulationRegularOverlay
    {
        get => GetValue(ShowSimulationRegularOverlayProperty);
        set => SetValue(ShowSimulationRegularOverlayProperty, value);
    }

    public NotchApplySimulationCanvasColorMode SimulationRegularOverlayColorMode
    {
        get => GetValue(SimulationRegularOverlayColorModeProperty);
        set => SetValue(SimulationRegularOverlayColorModeProperty, value);
    }

    public NotchApplySimulationCanvasViewMode SimulationRegularOverlayViewMode
    {
        get => GetValue(SimulationRegularOverlayViewModeProperty);
        set => SetValue(SimulationRegularOverlayViewModeProperty, value);
    }

    public double SimulationRegularOverlayThresholdValue
    {
        get => GetValue(SimulationRegularOverlayThresholdValueProperty);
        set => SetValue(SimulationRegularOverlayThresholdValueProperty, value);
    }

    public bool ShowSimulationCopperPillar
    {
        get => GetValue(ShowSimulationCopperPillarProperty);
        set => SetValue(ShowSimulationCopperPillarProperty, value);
    }

    public double SimulationCopperCenterX
    {
        get => GetValue(SimulationCopperCenterXProperty);
        set => SetValue(SimulationCopperCenterXProperty, value);
    }

    public double SimulationCopperCenterY
    {
        get => GetValue(SimulationCopperCenterYProperty);
        set => SetValue(SimulationCopperCenterYProperty, value);
    }

    public double SimulationCopperDiameter
    {
        get => GetValue(SimulationCopperDiameterProperty);
        set => SetValue(SimulationCopperDiameterProperty, value);
    }

    public bool ColorCadByArea
    {
        get => GetValue(ColorCadByAreaProperty);
        set => SetValue(ColorCadByAreaProperty, value);
    }

    public double MatchThreshold
    {
        get => GetValue(MatchThresholdProperty);
        set => SetValue(MatchThresholdProperty, value);
    }

    public double AreaBucketTolerance
    {
        get => GetValue(AreaBucketToleranceProperty);
        set => SetValue(AreaBucketToleranceProperty, value);
    }

    public double MaxAreaBuckets
    {
        get => GetValue(MaxAreaBucketsProperty);
        set => SetValue(MaxAreaBucketsProperty, value);
    }

    public double CadLineWidth
    {
        get => GetValue(CadLineWidthProperty);
        set => SetValue(CadLineWidthProperty, value);
    }

    public Color CadLineColor
    {
        get => GetValue(CadLineColorProperty);
        set => SetValue(CadLineColorProperty, value);
    }

    public double CadLineOpacity
    {
        get => GetValue(CadLineOpacityProperty);
        set => SetValue(CadLineOpacityProperty, value);
    }

    public double CadFillOpacity
    {
        get => GetValue(CadFillOpacityProperty);
        set => SetValue(CadFillOpacityProperty, value);
    }

    public double RegularLineWidth
    {
        get => GetValue(RegularLineWidthProperty);
        set => SetValue(RegularLineWidthProperty, value);
    }

    public double HighlightStrokeWidthAdjust
    {
        get => GetValue(HighlightStrokeWidthAdjustProperty);
        set => SetValue(HighlightStrokeWidthAdjustProperty, value);
    }

    public Color RegularLineColor
    {
        get => GetValue(RegularLineColorProperty);
        set => SetValue(RegularLineColorProperty, value);
    }

    public double RegularLineOpacity
    {
        get => GetValue(RegularLineOpacityProperty);
        set => SetValue(RegularLineOpacityProperty, value);
    }

    public double RegularFillOpacity
    {
        get => GetValue(RegularFillOpacityProperty);
        set => SetValue(RegularFillOpacityProperty, value);
    }

    public Color? RegularSelectedColor
    {
        get => GetValue(RegularSelectedColorProperty);
        set => SetValue(RegularSelectedColorProperty, value);
    }

    public double? RegularSelectedFillOpacity
    {
        get => GetValue(RegularSelectedFillOpacityProperty);
        set => SetValue(RegularSelectedFillOpacityProperty, value);
    }

    public IReadOnlyList<int>? HighlightedCadPadIds
    {
        get => GetValue(HighlightedCadPadIdsProperty);
        set => SetValue(HighlightedCadPadIdsProperty, value);
    }

    public IReadOnlyList<int>? DiffIndexOverrideCadPadIds
    {
        get => GetValue(DiffIndexOverrideCadPadIdsProperty);
        set => SetValue(DiffIndexOverrideCadPadIdsProperty, value);
    }

    public IReadOnlyList<int>? DiffIndexAnchorCadPadIds
    {
        get => GetValue(DiffIndexAnchorCadPadIdsProperty);
        set => SetValue(DiffIndexAnchorCadPadIdsProperty, value);
    }

    public bool ShowDiffIndexOverlay
    {
        get => GetValue(ShowDiffIndexOverlayProperty);
        set => SetValue(ShowDiffIndexOverlayProperty, value);
    }

    public IReadOnlyList<NotchCanvasPreviewItem>? NotchCanvasPreviewItems
    {
        get => GetValue(NotchCanvasPreviewItemsProperty);
        set => SetValue(NotchCanvasPreviewItemsProperty, value);
    }

    public bool ShowNotchCanvasPreview
    {
        get => GetValue(ShowNotchCanvasPreviewProperty);
        set => SetValue(ShowNotchCanvasPreviewProperty, value);
    }

    public bool ShowNotchToRegularLabels
    {
        get => GetValue(ShowNotchToRegularLabelsProperty);
        set => SetValue(ShowNotchToRegularLabelsProperty, value);
    }

    public bool ShowNotchToFullSeedOverlay
    {
        get => GetValue(ShowNotchToFullSeedOverlayProperty);
        set => SetValue(ShowNotchToFullSeedOverlayProperty, value);
    }

    public bool ShowNotchToFullCandidateOverlay
    {
        get => GetValue(ShowNotchToFullCandidateOverlayProperty);
        set => SetValue(ShowNotchToFullCandidateOverlayProperty, value);
    }

    public bool ShowNotchToFullFinalOverlay
    {
        get => GetValue(ShowNotchToFullFinalOverlayProperty);
        set => SetValue(ShowNotchToFullFinalOverlayProperty, value);
    }

    public double NotchPreviewStage
    {
        get => GetValue(NotchPreviewStageProperty);
        set => SetValue(NotchPreviewStageProperty, value);
    }

    public IReadOnlyList<PadMatchLink>? PadMatchLinks
    {
        get => GetValue(PadMatchLinksProperty);
        set => SetValue(PadMatchLinksProperty, value);
    }
}
