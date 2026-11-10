// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>Provides parameter-only bounds and cell calculations for pad spatial queries.</summary>
public static class PadSpatialQueryPolicy
{
    private const double EdgeEpsilon = 1e-6;

    /// <summary>Returns the union of CAD pad bounds in source order.</summary>
    public static Rect2 BuildBounds(IReadOnlyList<CadPad> pads)
    {
        if (pads.Count == 0)
        {
            return new Rect2(0, 0, 0, 0);
        }

        var bounds = pads[0].Bounds;
        for (var i = 1; i < pads.Count; i++)
        {
            bounds = Rect2.Union(bounds, pads[i].Bounds);
        }

        return bounds;
    }

    /// <summary>Returns the clamped CAD bucket column for a world X coordinate.</summary>
    public static int GetColumn(double x, Rect2 bounds, int cols, double cellWidth)
    {
        if (cols <= 1)
        {
            return 0;
        }

        if (x <= bounds.MinX)
        {
            return 0;
        }

        if (x >= bounds.MaxX)
        {
            return cols - 1;
        }

        var normalized = (x - bounds.MinX) / cellWidth;
        return Math.Clamp((int)Math.Floor(normalized), 0, cols - 1);
    }

    /// <summary>Returns the clamped CAD bucket row for a world Y coordinate.</summary>
    public static int GetRow(double y, Rect2 bounds, int rows, double cellHeight)
    {
        if (rows <= 1)
        {
            return 0;
        }

        if (y <= bounds.MinY)
        {
            return 0;
        }

        if (y >= bounds.MaxY)
        {
            return rows - 1;
        }

        var normalized = (y - bounds.MinY) / cellHeight;
        return Math.Clamp((int)Math.Floor(normalized), 0, rows - 1);
    }

    /// <summary>Checks that each pad shares its row and column bounds within the existing edge tolerance.</summary>
    public static bool ValidateSeparableGrid(
        IReadOnlyList<RegularPad> pads,
        int minRow,
        int minCol,
        double[] colMin,
        double[] colMax,
        double[] rowMin,
        double[] rowMax)
    {
        foreach (var pad in pads)
        {
            var r = pad.Row - minRow;
            var c = pad.Col - minCol;
            if ((uint)r >= (uint)rowMin.Length || (uint)c >= (uint)colMin.Length)
            {
                return false;
            }

            var bounds = pad.Bounds;
            if (Math.Abs(bounds.MinX - colMin[c]) > EdgeEpsilon ||
                Math.Abs(bounds.MaxX - colMax[c]) > EdgeEpsilon ||
                Math.Abs(bounds.MinY - rowMin[r]) > EdgeEpsilon ||
                Math.Abs(bounds.MaxY - rowMax[r]) > EdgeEpsilon)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the bucket (cell index) within a set of sorted edges that contains a given value.
    /// </summary>
    /// <param name="edges">Sorted array of edge coordinates.</param>
    /// <param name="v">The value to locate.</param>
    /// <returns>The index of the bucket, or -1 if the value is outside the range of edges.</returns>
    public static int FindBucket(double[] edges, double v)
    {
        // Returns i such that edges[i] <= v <= edges[i+1].
        if (v < edges[0] || v > edges[^1]) return -1; // Value is outside the total range.

        var idx = Array.BinarySearch(edges, v);
        if (idx >= 0)
        {
            // If value is exactly on an edge, prefer the bucket to its left (for discrete indexing).
            // Handle the case where it's the very last edge specifically.
            return Math.Clamp(idx == edges.Length - 1 ? idx - 1 : idx, 0, edges.Length - 2);
        }

        // If not exact match, bitwise complement of 'idx' gives insertion point.
        // This insertion point is effectively the right edge of the bucket containing 'v'.
        idx = ~idx;
        return Math.Clamp(idx - 1, 0, edges.Length - 2);
    }

    /// <summary>
    /// Finds the first cell (inclusive) in the grid along an axis that is intersected by a given minimum coordinate.
    /// </summary>
    public static int FindFirstCell(double[] edges, double min)
    {
        if (min <= edges[0]) return 0; // If min is before or at the first edge, it's the first cell.
        if (min >= edges[^1]) return edges.Length - 2; // If min is beyond the last edge, it's the last cell.

        var idx = Array.BinarySearch(edges, min);
        if (idx < 0) idx = ~idx; // If not found, get insertion point.
        return Math.Clamp(idx - 1, 0, edges.Length - 2); // Return the cell index.
    }

    /// <summary>
    /// Finds the last cell (inclusive) in the grid along an axis that is intersected by a given maximum coordinate.
    /// </summary>
    public static int FindLastCell(double[] edges, double max)
    {
        if (max <= edges[0]) return 0; // If max is before or at the first edge, it's the first cell.
        if (max >= edges[^1]) return edges.Length - 2; // If max is beyond the last edge, it's the last cell.

        var idx = Array.BinarySearch(edges, max);
        if (idx < 0) idx = ~idx - 1; // If not found, get insertion point and adjust for 'last' cell.
        return Math.Clamp(idx, 0, edges.Length - 2); // Return the cell index.
    }
}
