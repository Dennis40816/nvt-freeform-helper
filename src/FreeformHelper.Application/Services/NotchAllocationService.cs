using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Computes per-CAD allocation ratios against regular pads for notch preview and validation.
/// </summary>
public sealed class NotchAllocationService
{
    /// <summary>
    /// Builds allocation ratios (cad overlap / cad area) for a specific CAD pad.
    /// </summary>
    public static IReadOnlyList<NotchAllocation> BuildAllocations(CadPad cad, RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);

        var list = new List<NotchAllocation>();
        var cadArea = Math.Max(cad.Area, 1e-12);
        foreach (var reg in RegularGridCandidateQuery.QueryByBounds(grid, cad.Bounds))
        {
            if (!TryComputeAllocation(cad, reg, cadArea, out var ratio, out var q7))
            {
                continue;
            }

            list.Add(new NotchAllocation(reg, ratio, q7));
        }

        return list.OrderByDescending(a => a.Ratio).ToList();
    }

    /// <summary>
    /// Determines whether a CAD pad has a Q7-positive allocation on the specified IC.
    /// </summary>
    public static bool HasQ7PositiveAllocationInIc(CadPad cad, RegularGrid grid, int icIndex)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);

        var cadArea = Math.Max(cad.Area, 1e-12);
        foreach (var reg in RegularGridCandidateQuery.QueryByBounds(grid, cad.Bounds))
        {
            if (reg.IcIndex == icIndex &&
                TryComputeAllocation(cad, reg, cadArea, out _, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryComputeAllocation(
        CadPad cad,
        RegularPad regular,
        double cadArea,
        out double ratio,
        out int q7)
    {
        var overlap = Polygon2.IntersectionAreaWithRect(cad.Polygon, regular.Bounds);
        ratio = overlap / cadArea;
        q7 = overlap > 0 ? NotchThresholdQ7Contract.EncodeFraction(ratio) : 0;
        return q7 > 0;
    }

    /// <summary>
    /// Determines the freeform type for a CAD pad by using the highest-score matched regular pad.
    /// </summary>
    public static FreeformType GetFreeformType(CadPad cad, RegularGrid grid)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);

        var matches = grid.Pads
            .Where(p => p.MatchedCadPadId == cad.Id)
            .ToList();

        if (matches.Count == 0)
        {
            return FreeformType.None;
        }

        var anchor = matches.OrderByDescending(p => p.MatchScore).First();
        return anchor.Freeform;
    }
}

/// <summary>
/// Represents a single CAD-to-regular allocation entry.
/// </summary>
public sealed record NotchAllocation(RegularPad Pad, double Ratio, int Q7);
