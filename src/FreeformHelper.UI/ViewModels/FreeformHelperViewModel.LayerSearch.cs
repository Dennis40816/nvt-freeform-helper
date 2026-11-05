using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Gets the search box state that filters the left panel layer list by name.
    /// </summary>
    public LayerListSearchViewModel LayerSearch { get; } = new();

    partial void OnLayerTogglesChanged(ObservableCollection<LayerToggle> value) => LayerSearch.Attach(value);
}
