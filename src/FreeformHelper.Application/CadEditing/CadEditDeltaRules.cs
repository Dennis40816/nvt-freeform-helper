// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.CadEditing;

/// <summary>Compares accepted CAD with its import baseline through one calculation path.</summary>
public static class CadEditDeltaRules
{
    /// <summary>Counts current pads whose layer differs from the first matching baseline pad.</summary>
    public static int CountRelayeredPads(CadPadSet cad, CadPadSet baselineCad)
    {
        return EnumerateRelayeredPads(cad, baselineCad).Count();
    }

    /// <summary>Counts current pads whose ordered geometry differs from baseline.</summary>
    public static int CountRotatedPads(CadPadSet cad, CadPadSet baselineCad)
    {
        return EnumerateRotatedPads(cad, baselineCad).Count();
    }

    /// <summary>Enumerates current pads with changed baseline layers.</summary>
    public static IEnumerable<CadPad> EnumerateRelayeredPads(CadPadSet cad, CadPadSet baselineCad)
    {
        var baselineLayersByCadId = baselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First().Layer);

        return cad.Pads.Where(pad =>
            baselineLayersByCadId.TryGetValue(pad.Id, out var baselineLayer) &&
            !string.Equals(baselineLayer, pad.Layer, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Enumerates current pads with changed baseline geometry.</summary>
    public static IEnumerable<CadPad> EnumerateRotatedPads(CadPadSet cad, CadPadSet baselineCad)
    {
        var baselinePadsByCadId = baselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        return cad.Pads.Where(pad =>
            baselinePadsByCadId.TryGetValue(pad.Id, out var baselinePad) &&
            !DxfEditRules.ArePolygonVerticesEquivalent(baselinePad.Polygon, pad.Polygon));
    }
}
