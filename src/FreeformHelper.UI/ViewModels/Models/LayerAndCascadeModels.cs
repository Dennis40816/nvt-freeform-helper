// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using DxfLayerCategory = FreeformHelper.UI.ViewModels.FreeformHelperViewModel.DxfLayerCategory;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Represents one DXF layer category state summary in current layer toggles.
/// </summary>
/// <param name="Category">Category enum.</param>
/// <param name="Display">Display label.</param>
/// <param name="Description">Short category note.</param>
/// <param name="TotalCount">Total layer count in this category.</param>
/// <param name="SelectedCount">Selected layer count in this category.</param>
public readonly record struct DxfLayerCategoryState(
    DxfLayerCategory Category,
    string Display,
    string Description,
    int TotalCount,
    int SelectedCount);

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

/// <summary>
/// Represents settings for a single cascaded IC, specifically its X and Y channel counts.
/// This is used for binding in the UI to allow per-IC channel adjustments.
/// </summary>
public sealed partial class CascadeIcSetting : ObservableObject
{
    /// <summary>
    /// Gets the zero-based index of the IC.
    /// </summary>
    [ObservableProperty] private int _icIndex;
    /// <summary>
    /// Gets or sets the number of X channels for this IC.
    /// </summary>
    [ObservableProperty] private decimal _xChannels;
    /// <summary>
    /// Gets or sets the number of Y channels for this IC.
    /// </summary>
    [ObservableProperty] private decimal _yChannels;

    /// <summary>
    /// Event raised before an X or Y channel property is changed.
    /// </summary>
    public event EventHandler<CascadeChannelChangingEventArgs>? ChannelChanging;

    /// <summary>
    /// Initializes a new instance of the <see cref="CascadeIcSetting"/> class.
    /// </summary>
    /// <param name="icIndex">The index of the IC.</param>
    /// <param name="xChannels">The initial number of X channels.</param>
    /// <param name="yChannels">The initial number of Y channels.</param>
    public CascadeIcSetting(int icIndex, int xChannels, int yChannels)
    {
        _icIndex = icIndex;
        _xChannels = xChannels;
        _yChannels = yChannels;
    }

    /// <summary>
    /// Partial method hook invoked before the <see cref="XChannels"/> property changes.
    /// Raises the <see cref="ChannelChanging"/> event.
    /// </summary>
    /// <param name="value">The new value for <see cref="XChannels"/>.</param>
    partial void OnXChannelsChanging(decimal value)
    {
        ChannelChanging?.Invoke(this, new CascadeChannelChangingEventArgs(nameof(XChannels), XChannels, value));
    }

    /// <summary>
    /// Partial method hook invoked before the <see cref="YChannels"/> property changes.
    /// Raises the <see cref="ChannelChanging"/> event.
    /// </summary>
    /// <param name="value">The new value for <see cref="YChannels"/>.</param>
    partial void OnYChannelsChanging(decimal value)
    {
        ChannelChanging?.Invoke(this, new CascadeChannelChangingEventArgs(nameof(YChannels), YChannels, value));
    }
}

/// <summary>
/// Provides event data for when a channel count within a <see cref="CascadeIcSetting"/> is changing.
/// </summary>
public sealed class CascadeChannelChangingEventArgs : EventArgs
{
    public CascadeChannelChangingEventArgs(string propertyName, decimal oldValue, decimal newValue)
    {
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public string PropertyName { get; }

    public decimal OldValue { get; }

    public decimal NewValue { get; }
}
