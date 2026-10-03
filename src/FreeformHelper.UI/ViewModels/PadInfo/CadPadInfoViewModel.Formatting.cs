using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class CadPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    private static string BuildCadMatchText(
        List<int> regularIds,
        IReadOnlyList<PadMatchLink> links,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        if (regularIds.Count == 0)
        {
            return "Unmatched";
        }

        var ordered = links
            .Select(link => link.RegularPadId)
            .Concat(regularIds)
            .Distinct()
            .ToList();
        var bestRegularId = ordered[0];
        var bestText = BuildRegularMatchKey(bestRegularId, getRegularPadIcDiff);
        if (ordered.Count == 1)
        {
            return bestText;
        }

        return $"{bestText} (+{ordered.Count - 1})";
    }

    private static string BuildCadMatchDetailsText(
        List<int> regularIds,
        List<PadMatchLink> links,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        if (regularIds.Count == 0)
        {
            return "Unmatched";
        }

        if (links.Count == 0)
        {
            var texts = regularIds
                .Select(regularId => BuildRegularMatchDetail(regularId, getRegularPadIcDiff))
                .ToList();
            return $"Regular: {string.Join(", ", texts)}";
        }

        var lines = links
            .Select(link => $"{BuildRegularMatchDetail(link.RegularPadId, getRegularPadIcDiff)}: {(link.CadCoverage * 100):0.#}%")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildRegularMatchKey(
        int regularPadId,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        var match = getRegularPadIcDiff?.Invoke(regularPadId);
        return match.HasValue
            ? $"IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex}"
            : $"regId {regularPadId}";
    }

    private static string BuildRegularMatchDetail(
        int regularPadId,
        Func<int, (int IcIndex, int DiffIndex)?>? getRegularPadIcDiff)
    {
        var match = getRegularPadIcDiff?.Invoke(regularPadId);
        return match.HasValue
            ? $"IC{match.Value.IcIndex + 1}/diff{match.Value.DiffIndex} (regId {regularPadId})"
            : $"regId {regularPadId}";
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
