namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    internal IReadOnlyList<DxfLayerCategoryState> GetDxfLayerCategoryStates()
    {
        if (LayerToggles.Count == 0)
        {
            return Array.Empty<DxfLayerCategoryState>();
        }

        var categories = new[]
        {
            DxfLayerCategory.Polyline,
            DxfLayerCategory.Block,
            DxfLayerCategory.Text,
        };

        var states = new List<DxfLayerCategoryState>(categories.Length);
        foreach (var category in categories)
        {
            var members = GetLayerTogglesForCategory(category);
            if (members.Count == 0)
            {
                continue;
            }

            var (display, description) = GetLayerCategoryMeta(category);
            var selected = members.Count(static toggle => toggle.IsSelected);
            states.Add(new DxfLayerCategoryState(category, display, description, members.Count, selected));
        }

        return states;
    }

    internal void ApplyLayerCategorySelection(DxfLayerCategory category, bool isSelected)
    {
        if (!CanProceedWithPendingEdits())
        {
            return;
        }

        var targets = GetLayerTogglesForCategory(category);
        if (targets.Count == 0)
        {
            return;
        }

        var undoSnapshot = CaptureLayerSelectionSnapshot();
        var changed = false;
        _suppressLayerToggleChange = true;
        foreach (var toggle in targets)
        {
            if (toggle.IsSelected == isSelected)
            {
                continue;
            }

            toggle.IsSelected = isSelected;
            changed = true;
        }
        _suppressLayerToggleChange = false;

        if (!changed)
        {
            return;
        }

        MarkUnsaved();
        FilterCadPadsByLayer();

        var (display, _) = GetLayerCategoryMeta(category);
        var action = isSelected ? "On" : "Off";
        PushUndo(() => RestoreLayerSelectionSnapshot(undoSnapshot), $"DXF layer category {display} {action}");
    }

    private List<LayerToggle> GetLayerTogglesForCategory(DxfLayerCategory category)
    {
        return LayerToggles
            .Where(toggle => LayerMatchesCategory(toggle.Name, category))
            .ToList();
    }

    private bool LayerMatchesCategory(string layerName, DxfLayerCategory category)
    {
        var catalog = _layerCatalogStateService.Catalog;
        if (catalog.TryGetLayer(layerName, out var item))
        {
            return category switch
            {
                DxfLayerCategory.Polyline => item.HasPolylineContent,
                DxfLayerCategory.Block => item.HasBlockSectionContent,
                DxfLayerCategory.Text => item.HasTextContent,
                _ => false,
            };
        }

        return category == DxfLayerCategory.Polyline;
    }

    private static (string Display, string Description) GetLayerCategoryMeta(DxfLayerCategory category)
    {
        return category switch
        {
            DxfLayerCategory.Polyline => ("Polyline", "Layers containing LWPOLYLINE/POLYLINE"),
            DxfLayerCategory.Block => ("Block", "Layers with entities in BLOCKS section"),
            DxfLayerCategory.Text => ("Text", "Layers containing TEXT/MTEXT"),
            _ => ("Unknown", string.Empty),
        };
    }
}
