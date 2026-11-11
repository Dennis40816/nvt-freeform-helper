namespace FreeformHelper.Application.CadImport;

/// <summary>Preserves catalog category membership and fallback metadata.</summary>
public static class CadLayerCategoryRules
{
    public static bool Matches(DxfLayerCategory category, bool isKnown, bool hasPolyline, bool hasBlock, bool hasText)
    {
        if (!isKnown) return category == DxfLayerCategory.Polyline;
        return category switch
        {
            DxfLayerCategory.Polyline => hasPolyline,
            DxfLayerCategory.Block => hasBlock,
            DxfLayerCategory.Text => hasText,
            _ => false,
        };
    }

    public static (string Display, string Description) GetMetadata(DxfLayerCategory category) => category switch
    {
        DxfLayerCategory.Polyline => ("Polyline", "Layers containing LWPOLYLINE/POLYLINE"),
        DxfLayerCategory.Block => ("Block", "Layers with entities in BLOCKS section"),
        DxfLayerCategory.Text => ("Text", "Layers containing TEXT/MTEXT"),
        _ => ("Unknown", string.Empty),
    };
}
