using Avalonia.Media;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// The brushes and pens the canvas uses to draw regular pads.
/// </summary>
internal sealed record RegularPadStyle(
    IBrush FillBase,
    IBrush FillSelected,
    IBrush FillFreeform,
    Pen PenBase,
    Pen PenSelected,
    Pen PenFreeform,
    Pen PenUnmatched);

/// <summary>
/// The brushes and pens the canvas uses to draw CAD pads.
/// </summary>
internal sealed record CadPadStyle(
    IBrush FillDefault,
    IBrush FillSelected,
    IBrush FillOverlap,
    Pen Pen,
    Pen PenSelected,
    Pen PenOverlap);

/// <summary>
/// Builds the regular pad style and the CAD pad style of one <see cref="PadCanvas"/> frame.
/// It gets theme resources and the brush cache from the canvas through <see cref="Services"/>.
/// </summary>
internal sealed class PadCanvasPadStyleBuilder
{
    private readonly Services _services;

    public PadCanvasPadStyleBuilder(Services services)
    {
        _services = services;
    }

    /// <summary>
    /// The canvas operations the builder needs. The canvas passes its own methods in.
    /// </summary>
    internal sealed record Services(
        Func<string, Color, Color> GetResourceColor,
        Func<string, double, byte> GetResourceAlpha,
        Func<string, double, double> GetResourceDouble,
        Func<Color, ISolidColorBrush> GetBrush,
        Func<byte, double, byte> ApplyOpacity);

    internal readonly record struct RegularInput(
        Color LineColor,
        Color? SelectedColor,
        double? SelectedFillOpacity,
        double LineWidth,
        double LineOpacity,
        double FillOpacity,
        double SelectedOutlineWidth,
        double HighlightStrokeWidthAdjust);

    internal readonly record struct CadInput(
        Color LineColor,
        double LineWidth,
        double LineOpacity,
        double FillOpacity,
        double SelectedOutlineWidth,
        double SelectedWidthAdd,
        double HighlightStrokeWidthAdjust);

    public RegularPadStyle BuildRegular(RegularInput input)
    {
        var baseColor = input.LineColor;
        var selectedColor = input.SelectedColor ?? _services.GetResourceColor("ColorCanvasRegularSelected", baseColor);
        var freeformColor = _services.GetResourceColor("ColorCanvasRegularFreeform", baseColor);
        var unmatchedColor = _services.GetResourceColor("ColorCanvasRegularUnmatched", baseColor);

        var regFillAlpha = _services.GetResourceAlpha("CanvasRegularFillAlpha", 0.0);
        var regFillSelectedAlpha = input.SelectedFillOpacity.HasValue
            ? (byte)Math.Clamp((int)Math.Round(input.SelectedFillOpacity.Value * 255.0), 0, 255)
            : _services.GetResourceAlpha("CanvasRegularSelectedFillAlpha", 0.0);
        var regFillFreeformAlpha = _services.GetResourceAlpha("CanvasRegularFreeformFillAlpha", 0.0);
        var regStrokeAlpha = _services.GetResourceAlpha("CanvasRegularStrokeAlpha", 0.0);
        var highlightAlpha = _services.GetResourceAlpha("CanvasHighlightStrokeAlpha", regStrokeAlpha);

        // Create semi-transparent fill brushes for regular pads.
        var regBaseFillAlpha = _services.ApplyOpacity(regFillAlpha, input.FillOpacity);
        var regSelectedFillAlpha = _services.ApplyOpacity(regFillSelectedAlpha, input.FillOpacity);
        var regFreeformFillAlpha = _services.ApplyOpacity(regFillFreeformAlpha, input.FillOpacity);
        IBrush regFillBase = regBaseFillAlpha == 0
            ? Brushes.Transparent
            : _services.GetBrush(Color.FromArgb(regBaseFillAlpha, baseColor.R, baseColor.G, baseColor.B));
        IBrush regFillSelected = regSelectedFillAlpha == 0
            ? Brushes.Transparent
            : _services.GetBrush(Color.FromArgb(regSelectedFillAlpha, selectedColor.R, selectedColor.G, selectedColor.B));
        IBrush regFillFreeform = regFreeformFillAlpha == 0
            ? Brushes.Transparent
            : _services.GetBrush(Color.FromArgb(regFreeformFillAlpha, freeformColor.R, freeformColor.G, freeformColor.B));

        // Create pens for regular pads, including different styles for various states.
        var baseStrokeAlpha = _services.ApplyOpacity(regStrokeAlpha, input.LineOpacity);
        var stateStrokeAlpha = _services.ApplyOpacity(highlightAlpha, input.LineOpacity);
        var highlightedRegularWidth = Math.Max(0.05, input.LineWidth + input.HighlightStrokeWidthAdjust);
        var regPenBase = new Pen(_services.GetBrush(Color.FromArgb(baseStrokeAlpha, baseColor.R, baseColor.G, baseColor.B)), input.LineWidth);
        var regPenSelected = new Pen(_services.GetBrush(Color.FromArgb(stateStrokeAlpha, selectedColor.R, selectedColor.G, selectedColor.B)), input.SelectedOutlineWidth);
        // Freeform remains color-coded, but does not get selection-style thick highlight.
        var regPenFreeform = new Pen(_services.GetBrush(Color.FromArgb(baseStrokeAlpha, freeformColor.R, freeformColor.G, freeformColor.B)), input.LineWidth);
        var regPenUnmatched = new Pen(_services.GetBrush(Color.FromArgb(stateStrokeAlpha, unmatchedColor.R, unmatchedColor.G, unmatchedColor.B)), highlightedRegularWidth);

        return new RegularPadStyle(regFillBase, regFillSelected, regFillFreeform, regPenBase, regPenSelected, regPenFreeform, regPenUnmatched);
    }

