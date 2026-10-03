using Avalonia;
using Avalonia.Media;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    /// <summary>
    /// Draws a single regular pad onto the canvas, applying appropriate styling based on its state.
    /// </summary>
    private void DrawRegularPad(
        DrawingContext context,
        RegularPad pad,
        bool isSelected,
        IBrush regFillBase,
        IBrush regFillSelected,
        IBrush regFillFreeform,
        Pen regPenBase,
        Pen regPenSelected,
        Pen regPenFreeform,
        Pen regPenUnmatched)
    {
        var isFreeformPad = ShouldDrawFreeformHatch(pad);
        IBrush fill = regFillBase;
        Pen pen = regPenBase;
        var drawFreeformHatch = isFreeformPad;
        var suppressFreeformAccentBorder = isFreeformPad && ShouldSuppressFreeformAccentBorderForNotchPreview();
        var simulationFill = TryBuildSimulationRegularFillBrush(pad);

        if (simulationFill is not null)
        {
            fill = simulationFill;
        }

        // Apply selected styling.
        if (isSelected)
        {
            fill = simulationFill ?? regFillSelected;
            pen = regPenSelected;
        }
        // Apply freeform styling if enabled and pad is freeform.
        else if (isFreeformPad)
        {
            if (!suppressFreeformAccentBorder)
            {
                fill = simulationFill ?? regFillFreeform;
                pen = regPenFreeform;
            }
        }
        // Apply unmatched styling if enabled and pad is unmatched.
        else if (HighlightUnmatched && (pad.MatchedCadPadId is null || pad.MatchScore < MatchThreshold))
        {
            pen = regPenUnmatched;
        }

        // Get the bounding box of the regular pad and draw it.
        var b = pad.Bounds;
        var rect = new Rect(b.MinX, b.MinY, b.Width, b.Height); // Avalonia Rect from domain Rect2.
        context.DrawRectangle(fill, pen, rect);
        if (drawFreeformHatch)
        {
            DrawRegularFreeformHatch(context, rect, pad.Freeform);
        }
    }

    private bool ShouldDrawFreeformHatch(RegularPad pad)
    {
        return HighlightFreeform && pad.Freeform != FreeformType.None;
    }

    private void DrawRegularFreeformHatch(DrawingContext context, Rect rect, FreeformType freeformType)
    {
        var zoom = Math.Max(_zoom, 1e-6);
        var spacingScreen = GetResourceDouble("CanvasRegularFreeformHatchSpacing", 8.0);
        var widthScreen = GetResourceDouble("CanvasRegularFreeformHatchWidth", 1.2);
        var spacingWorld = Math.Max(spacingScreen / zoom, 1e-6);
        var widthWorld = Math.Max(widthScreen / zoom, 1e-6);
        var opacity = Math.Clamp(RegularLineOpacity, 0.0, 1.0);

        var primary = GetResourceColor("ColorCanvasRegularFreeformHatchPrimary", Colors.Red);
        var secondary = GetResourceColor("ColorCanvasRegularFreeformHatchSecondary", Colors.Green);
        var primaryAlpha = ApplyOpacity(primary.A, opacity);
        var secondaryAlpha = ApplyOpacity(secondary.A, opacity);
        var primaryPen = new Pen(GetBrush(Color.FromArgb(primaryAlpha, primary.R, primary.G, primary.B)), widthWorld);
        var secondaryPen = new Pen(GetBrush(Color.FromArgb(secondaryAlpha, secondary.R, secondary.G, secondary.B)), widthWorld);

        using (context.PushClip(rect))
        {
            if (freeformType is FreeformType.XWay or FreeformType.XYWay)
            {
                DrawDiagonalHatch(context, rect, primaryPen, spacingWorld, downward: true);
            }

            if (freeformType is FreeformType.YWay or FreeformType.XYWay)
            {
                DrawDiagonalHatch(context, rect, secondaryPen, spacingWorld, downward: false);
            }
        }
    }

    private static void DrawDiagonalHatch(DrawingContext context, Rect rect, Pen pen, double spacing, bool downward)
    {
        if (spacing <= 0)
        {
            return;
        }

        var height = rect.Height;
        if (height <= 0 || rect.Width <= 0)
        {
            return;
        }

        var startX = rect.Left - height;
        var endX = rect.Right + height;
        for (var x = startX; x <= endX; x += spacing)
        {
            if (downward)
            {
                var from = new Point(x, rect.Top);
                var to = new Point(x + height, rect.Bottom);
                context.DrawLine(pen, from, to);
            }
            else
            {
                var from = new Point(x, rect.Bottom);
                var to = new Point(x + height, rect.Top);
                context.DrawLine(pen, from, to);
            }
        }
    }

    /// <summary>
    /// Placeholder for drawing additional selection overlays (e.g., selection handles, detailed info).
    /// Currently, it performs no operation.
    /// </summary>
    private static void DrawSelectionOverlay(DrawingContext context)
    {
        _ = context;
        // Intentional no-op for now. (Hook for future overlays.)
    }
}
