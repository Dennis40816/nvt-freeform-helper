using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Represents a single item in the workspace header menu, typically a toggle button
/// or a command button. It supports an ID, a display label, an icon, and can be grouped.
/// </summary>
public sealed class WorkspaceHeaderItem : ObservableObject
{
    // Action to invoke when the IsChecked state changes, usually to update the ViewModel.
    private readonly Action<bool>? _onToggle;
    // Backing field for the IsChecked property.
    private bool _isChecked;
    // Flag to suppress the _onToggle action when IsChecked is set programmatically.
    private bool _suppressToggle;

    /// <summary>
    /// Gets the unique identifier for this header item.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the display label for this header item.
    /// </summary>
    public string Label { get; }
    /// <summary>
    /// Gets the group name for this header item, used for logical grouping in the UI.
    /// </summary>
    public string? Group { get; }
    /// <summary>
    /// Gets the icon object associated with this header item.
    /// </summary>
    public object? Icon { get; }
    /// <summary>
    /// Gets the command to execute when this item is activated (if it's not a toggle).
    /// </summary>
    public ICommand? Command { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceHeaderItem"/> class.
    /// </summary>
    /// <param name="id">A unique identifier for the item.</param>
    /// <param name="label">The display label for the item.</param>
    /// <param name="isChecked">The initial checked state of the item (for toggles).</param>
    /// <param name="onToggle">An optional action to execute when the checked state changes.</param>
    /// <param name="command">An optional command to execute when the item is activated.</param>
    /// <param name="group">An optional group name for the item.</param>
    /// <param name="icon">An optional icon object for the item.</param>
    public WorkspaceHeaderItem(
        string id,
        string label,
        bool isChecked,
        Action<bool>? onToggle = null,
        ICommand? command = null,
        string? group = null,
        object? icon = null)
    {
        Id = id;
        Label = label;
        _isChecked = isChecked;
        _onToggle = onToggle;
        Command = command;
        Group = group;
        Icon = icon;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the item is checked (for toggle items).
    /// When set by user interaction, it invokes the <see cref="_onToggle"/> action.
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            // Only invoke _onToggle if the value actually changed and it's not being suppressed.
            if (SetProperty(ref _isChecked, value) && !_suppressToggle)
            {
                _onToggle?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Sets the <see cref="IsChecked"/> state programmatically without invoking the <see cref="_onToggle"/> action.
    /// This is useful when the checked state is being updated from the source ViewModel.
    /// </summary>
    /// <param name="value">The new checked state.</param>
    public void SetCheckedFromSource(bool value)
    {
        _suppressToggle = true; // Suppress the _onToggle action.
        IsChecked = value; // Set the property.
        _suppressToggle = false; // Re-enable _onToggle.
    }
}