    public CadPadStyle BuildCad(CadInput input)
    {
        // Create semi-transparent fill brushes for CAD pads.
        var cadFillDefaultColor = _services.GetResourceColor("ColorCanvasCadFillDefault", Colors.Transparent);
        var cadFillSelectedColor = _services.GetResourceColor("ColorCanvasCadFillSelected", Colors.Transparent);
        var cadFillOverlapColor = _services.GetResourceColor("ColorCanvasCadOverlapFill", Colors.Transparent);
        var cadDefaultFillAlpha = _services.ApplyOpacity(cadFillDefaultColor.A, input.FillOpacity);
        var cadSelectedFillAlpha = _services.ApplyOpacity(cadFillSelectedColor.A, input.FillOpacity);
        var cadOverlapFillAlpha = _services.ApplyOpacity(cadFillOverlapColor.A, input.FillOpacity);
        IBrush cadFillDefault = cadDefaultFillAlpha == 0
            ? Brushes.Transparent
            : _services.GetBrush(Color.FromArgb(cadDefaultFillAlpha, cadFillDefaultColor.R, cadFillDefaultColor.G, cadFillDefaultColor.B));
        IBrush cadFillSelected = cadSelectedFillAlpha == 0
            ? Brushes.Transparent
            : _services.GetBrush(Color.FromArgb(cadSelectedFillAlpha, cadFillSelectedColor.R, cadFillSelectedColor.G, cadFillSelectedColor.B));
        IBrush cadFillOverlap = cadOverlapFillAlpha == 0
            ? Brushes.Transparent
            : _services.GetBrush(Color.FromArgb(cadOverlapFillAlpha, cadFillOverlapColor.R, cadFillOverlapColor.G, cadFillOverlapColor.B));

        var cadStrokeAlpha = _services.GetResourceAlpha("CanvasCadStrokeAlpha", 0.0);
        var highlightAlpha = _services.GetResourceAlpha("CanvasHighlightStrokeAlpha", cadStrokeAlpha);
        var cadSelectedColor = _services.GetResourceColor("ColorCanvasCadSelected", input.LineColor);
        var cadOverlapColor = _services.GetResourceColor("ColorCanvasCadOverlapStroke", cadSelectedColor);
        var overlapWidthAdd = _services.GetResourceDouble("CanvasCadOverlapStrokeWidthAdd", input.SelectedWidthAdd);
        var overlapWidth = Math.Max(input.LineWidth + 0.05, input.LineWidth + overlapWidthAdd + input.HighlightStrokeWidthAdjust);

        // Create pens for CAD pads, including a thicker pen for selected state.
        var baseStrokeAlpha = _services.ApplyOpacity(cadStrokeAlpha, input.LineOpacity);
        var selectedStrokeAlpha = _services.ApplyOpacity(highlightAlpha, input.LineOpacity);
        var cadPen = new Pen(_services.GetBrush(Color.FromArgb(baseStrokeAlpha, input.LineColor.R, input.LineColor.G, input.LineColor.B)), input.LineWidth);
        var cadPenSelected = new Pen(_services.GetBrush(Color.FromArgb(selectedStrokeAlpha, cadSelectedColor.R, cadSelectedColor.G, cadSelectedColor.B)), input.SelectedOutlineWidth);
        var cadPenOverlap = new Pen(_services.GetBrush(Color.FromArgb(selectedStrokeAlpha, cadOverlapColor.R, cadOverlapColor.G, cadOverlapColor.B)), overlapWidth);

        return new CadPadStyle(cadFillDefault, cadFillSelected, cadFillOverlap, cadPen, cadPenSelected, cadPenOverlap);
    }
}
