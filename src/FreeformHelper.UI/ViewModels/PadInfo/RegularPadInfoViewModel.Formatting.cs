using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class RegularPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    /// <summary>
    /// Helper method to format a collection of integers into formatted index ranges (e.g., "1, 3-5, 7").
    /// </summary>
    private static string FormatIndexRanges(IEnumerable<int> indices)
    {
        var list = indices.Distinct().OrderBy(i => i).ToList();
        if (list.Count == 0) return "-";

        var parts = new List<string>();
        var start = list[0];
        var prev = list[0];

        for (var i = 1; i < list.Count; i++)
        {
            var current = list[i];
            if (current == prev + 1)
            {
                prev = current;
                continue;
            }

            parts.Add(start == prev ? $"{start}" : $"{start}-{prev}");
            start = current;
            prev = current;
        }

        parts.Add(start == prev ? $"{start}" : $"{start}-{prev}");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Helper method to union the bounding boxes of a collection of rectangles.
    /// </summary>
    private static Rect2 UnionBounds(IEnumerable<Rect2> rects)
    {
        Rect2? bounds = null;
        foreach (var r in rects)
        {
            bounds = bounds is null ? r : Rect2.Union(bounds.Value, r);
        }
        return bounds ?? new Rect2(0, 0, 0, 0);
    }

    /// <summary>
    /// Helper method to format a <see cref="Rect2" /> into a human-readable string.
    /// </summary>
    private static string FormatBounds(Rect2 bounds)
    {
        return $"X {bounds.MinX:0.###} - {bounds.MaxX:0.###}\nY {bounds.MinY:0.###} - {bounds.MaxY:0.###}";
    }

    /// <summary>
    /// Helper method to format a <see cref="Point2" /> into a human-readable string.
    /// </summary>
    private static string FormatPoint(Point2 p)
    {
        return $"{p.X:0.####}, {p.Y:0.####}";
    }
}
