// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace FreeformHelper.Application.Services;

internal static class GridCellLookup
{
    internal static int FindCellIndex(IReadOnlyList<double> edges, double coord, int cellCount)
    {
        if (cellCount <= 1 || edges.Count < 2)
        {
            return 0;
        }

        if (coord <= edges[0]) return 0;
        if (coord >= edges[^1]) return cellCount - 1;

        var lo = 0;
        var hi = edges.Count - 1;
        while (lo + 1 < hi)
        {
            var mid = (lo + hi) / 2;
            if (coord < edges[mid])
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }

        return Math.Clamp(lo, 0, cellCount - 1);
    }
}
