using System.Globalization;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// The margins that a fit leaves free around the content.
/// </summary>
/// <param name="Left">The left margin, in screen units.</param>
/// <param name="Top">The top margin, in screen units.</param>
/// <param name="Right">The right margin, in screen units.</param>
/// <param name="Bottom">The bottom margin, in screen units.</param>
internal readonly record struct CanvasFitMargins(double Left, double Top, double Right, double Bottom);

/// <summary>
/// Calculates the margins that <see cref="PadCanvas.FitToContent"/> leaves around the content.
/// The margins make room for the axis labels, the IC group blocks and the overlay controls at the top.
/// </summary>
internal static class CanvasFitMarginCalculator
{
    /// <summary>
    /// Calculates the margins for the given regular pads.
    /// </summary>
    /// <param name="regularPads">The regular pads that the canvas shows. They decide the size of the axis labels.</param>
    /// <param name="overlayTopInset">The height at the top that overlay controls cover. It is added to the top margin.</param>
    /// <param name="resource">Looks up a size token by key and returns the fallback when the token is missing.</param>
    /// <returns>The margins in screen units.</returns>
    public static CanvasFitMargins Calculate(
        IReadOnlyList<RegularPad>? regularPads,
        double overlayTopInset,
        Func<string, double, double> resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var baseMargin = resource("Space20", 20.0); // Base margin around content.
        var topInset = Math.Max(0.0, overlayTopInset);

        if (regularPads is null || regularPads.Count == 0)
        {
            return new CanvasFitMargins(baseMargin, baseMargin + topInset, baseMargin, baseMargin);
        }

        var axisTopMargin = resource("CanvasAxisLabelTopMargin", resource("Space8", 8.0));
        var axisSideMargin = resource("CanvasAxisLabelSideMargin", resource("Space8", 8.0));
        var labelHeight = resource("CanvasAxisLabelBaseFontSize", resource("FontSizeBase", 12.0)); // Approximate height of a label.
        var charWidth = Math.Max(1.0, labelHeight * 0.65); // Approximate width of a character.
        var labelMarginTop = Math.Max(axisTopMargin, labelHeight * 1.1);
        var labelMarginSide = Math.Max(axisSideMargin, labelHeight * 0.8);
        var fitSafetyPadding = resource("Space2", 2.0);
        var blockHeight = Math.Max(
            resource("CanvasIcBlockMinHeight", 18.0),
            labelHeight + resource("CanvasIcBlockHeightExtra", 8.0)); // Height of an IC group block.
        var blockPadding = resource("CanvasIcBlockPadding", resource("Space4", 4.0)); // Padding for IC group block.

        // Calculate left margin based on row labels.
        var minRow = regularPads.Min(p => p.Row);
        var maxRow = regularPads.Max(p => p.Row);
        var maxDisplayRow = maxRow - minRow;
        var rowDigits = Math.Max(1, maxDisplayRow.ToString(CultureInfo.InvariantCulture).Length);
        var rowLabelWidth = Math.Max(labelHeight, rowDigits * charWidth);
        var leftExtra = labelMarginSide + rowLabelWidth + fitSafetyPadding;

        // Calculate top margin based on column labels and potential IC group blocks.
        var topExtra = labelMarginTop + labelHeight + fitSafetyPadding;
        var firstRowPads = regularPads.Where(p => p.Row == minRow).ToList();
        if (firstRowPads.Count > 0)
        {
            var icGroups = firstRowPads.Select(p => p.IcIndex).Distinct().Count();
            if (icGroups > 1)
            {
                topExtra += blockPadding + blockHeight; // Add space for IC group blocks if present.
            }
        }

        return new CanvasFitMargins(baseMargin + leftExtra, baseMargin + topExtra + topInset, baseMargin, baseMargin);
    }
}
