using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.CadImport;

/// <summary>
/// Represents a toggleable layer item in the UI, typically for controlling layer visibility.
/// </summary>
public sealed partial class LayerToggle : ObservableObject
{
    /// <summary>
    /// Gets or sets the name of the layer.
    /// </summary>
    [ObservableProperty] private string _name;
    /// <summary>
    /// Gets or sets a value indicating whether the layer is selected (e.g., visible).
    /// </summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayerToggle"/> class.
    /// </summary>
    /// <param name="name">The name of the layer.</param>
    /// <param name="isSelected">The initial selection state of the layer.</param>
    public LayerToggle(string name, bool isSelected = true)
    {
        _name = name;
        _isSelected = isSelected;
    }
}

