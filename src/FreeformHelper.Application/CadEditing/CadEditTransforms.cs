// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.CadEditing;

/// <summary>Pure replacement transforms for accepted CAD documents.</summary>
public static class CadEditTransforms
{
    /// <summary>Replaces selected pad layers and reports the number changed.</summary>
    public static (CadPadSet Cad, int ChangedCount) MoveToLayer(CadPadSet cad, HashSet<int> ids, string layer)
    {
        var changed = 0;
        var pads = new List<CadPad>(cad.Pads.Count);
        foreach (var pad in cad.Pads)
        {
            if (!ids.Contains(pad.Id) || string.Equals(pad.Layer, layer, StringComparison.OrdinalIgnoreCase))
            {
                pads.Add(pad);
                continue;
            }

            pads.Add(new CadPad(pad.Id, pad.Name, layer, pad.Polygon));
            changed++;
        }

        return (changed == 0 ? cad : new CadPadSet(pads), changed);
    }

    /// <summary>Restores matching pad geometry while preserving current names and layers.</summary>
    public static (CadPadSet Cad, int ChangedCount) RestoreGeometry(CadPadSet cad, CadPad baselinePad)
    {
        var changed = 0;
        var pads = new List<CadPad>(cad.Pads.Count);
        foreach (var pad in cad.Pads)
        {
            if (pad.Id != baselinePad.Id || DxfEditRules.ArePolygonVerticesEquivalent(pad.Polygon, baselinePad.Polygon))
            {
                pads.Add(pad);
                continue;
            }

            pads.Add(new CadPad(pad.Id, pad.Name, pad.Layer, baselinePad.Polygon));
            changed++;
        }

        return (changed == 0 ? cad : new CadPadSet(pads), changed);
    }

    /// <summary>Applies saved nonempty layer overrides to current pads.</summary>
    public static CadPadSet ApplyLayerOverrides(CadPadSet cad, IReadOnlyDictionary<int, string> overrides)
    {
        var pads = new List<CadPad>(cad.Pads.Count);
        foreach (var pad in cad.Pads)
        {
            if (!overrides.TryGetValue(pad.Id, out var layer) || string.IsNullOrWhiteSpace(layer) ||
                string.Equals(layer, pad.Layer, StringComparison.OrdinalIgnoreCase))
            {
                pads.Add(pad);
                continue;
            }

            pads.Add(new CadPad(pad.Id, pad.Name, layer, pad.Polygon));
        }

        return new CadPadSet(pads);
    }

    /// <summary>Resolves saved geometry in current-pad order and replaces only matching polygons.</summary>
    public static CadPadSet ApplyGeometryOverrides(CadPadSet cad, Func<int, Polygon2?> resolveGeometry)
    {
        var pads = new List<CadPad>(cad.Pads.Count);
        foreach (var pad in cad.Pads)
        {
            var polygon = resolveGeometry(pad.Id);
            pads.Add(polygon is null ? pad : new CadPad(pad.Id, pad.Name, pad.Layer, polygon));
        }

        return new CadPadSet(pads);
    }
}
