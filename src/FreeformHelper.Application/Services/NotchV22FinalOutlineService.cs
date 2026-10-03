using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Builds Stage 3 (final) outline polygons for Notch 2.2 canvas preview.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance API is intentionally preserved for service call-site stability.")]
public sealed class NotchV22FinalOutlineService
{
    private const double AreaEpsilon = 1e-12;
    private readonly CadPadUnionService _unionService = new();

    /// <summary>
    /// Produces a compact final outline that always contains the CAD body and
    /// expanded To Full region in one visual boundary.
    /// </summary>
    public IReadOnlyList<Polygon2> Build(
        CadPad cadPad,
        IReadOnlyList<Polygon2> appliedToFullPolygons,
        bool isToFullEnabled)
    {
        ArgumentNullException.ThrowIfNull(cadPad);

        if (!isToFullEnabled)
        {
            return Array.Empty<Polygon2>();
        }

        var input = new List<Polygon2>(1 + (appliedToFullPolygons?.Count ?? 0))
        {
            cadPad.Polygon
        };
        AddPolygons(input, appliedToFullPolygons);
        if (input.Count == 0)
        {
            return Array.Empty<Polygon2>();
        }

        var union = CadPadUnionService.Union(input);
        if (union.OuterPolygons.Count > 0)
        {
            return union.OuterPolygons;
        }

        // Fallback: at least keep CAD body visible.
        return new ReadOnlyCollection<Polygon2>(new[] { cadPad.Polygon });
    }

    private static void AddPolygons(List<Polygon2> target, IReadOnlyList<Polygon2>? polygons)
    {
        if (polygons is null || polygons.Count == 0)
        {
            return;
        }

        foreach (var polygon in polygons)
        {
            if (polygon.Area() <= AreaEpsilon)
            {
                continue;
            }

            target.Add(polygon);
        }
    }
}
