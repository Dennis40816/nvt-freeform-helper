using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Services;

public enum CoordinatePlannerActiveAreaSourceKind
{
    RegularGridBounds,
    LayerBounds,
    LayerBoundsFallbackNoCadPads,
    LayerBoundsFallbackLayerMissing,
}

public sealed record CoordinatePlannerActiveAreaResolution(
    Rect2 Bounds,
    CoordinatePlannerActiveAreaSourceKind SourceKind,
    string SourceText,
    string? RequestedLayerName,
    string? ResolvedLayerName);

public static class CoordinatePlannerActiveAreaResolver
{
    private const string DefaultActiveAreaLayerName = "AA.drawing";

    public static CoordinatePlannerActiveAreaResolution Resolve(
        CoordinatePlannerWorkspaceSession session,
        string? requestedLayerName)
    {
        ArgumentNullException.ThrowIfNull(session);

        var normalizedLayerName = requestedLayerName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLayerName))
        {
            return new CoordinatePlannerActiveAreaResolution(
                session.Grid.Bounds,
                CoordinatePlannerActiveAreaSourceKind.RegularGridBounds,
                "AA outline: regular grid bounds",
                RequestedLayerName: null,
                ResolvedLayerName: null);
        }

        var matchingPads = session.CadPads
            .Where(pad => string.Equals(pad.Layer, normalizedLayerName, StringComparison.Ordinal))
            .ToArray();
        if (matchingPads.Length > 0)
        {
            return new CoordinatePlannerActiveAreaResolution(
                matchingPads.Select(static pad => pad.Bounds).Aggregate(Rect2.Union),
                CoordinatePlannerActiveAreaSourceKind.LayerBounds,
                $"AA outline: layer {normalizedLayerName}",
                RequestedLayerName: normalizedLayerName,
                ResolvedLayerName: normalizedLayerName);
        }

        var resolvedLayerName = session.VisibleLayerNames.FirstOrDefault(layerName =>
            string.Equals(layerName, normalizedLayerName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(resolvedLayerName))
        {
            return new CoordinatePlannerActiveAreaResolution(
                session.Grid.Bounds,
                CoordinatePlannerActiveAreaSourceKind.LayerBoundsFallbackNoCadPads,
                $"AA outline: regular grid bounds (selected layer {normalizedLayerName} has no CAD pads)",
                RequestedLayerName: normalizedLayerName,
                ResolvedLayerName: resolvedLayerName);
        }

        return new CoordinatePlannerActiveAreaResolution(
            session.Grid.Bounds,
            CoordinatePlannerActiveAreaSourceKind.LayerBoundsFallbackLayerMissing,
            $"AA outline: regular grid bounds (selected layer {normalizedLayerName} not found)",
            RequestedLayerName: normalizedLayerName,
            ResolvedLayerName: null);
    }

    public static string? ResolvePreferredLayerName(
        IReadOnlyCollection<string> layerNames,
        string? preferredLayerName)
    {
        ArgumentNullException.ThrowIfNull(layerNames);
        if (layerNames.Count == 0)
        {
            return null;
        }

        var normalizedPreferred = preferredLayerName?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPreferred))
        {
            var preferredMatch = layerNames.FirstOrDefault(layerName =>
                string.Equals(layerName, normalizedPreferred, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(preferredMatch))
            {
                return preferredMatch;
            }
        }

        return layerNames.FirstOrDefault(layerName =>
            string.Equals(layerName, DefaultActiveAreaLayerName, StringComparison.OrdinalIgnoreCase));
    }
}
