using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages the items
/// displayed in the workspace header, particularly toggles for various view options.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Initializes the collection of <see cref="WorkspaceHeaderItem"/>s that appear in the
    /// application's header/menu. These items typically represent view toggles.
    /// </summary>
    private void InitializeWorkspaceHeaderItems()
    {
        _workspaceHeaderItemMap.Clear(); // Clear existing map to avoid duplicates on reinitialization.
        WorkspaceHeaderItems = new ObservableCollection<WorkspaceHeaderItem>
        {
            CreateWorkspaceToggle("cadLayer", "CAD layer", () => ShowCad, v => ShowCad = v),
            CreateWorkspaceToggle("regularGrid", "Regular grid", () => ShowRegular, v => ShowRegular = v),
            CreateWorkspaceToggle("highlightUnmatched", "Highlight unmatched regular", () => HighlightUnmatched, v => HighlightUnmatched = v),
            CreateWorkspaceToggle("highlightFreeform", "Highlight freeform regular", () => HighlightFreeform, v => HighlightFreeform = v),
            CreateWorkspaceToggle("colorByArea", "Color CAD by area", () => ColorCadByArea, v => ColorCadByArea = v),
            // Add other workspace header items here as needed.
        };
    }

    /// <summary>
    /// Helper method to create a <see cref="WorkspaceHeaderItem"/> that acts as a boolean toggle
    /// and registers it in the internal map.
    /// </summary>
    /// <param name="id">A unique identifier for the item.</param>
    /// <param name="label">The display label for the item.</param>
    /// <param name="getter">A function to get the current boolean state of the item.</param>
    /// <param name="setter">An action to set the boolean state of the item.</param>
    /// <returns>A new <see cref="WorkspaceHeaderItem"/> instance.</returns>
    private WorkspaceHeaderItem CreateWorkspaceToggle(string id, string label, Func<bool> getter, Action<bool> setter)
    {
        var item = new WorkspaceHeaderItem(id, label, getter(), setter);
        _workspaceHeaderItemMap[id] = item; // Store in map for easy lookup.
        return item;
    }

    /// <summary>
    /// Synchronizes the checked state of a <see cref="WorkspaceHeaderItem"/> with an external boolean value.
    /// This is typically called when the underlying ViewModel property changes to update the UI item.
    /// </summary>
    /// <param name="id">The unique identifier of the <see cref="WorkspaceHeaderItem"/> to sync.</param>
    /// <param name="value">The boolean value to set the item's checked state to.</param>
    private void SyncWorkspaceToggle(string id, bool value)
    {
        if (_workspaceHeaderItemMap.TryGetValue(id, out var item))
        {
            item.SetCheckedFromSource(value); // Update the item's checked state without triggering its own setter logic.
        }
    }
}
