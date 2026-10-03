using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>
/// Interaction logic for the PadInfoPopover.axaml user control.
/// This control displays detailed information about a selected pad (or group of pads)
/// and provides functionality to apply or discard changes, including a confirmation prompt
/// for unsaved changes.
/// </summary>
public sealed partial class PadInfoPopover : UserControl
{
    /// <summary>
    /// Defines the <see cref="IsCloseConfirmVisible"/> AvaloniaProperty.
    /// Controls the visibility of the "unsaved changes" confirmation prompt within the popover.
    /// </summary>
    public static readonly StyledProperty<bool> IsCloseConfirmVisibleProperty =
        AvaloniaProperty.Register<PadInfoPopover, bool>(nameof(IsCloseConfirmVisible));

    /// <summary>
    /// Event raised when the user requests to close the popover after applying changes.
    /// </summary>
    public event EventHandler? ApplyCloseRequested;
    /// <summary>
    /// Event raised when the user requests to close the popover after discarding changes.
    /// </summary>
    public event EventHandler? DiscardCloseRequested;
    /// <summary>
    /// Event raised when the user dismisses the close confirmation prompt.
    /// </summary>
    public event EventHandler? CloseConfirmDismissed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PadInfoPopover"/> class.
    /// </summary>
    public PadInfoPopover()
    {
        InitializeComponent(); // Loads the XAML definition and initializes controls.
    }

    /// <summary>
    /// Gets or sets a value indicating whether the close confirmation prompt is visible.
    /// </summary>
    public bool IsCloseConfirmVisible
    {
        get => GetValue(IsCloseConfirmVisibleProperty);
        set => SetValue(IsCloseConfirmVisibleProperty, value);
    }

    /// <summary>
    /// Shows the "unsaved changes" confirmation prompt.
    /// </summary>
    public void ShowCloseConfirm()
    {
        IsCloseConfirmVisible = true;
    }

    /// <summary>
    /// Hides the "unsaved changes" confirmation prompt.
    /// </summary>
    public void HideCloseConfirm()
    {
        IsCloseConfirmVisible = false;
    }

    /// <summary>
    /// Loads the XAML UI definition for this control.
    /// </summary>
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Event handler for the "Apply and Close" button click.
    /// Invokes the ViewModel's ApplyChangesCommand and raises the <see cref="ApplyCloseRequested"/> event.
    /// </summary>
    private void ApplyAndClose_Click(object? sender, RoutedEventArgs e)
    {
        // If the DataContext implements IPadInfoChangeTracking, execute its ApplyChangesCommand.
        if (DataContext is IPadInfoChangeTracking tracking)
        {
            if (tracking.ApplyChangesCommand.CanExecute(null))
            {
                tracking.ApplyChangesCommand.Execute(null);
            }
        }

        ApplyCloseRequested?.Invoke(this, EventArgs.Empty); // Raise event to signal close.
    }

    /// <summary>
    /// Event handler for the "Discard and Close" button click.
    /// Invokes the ViewModel's DiscardChangesCommand and raises the <see cref="DiscardCloseRequested"/> event.
    /// </summary>
    private void DiscardAndClose_Click(object? sender, RoutedEventArgs e)
    {
        // If the DataContext implements IPadInfoChangeTracking, execute its DiscardChangesCommand.
        if (DataContext is IPadInfoChangeTracking tracking)
        {
            if (tracking.DiscardChangesCommand.CanExecute(null))
            {
                tracking.DiscardChangesCommand.Execute(null);
            }
        }

        DiscardCloseRequested?.Invoke(this, EventArgs.Empty); // Raise event to signal close.
    }

    /// <summary>
    /// Event handler for pointer presses on the popover itself.
    /// If the close confirmation is visible, pressing outside (but within the popover) dismisses it.
    /// </summary>
    private void OnPopoverPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsCloseConfirmVisible)
        {
            IsCloseConfirmVisible = false;
            CloseConfirmDismissed?.Invoke(this, EventArgs.Empty); // Raise event that confirmation was dismissed.
        }
    }
}
