using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Copies the overlay data that a <see cref="PadCanvas"/> receives through its styled properties into the
/// collections that the canvas draws from. The canvas passes its own collections in, so this class holds no state.
/// </summary>
internal static class PadCanvasOverlayData
{
    /// <summary>
    /// The value range of the simulation overlay. The canvas colors the regular pads with it.
    /// </summary>
    internal readonly record struct SimulationScale(
        double MinValue,
        double MaxValue,
        double MaxAbsDelta,
        double AutoNegativeClampAbs,
        double AutoPositiveClamp);

    /// <summary>
    /// Replaces the content of <paramref name="target"/> with <paramref name="ids"/>.
    /// </summary>
    public static void ReplaceIds(HashSet<int> target, IReadOnlyList<int>? ids)
    {
        target.Clear();
        if (ids is null)
        {
            return;
        }

        foreach (var id in ids)
        {
            target.Add(id);
        }
    }

    /// <summary>
    /// Replaces the Notch preview items and the staged To Full polygons of the enabled items.
    /// </summary>
    public static void ReplaceNotchPreview(
        Dictionary<int, NotchCanvasPreviewItem> byCadId,
        List<Polygon2> seedPolygons,
        List<Polygon2> candidatePolygons,
        IReadOnlyList<NotchCanvasPreviewItem>? items)
    {
        byCadId.Clear();
        seedPolygons.Clear();
        candidatePolygons.Clear();
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            byCadId[item.CadPadId] = item;
            if (!item.IsToFullEnabled)
            {
                continue;
            }

            if (item.ToFullSeedPolygons.Count > 0)
            {
                seedPolygons.AddRange(item.ToFullSeedPolygons);
            }

            if (item.ToFullCandidatePolygons.Count > 0)
            {
                candidatePolygons.AddRange(item.ToFullCandidatePolygons);
            }
        }
    }

    /// <summary>
    /// Replaces the simulation overlay items by regular pad id and returns the value range of the new items.
    /// The range is all zero when there are no items.
    /// </summary>
    public static SimulationScale ReplaceSimulationOverlay<TItem>(
        Dictionary<int, TItem> byRegularPadId,
        IReadOnlyList<TItem>? items,
        Func<TItem, int> regularPadIdOf,
        Func<TItem, double> displayValueOf,
        Func<TItem, double> deltaValueOf)
    {
        byRegularPadId.Clear();
        if (items is not { Count: > 0 })
        {
            return default;
        }

        foreach (var item in items)
        {
            byRegularPadId[regularPadIdOf(item)] = item;
        }

        var autoScaleRange = SimulationColorScaleResolver.ComputeAutoScaleRange(items.Select(displayValueOf));
        return new SimulationScale(
            autoScaleRange.Minimum,
            autoScaleRange.Maximum,
            items.Max(item => Math.Abs(deltaValueOf(item))),
            autoScaleRange.NegativeClampAbs,
            autoScaleRange.PositiveClamp);
    }
}
