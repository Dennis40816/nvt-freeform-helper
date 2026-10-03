using Avalonia;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    /// <summary>
    /// Converts a point from world coordinates to screen coordinates.
    /// </summary>
    /// <param name="w">The point in world coordinates.</param>
    /// <returns>The corresponding point in screen coordinates.</returns>
    internal Point WorldToScreen(Point2 w)
    {
        // Apply zoom and pan. Invert Y for screen coordinates (typically Y-down).
        return new Point(w.X * _zoom + _pan.X, -w.Y * _zoom + _pan.Y);
    }

    /// <summary>
    /// Converts a point from screen coordinates to world coordinates.
    /// </summary>
    /// <param name="s">The point in screen coordinates.</param>
    /// <returns>The corresponding point in world coordinates.</returns>
    internal Point2 ScreenToWorld(Point s)
    {
        // Reverse the world-to-screen transformation.
        return new Point2((s.X - _pan.X) / _zoom, -(s.Y - _pan.Y) / _zoom);
    }

    /// <summary>
    /// Calculates the bounding box that encompasses all currently loaded CAD and regular pads.
    /// This cached value is used for operations like FitToContent.
    /// </summary>
    /// <returns>A <see cref="Rect2"/> representing the combined bounds, or null if no pads are loaded.</returns>
    private Rect2? GetWorldBounds()
    {
        if (FitBoundsOverride is { } fitOverride && !fitOverride.IsEmpty)
        {
            return fitOverride;
        }

        // Return cached bounds if available.
        if (_cachedWorldBounds is not null)
        {
            return _cachedWorldBounds;
        }

        var has = false; // Flag to track if any bounds have been included.
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        // Helper local function to include a rectangle in the overall bounds.
        void Include(Rect2 r)
        {
            if (r.IsEmpty) return;
            has = true;
            minX = Math.Min(minX, r.MinX);
            minY = Math.Min(minY, r.MinY);
            maxX = Math.Max(maxX, r.MaxX);
            maxY = Math.Max(maxY, r.MaxY);
        }

        // Include bounds of all CAD pads.
        if (CadPads is { Count: > 0 })
        {
            foreach (var p in CadPads) Include(p.Bounds);
        }
        // Include bounds of all Regular pads.
        if (RegularPads is { Count: > 0 })
        {
            foreach (var p in RegularPads) Include(p.Bounds);
        }

        if (!has)
        {
            _cachedWorldBounds = null;
            return null; // No pads, no bounds.
        }

        // Cache and return the calculated world bounds.
        _cachedWorldBounds = new Rect2(minX, minY, maxX, maxY);
        return _cachedWorldBounds;
    }

    private Rect2? GetSelectionBounds()
    {
        var has = false;
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        void Include(Rect2 r)
        {
            if (r.IsEmpty)
            {
                return;
            }

            has = true;
            minX = Math.Min(minX, r.MinX);
            minY = Math.Min(minY, r.MinY);
            maxX = Math.Max(maxX, r.MaxX);
            maxY = Math.Max(maxY, r.MaxY);
        }

        if (_selectedCadIds.Count > 0 && CadPads is { Count: > 0 })
        {
            foreach (var pad in CadPads)
            {
                if (_selectedCadIds.Contains(pad.Id))
                {
                    Include(pad.Bounds);
                }
            }
        }

        if (_selectedRegIdx.Count > 0 && RegularPads is { Count: > 0 })
        {
            foreach (var pad in RegularPads)
            {
                if (_selectedRegIdx.Contains(pad.Index))
                {
                    Include(pad.Bounds);
                }
            }
        }

        return has ? new Rect2(minX, minY, maxX, maxY) : null;
    }

    /// <summary>
    /// Calculates the center point of a given bounding box.
    /// </summary>
    /// <param name="r">The <see cref="Rect2"/> whose center is to be found.</param>
    /// <returns>A <see cref="Point2"/> representing the center of the rectangle.</returns>
    private static Point2 GetBoundsCenter(Rect2 r)
    {
        return new Point2((r.MinX + r.MaxX) / 2.0, (r.MinY + r.MaxY) / 2.0);
    }

    /// <summary>
    /// Calculates the bounding box that encompasses only the currently loaded regular pads.
    /// </summary>
    /// <returns>A <see cref="Rect2"/> representing the regular pad bounds, or null if no regular pads are loaded.</returns>
    private Rect2? GetRegularBounds()
    {
        if (RegularPads is null || RegularPads.Count == 0) return null;
        var minX = RegularPads.Min(p => p.Bounds.MinX);
        var maxX = RegularPads.Max(p => p.Bounds.MaxX);
        var minY = RegularPads.Min(p => p.Bounds.MinY);
        var maxY = RegularPads.Max(p => p.Bounds.MaxY);
        return new Rect2(minX, minY, maxX, maxY);
    }
}
