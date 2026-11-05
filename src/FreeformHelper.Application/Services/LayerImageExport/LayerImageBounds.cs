// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

/// <summary>Computes the common pad bounds used by sizing, preview and export.</summary>
public static class LayerImageBounds
{
    public static Rect2 Compute(IReadOnlyList<LayerImageLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        Rect2? bounds = null;
        foreach (var layer in layers)
        {
            foreach (var pad in layer.Pads)
            {
                bounds = bounds is { } previous ? Rect2.Union(previous, pad.Bounds) : pad.Bounds;
            }
        }

        return bounds.GetValueOrDefault();
    }
}
