using Avalonia;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Places the inline editor of a canvas next to the selected pad.
/// </summary>
internal static class InlineEditorPlacement
{
    /// <summary>
    /// Computes the top-left corner of the editor. The editor sits to the right of the anchor and moves to the
    /// left when it would not fit. It stays inside the canvas with <paramref name="gap"/> on every side.
    /// </summary>
    /// <param name="canvasRect">The canvas bounds in the coordinates of the overlay.</param>
    /// <param name="anchor">The center of the selected pad in the coordinates of the overlay.</param>
    /// <param name="panelSize">The size of the editor.</param>
    /// <param name="gap">The distance to keep from the canvas edges and from the anchor.</param>
    /// <returns>The top-left corner of the editor in the coordinates of the overlay.</returns>
    public static Point Compute(Rect canvasRect, Point anchor, Size panelSize, double gap)
    {
        var minX = canvasRect.Left + gap;
        var maxX = Math.Max(minX, canvasRect.Right - panelSize.Width - gap);
        var x = Math.Min(anchor.X + gap, maxX);
        if (x + panelSize.Width > canvasRect.Right - gap)
        {
            x = Math.Max(minX, anchor.X - gap - panelSize.Width);
        }

        var y = Math.Clamp(
            anchor.Y - panelSize.Height * 0.5,
            canvasRect.Top + gap,
            Math.Max(canvasRect.Top + gap, canvasRect.Bottom - panelSize.Height - gap));
        return new Point(x, y);
    }
}
