using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Computes layer filter results for CAD pads based on layer toggles.
/// </summary>
public sealed class LayerFilterUseCase
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

    public static LayerFilterResult Filter(CadPadSet cad, IReadOnlyCollection<FreeformHelperViewModel.LayerToggle> toggles)
    {
        var allowed = toggles
            .Where(t => t.IsSelected)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filtered = allowed.Count == 0 && toggles.Count > 0
            ? Enumerable.Empty<CadPad>()
            : cad.Pads.Where(p => allowed.Count == 0 || allowed.Contains(p.Layer));

        var list = filtered.ToList();
        return new LayerFilterResult(list, allowed);
    }
}

public sealed record LayerFilterResult(
    IReadOnlyList<CadPad> VisiblePads,
    IReadOnlyCollection<string> AllowedLayers);
