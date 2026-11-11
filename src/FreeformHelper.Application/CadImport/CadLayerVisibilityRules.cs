using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.CadImport;

public static class CadLayerVisibilityRules
{
    public static IReadOnlyList<string> GetLayerNames(CadPadSet cad)
    {
        if (cad is null)
        {
            return Array.Empty<string>();
        }

        return cad.Pads
            .Select(p => p.Layer)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static CadLayerVisibilityResult Filter(CadPadSet cad, IReadOnlyCollection<CadLayerSelection> toggles)
    {
        var allowed = toggles
            .Where(t => t.IsSelected)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filtered = allowed.Count == 0 && toggles.Count > 0
            ? Enumerable.Empty<CadPad>()
            : cad.Pads.Where(p => allowed.Count == 0 || allowed.Contains(p.Layer));

        var list = filtered.ToList();
        return new CadLayerVisibilityResult(list, allowed);
    }
}

public sealed record CadLayerVisibilityResult(IReadOnlyList<CadPad> VisiblePads, IReadOnlyCollection<string> AllowedLayers);
