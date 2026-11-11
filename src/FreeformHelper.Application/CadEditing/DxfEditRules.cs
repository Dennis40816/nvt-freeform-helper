// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.CadEditing;

/// <summary>Pure normalization and eligibility rules for CAD edits.</summary>
public static class DxfEditRules
{
    /// <summary>Normalizes a rotation to the existing signed half-turn range.</summary>
    public static double NormalizeRotationDegrees(double rotationDegrees)
    {
        var normalized = rotationDegrees % 360.0;
        if (normalized <= -180.0)
        {
            normalized += 360.0;
        }
        else if (normalized > 180.0)
        {
            normalized -= 360.0;
        }

        return Math.Abs(normalized) < 1e-9 ? 0.0 : normalized;
    }

    /// <summary>Compares ordered polygon vertices using the edit tolerance.</summary>
    public static bool ArePolygonVerticesEquivalent(Polygon2 left, Polygon2 right, double epsilon = 1e-6)
    {
        if (left.Vertices.Length != right.Vertices.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Vertices.Length; index++)
        {
            var leftVertex = left.Vertices[index];
            var rightVertex = right.Vertices[index];
            if (Math.Abs(leftVertex.X - rightVertex.X) > epsilon ||
                Math.Abs(leftVertex.Y - rightVertex.Y) > epsilon)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Trims an edit layer name, treating whitespace as empty.</summary>
    public static string NormalizeLayerName(string? layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return string.Empty;
        }

        return layerName.Trim();
    }

    /// <summary>Selects visible baseline pads from the current selection.</summary>
    public static HashSet<int> ResolveRotationSelectedCadIds(
        IEnumerable<int> selectedCadIds, HashSet<int> baselineCadIds, Func<int, bool> isEffectivelyHidden)
    {
        return selectedCadIds
            .Where(id => !isEffectivelyHidden(id))
            .Where(baselineCadIds.Contains)
            .Distinct()
            .ToHashSet();
    }

    /// <summary>Selects visible baseline pads in the requested layer.</summary>
    public static HashSet<int> ResolveRotationTargetLayerCadIds(
        CadPadSet cad, HashSet<int> baselineCadIds, string? rotationLayerName, Func<int, bool> isEffectivelyHidden)
    {
        var targetLayerName = NormalizeLayerName(rotationLayerName);
        if (string.IsNullOrWhiteSpace(targetLayerName))
        {
            return new HashSet<int>();
        }

        return cad.Pads
            .Where(pad => baselineCadIds.Contains(pad.Id))
            .Where(pad => !isEffectivelyHidden(pad.Id))
            .Where(pad => string.Equals(pad.Layer, targetLayerName, StringComparison.OrdinalIgnoreCase))
            .Select(static pad => pad.Id)
            .ToHashSet();
    }
}
