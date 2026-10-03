using Avalonia;
using Avalonia.Controls;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// A custom <see cref="ContentControl"/> that provides a collapsible panel.
/// It displays a title, optional content on the right side of the header,
/// and can hide or show its main content area.
/// </summary>
public class HidePanelBlock : ContentControl
{
    /// <summary>
    /// Defines the <see cref="Title"/> AvaloniaProperty.
    /// </summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<HidePanelBlock, string>(nameof(Title), "");

    /// <summary>
    /// Defines the <see cref="IsExpanded"/> AvaloniaProperty.
    /// </summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<HidePanelBlock, bool>(nameof(IsExpanded), true);

    /// <summary>
    /// Defines the <see cref="DefaultExpanded"/> AvaloniaProperty.
    /// </summary>
    public static readonly StyledProperty<bool> DefaultExpandedProperty =
        AvaloniaProperty.Register<HidePanelBlock, bool>(nameof(DefaultExpanded), true);

    /// <summary>
    /// Defines the <see cref="IsCollapsible"/> AvaloniaProperty.
    /// </summary>
    public static readonly StyledProperty<bool> IsCollapsibleProperty =
        AvaloniaProperty.Register<HidePanelBlock, bool>(nameof(IsCollapsible), true);

    /// <summary>
    /// Defines the <see cref="HeaderRight"/> AvaloniaProperty.
    /// </summary>
    public static readonly StyledProperty<object?> HeaderRightProperty =
        AvaloniaProperty.Register<HidePanelBlock, object?>(nameof(HeaderRight));

    /// <summary>
    /// Gets or sets the title text displayed in the header of the panel.
    /// </summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the content area of the panel is currently expanded (visible).
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating the default expanded state when the control is initialized.
    /// This value is used if <see cref="IsExpanded"/> is not explicitly set.
    /// </summary>
    public bool DefaultExpanded
    {
        get => GetValue(DefaultExpandedProperty);
        set => SetValue(DefaultExpandedProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the panel can be collapsed (i.e., its expanded state can be changed).
    /// If false, the panel will always remain expanded.
    /// </summary>
    public bool IsCollapsible
    {
        get => GetValue(IsCollapsibleProperty);
        set => SetValue(IsCollapsibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the content to be displayed on the right side of the panel's header.
    /// </summary>
    public object? HeaderRight
    {
        get => GetValue(HeaderRightProperty);
        set => SetValue(HeaderRightProperty, value);
    }

    /// <summary>
    /// Called when the control is initialized. Sets <see cref="IsExpanded"/> to <see cref="DefaultExpanded"/>
    /// if <see cref="IsExpanded"/> hasn't been explicitly set.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // If IsExpanded is not explicitly set in XAML or code, use the DefaultExpanded value.
        if (!IsSet(IsExpandedProperty))
        {
            IsExpanded = DefaultExpanded;
        }
    }

    /// <summary>
    /// Responds to changes in AvaloniaProperties.
    /// </summary>
    /// <param name="change">The event arguments for the property change.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // If IsCollapsible changes to false, force the panel to be expanded.
        if (change.Property == IsCollapsibleProperty && change.NewValue is bool isCollapsible && !isCollapsible)
        {
            IsExpanded = true;
        }

        // If the panel is not collapsible, ensure it always remains expanded
        // even if an attempt is made to collapse it.
        if (change.Property == IsExpandedProperty && !IsCollapsible && change.NewValue is bool isExpanded && !isExpanded)
        {
            IsExpanded = true;
        }
    }
}
