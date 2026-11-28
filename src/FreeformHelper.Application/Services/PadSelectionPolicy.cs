// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>Provides parameter-only axis membership and extend-selection bounds.</summary>
public static class PadSelectionPolicy
{
    public enum Axis { Row, Column, Ic }

    /// <summary>Returns matching regular pad indices in source order, without sorting or deduplicating.</summary>
    public static int[] ResolveAxis(IReadOnlyList<RegularPad> pads, Axis axis, int index)
    {
        var selectedRegIdx = new List<int>();
        switch (axis)
        {
            case Axis.Row:
                foreach (var pad in pads)
                {
                    if (pad.Row == index)
                    {
                        selectedRegIdx.Add(pad.Index);
                    }
                }
                break;
            case Axis.Column:
                foreach (var pad in pads)
                {
                    if (pad.Col == index)
                    {
                        selectedRegIdx.Add(pad.Index);
                    }
                }
                break;
            case Axis.Ic:
                foreach (var pad in pads)
                {
                    if (pad.IcIndex == index)
                    {
                        selectedRegIdx.Add(pad.Index);
                    }
                }
                break;
        }

        return selectedRegIdx.ToArray();
    }

    /// <summary>Returns the rectangle between two world centers, extended by 1.0 world unit on each side.</summary>
    public static Rect2 ExtendBounds(Point2 anchorCenter, Point2 currentCenter)
    {
        var worldRect = new Rect2(
            Math.Min(anchorCenter.X, currentCenter.X),
            Math.Min(anchorCenter.Y, currentCenter.Y),
            Math.Max(anchorCenter.X, currentCenter.X),
            Math.Max(anchorCenter.Y, currentCenter.Y)
        );
        // Inflate slightly to ensure pads whose centers are exactly on the edge are included
        worldRect = new Rect2(worldRect.MinX - 1.0, worldRect.MinY - 1.0, worldRect.MaxX + 1.0, worldRect.MaxY + 1.0);
        return worldRect;
    }
}
