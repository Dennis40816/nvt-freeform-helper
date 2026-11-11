using FreeformHelper.Application.CadImport;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.CadImport;

namespace FreeformHelper.UI.Services;

/// <summary>Adapts observable layer inputs to the shared pure visibility rule.</summary>
public sealed class LayerFilterUseCase
{
    public static IReadOnlyList<string> GetLayerNames(CadPadSet cad) => CadLayerVisibilityRules.GetLayerNames(cad);

    public static LayerFilterResult Filter(CadPadSet cad, IReadOnlyCollection<LayerToggle> toggles)
    {
        var result = CadLayerVisibilityRules.Filter(cad, toggles.Select(toggle => new CadLayerSelection(toggle.Name, toggle.IsSelected)).ToArray());
        return new LayerFilterResult(result.VisiblePads, result.AllowedLayers);
    }
}

public sealed record LayerFilterResult(IReadOnlyList<CadPad> VisiblePads, IReadOnlyCollection<string> AllowedLayers);
