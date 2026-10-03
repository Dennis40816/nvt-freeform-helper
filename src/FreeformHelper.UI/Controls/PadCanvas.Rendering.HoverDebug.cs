using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private void DrawHoverDebugOverlay(DrawingContext context)
    {
        var snapshot = BuildHoverDebugSnapshot();
        if (snapshot is null)
        {
            return;
        }

        var textColor = GetResourceColor("ColorCanvasMatchRatioLabel", GetResourceColor("ColorCanvasAxisLabel"));
        var backgroundColor = snapshot.Value.IsRegularMode
            ? GetResourceColor("ColorCanvasNotchRatioBackground", GetResourceColor("ColorCanvasMatchRatioBackground"))
            : GetResourceColor("ColorCanvasMatchRatioBackground", Colors.Transparent);
        var borderColor = snapshot.Value.IsRegularMode
            ? GetResourceColor("ColorCanvasNotchRatioBorder", GetResourceColor("ColorCanvasMatchRatioBorder"))
            : GetResourceColor("ColorCanvasMatchRatioBorder", GetResourceColor("ColorCanvasCadSelected"));
        var fontSize = Math.Clamp(
            GetResourceDouble("CanvasMatchRatioFontSize", GetResourceDouble("FontSizeSm", 11)),
            GetResourceDouble("FontSizeSm", 11),
            GetResourceDouble("FontSizeMd", 13));
        var paddingX = GetResourceDouble("CanvasMatchRatioPaddingX", 6);
        var paddingY = GetResourceDouble("CanvasMatchRatioPaddingY", 3);
        var cornerRadius = GetResourceDouble("CanvasMatchRatioCornerRadius", GetResourceDouble("RadiusSm", 6));
        var borderThickness = GetResourceDouble("CanvasMatchRatioBorderThickness", 1);
        var offset = Math.Max(
            4,
            GetResourceDouble("CanvasNotchRatioOffsetY", GetResourceDouble("CanvasMatchRatioOffsetY", 4)));

        var layout = new TextLayout(snapshot.Value.Text, AxisLabelTypeface, fontSize, GetBrush(textColor));
        var width = layout.Width + (paddingX * 2);
        var height = layout.Height + (paddingY * 2);
        var anchor = WorldToScreen(snapshot.Value.Anchor);
        var proposed = new Rect(anchor.X + offset, anchor.Y - height - offset, width, height);
        var viewport = new Rect(Bounds.Size);
        var rect = ClampToViewport(proposed, viewport);

        var background = GetBrush(backgroundColor);
        var stroke = new Pen(GetBrush(borderColor), borderThickness);
        context.DrawRectangle(background, stroke, new RoundedRect(rect, cornerRadius));
        layout.Draw(context, new Point(rect.X + paddingX, rect.Y + paddingY));
    }
}
