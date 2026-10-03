namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private PadCanvasRenderFrame ResolveRenderFrame()
    {
        var viewFrame = ResolveViewFrameSnapshot();
        var showRegular = ShowRegular && RegularPads is { Count: > 0 };
        var showCad = ShowCad && CadPads is { Count: > 0 };
        var lowDetailZoomThreshold = GetResourceDouble("CanvasLowDetailZoomThreshold", 0.3);
        var lowDetailMode = ComputeLowDetailMode(
            _isMiddlePanning,
            _isSpacePanning,
            _isBoxSelecting,
            _zoom,
            lowDetailZoomThreshold,
            _transientLowDetailUntilTicks,
            DateTime.UtcNow.Ticks);
        var secondaryVisualsDeferred = ShouldDeferSecondaryVisuals();
        var highlightStrokeWidthAdjust = HighlightStrokeWidthAdjust;
        var selectedWidthAdd = GetResourceDouble("CanvasCadSelectedStrokeWidthAdd", 0.8);
        var selectedOutlineBase = Math.Max(CadLineWidth, RegularLineWidth);
        var selectedOutlineWidth = Math.Max(
            selectedOutlineBase + 0.05,
            selectedOutlineBase + selectedWidthAdd + highlightStrokeWidthAdjust);

        return new PadCanvasRenderFrame(
            ViewFrame: viewFrame,
            ShowRegular: showRegular,
            ShowCad: showCad,
            LowDetailMode: lowDetailMode,
            SecondaryVisualsDeferred: secondaryVisualsDeferred,
            LowDetailZoomThreshold: lowDetailZoomThreshold,
            SelectedWidthAdd: selectedWidthAdd,
            CadOpacity: Math.Clamp(CadLineOpacity, 0.0, 1.0),
            RegularOpacity: Math.Clamp(RegularLineOpacity, 0.0, 1.0),
            CadFillOpacity: Math.Clamp(CadFillOpacity, 0.0, 1.0),
            RegularFillOpacity: Math.Clamp(RegularFillOpacity, 0.0, 1.0),
            HighlightStrokeWidthAdjust: highlightStrokeWidthAdjust,
            SelectedOutlineWidth: selectedOutlineWidth);
    }

    private readonly record struct PadCanvasRenderFrame(
        PadCanvasViewFrameSnapshot ViewFrame,
        bool ShowRegular,
        bool ShowCad,
        bool LowDetailMode,
        bool SecondaryVisualsDeferred,
        double LowDetailZoomThreshold,
        double SelectedWidthAdd,
        double CadOpacity,
        double RegularOpacity,
        double CadFillOpacity,
        double RegularFillOpacity,
        double HighlightStrokeWidthAdjust,
        double SelectedOutlineWidth);
}
