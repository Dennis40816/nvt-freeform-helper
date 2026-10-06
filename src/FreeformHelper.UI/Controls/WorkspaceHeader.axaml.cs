using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// A custom control that represents the header section of the workspace.
/// It displays various information like status text, selection summary, and
/// provides a button for a dropdown view menu. It includes logic for managing
/// the view menu's open/close state based on hover and pinning.
/// </summary>
public partial class WorkspaceHeader : UserControl
{
    // --- Private Fields for UI State Management ---
    private bool _viewPinned; // True if the view menu popup is explicitly pinned open.
    private bool _isViewButtonHovered; // True while pointer is over the display button.
    private bool _isViewPopupHovered; // True while pointer is over the display popup.
    private TopLevel? _topLevel; // Reference to the top-level window, used for global pointer events and bounds.
    private Window? _window; // Concrete window reference for activation events.

    // --- Styled Properties ---

    /// <summary>
    /// Defines the <see cref="Items"/> AvaloniaProperty.
    /// A collection of <see cref="WorkspaceHeaderItem"/> to display in the view menu.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<WorkspaceHeaderItem>?> ItemsProperty =
        AvaloniaProperty.Register<WorkspaceHeader, IEnumerable<WorkspaceHeaderItem>?>(nameof(Items));

    /// <summary>
    /// Defines the <see cref="StatusText"/> AvaloniaProperty.
    /// The text displayed as the general status of the application.
    /// </summary>
    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<WorkspaceHeader, string?>(nameof(StatusText));

    /// <summary>
    /// Defines the <see cref="SelectionSummary"/> AvaloniaProperty.
    /// A summary text indicating the current selection (e.g., "3 CAD pads selected").
    /// </summary>
    public static readonly StyledProperty<string?> SelectionSummaryProperty =
        AvaloniaProperty.Register<WorkspaceHeader, string?>(nameof(SelectionSummary));

    /// <summary>
    /// Defines the <see cref="ClearSelectionCommand"/> AvaloniaProperty.
    /// The command to execute when the clear selection action is invoked.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ClearSelectionCommandProperty =
        AvaloniaProperty.Register<WorkspaceHeader, ICommand?>(nameof(ClearSelectionCommand));

    // --- Public Properties (Wrappers for Styled Properties) ---

    public IEnumerable<WorkspaceHeaderItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string? StatusText
    {
        get => GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string? SelectionSummary
    {
        get => GetValue(SelectionSummaryProperty);
        set => SetValue(SelectionSummaryProperty, value);
    }

    public ICommand? ClearSelectionCommand
    {
        get => GetValue(ClearSelectionCommandProperty);
        set => SetValue(ClearSelectionCommandProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceHeader"/> class.
    /// </summary>
    public WorkspaceHeader()
    {
        InitializeComponent(); // Loads the XAML definition for the control.
        AttachedToVisualTree += OnAttachedToVisualTree; // Subscribe to when the control is added to the visual tree.
        DetachedFromVisualTree += OnDetachedFromVisualTree; // Subscribe to when the control is removed from the visual tree.
    }

    /// <summary>
    /// Event handler for when the control is attached to the visual tree.
    /// This is where global event listeners are set up.
    /// </summary>
    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(this); // Get the parent TopLevel window.
        if (_topLevel is not null)
        {
            // Subscribe to global pointer presses to handle clicking outside the popup.
            _topLevel.PointerPressed += OnTopLevelPointerPressed;
            // Subscribe to layout updates to adjust popup bounds dynamically.
            _topLevel.LayoutUpdated += OnTopLevelLayoutUpdated;
            _window = _topLevel as Window;
            if (_window is not null)
            {
                _window.Deactivated += OnWindowDeactivated;
            }
            UpdatePopupBounds(); // Set initial popup bounds.
        }
    }

    /// <summary>
    /// Event handler for when the control is detached from the visual tree.
    /// This is where global event listeners are cleaned up.
    /// </summary>
    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.PointerPressed -= OnTopLevelPointerPressed;
            _topLevel.LayoutUpdated -= OnTopLevelLayoutUpdated;
            _topLevel = null;
        }

        if (_window is not null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }
    }

    /// <summary>
    /// Event handler for TopLevel layout updates.
    /// Adjusts the maximum width and height of the view menu popup to stay within window bounds.
    /// </summary>
    private void OnTopLevelLayoutUpdated(object? sender, EventArgs e)
    {
        UpdatePopupBounds();
    }

    /// <summary>
    /// Event handler for when the mouse pointer enters the view menu button area.
    /// </summary>
    private void ViewMenuButton_PointerEntered(object? sender, PointerEventArgs e)
    {
        _isViewButtonHovered = true;
        UpdateViewPopupState();
    }

    /// <summary>
    /// Pins the popup on first click when it is already open due to hover,
    /// instead of toggling it closed.
    /// </summary>
    private void ViewMenuButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Click behavior is handled by ToggleButton Checked/Unchecked events.
    }

    /// <summary>
    /// Event handler for when the mouse pointer exits the view menu button area.
    /// </summary>
    private void ViewMenuButton_PointerExited(object? sender, PointerEventArgs e)
    {
        _isViewButtonHovered = false;
        ScheduleViewPopupHoverClose();
    }

    /// <summary>
    /// Event handler for when the mouse pointer enters the view menu popup area.
    /// </summary>
    private void ViewMenuPopup_PointerEntered(object? sender, PointerEventArgs e)
    {
        _isViewPopupHovered = true;
        UpdateViewPopupState();
    }

    /// <summary>
    /// Event handler for when the mouse pointer exits the view menu popup area.
    /// </summary>
    private void ViewMenuPopup_PointerExited(object? sender, PointerEventArgs e)
    {
        _isViewPopupHovered = false;
        ScheduleViewPopupHoverClose();
    }

    /// <summary>
    /// Event handler for when the view menu button is checked (pinned) or unchecked (unpinned).
    /// An indeterminate state leaves the pin unchanged, as the separate checked and unchecked events did.
    /// </summary>
    private void ViewMenuButton_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { IsChecked: bool isChecked })
        {
            return;
        }

        _viewPinned = isChecked;
        UpdateViewPopupState(); // Update popup visibility.
    }

    /// <summary>
    /// Global event handler for pointer presses on the top-level window.
    /// Used to close the pinned popup if a click occurs outside of it.
    /// </summary>
    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_viewPinned)
        {
            return; // Only applies if the view menu is pinned.
        }

        if (ViewMenuButton.IsPointerOver)
        {
            return; // Ignore clicks on the button itself.
        }

        // Check if the click occurred within the popup's content area.
        if (ViewMenuPopup.IsOpen && ViewMenuPopup.Child is Control child && child.IsPointerOver)
        {
            return;
        }

        // If pinned and clicked outside, unpin and close the popup.
        _viewPinned = false;
        ViewMenuButton.IsChecked = false;
        ViewMenuPopup.IsOpen = false;
    }

    /// <summary>
    /// Handles activation changes to close/unpin popup when the host window is not active.
    /// </summary>
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        _viewPinned = false;
        _isViewButtonHovered = false;
        _isViewPopupHovered = false;
        ViewMenuButton.IsChecked = false;
        ViewMenuPopup.IsOpen = false;
    }

    /// <summary>
    /// Updates the open/close state of the view menu popup based on current hover and pinned states.
    /// </summary>
    private void UpdateViewPopupState()
    {
        var shouldOpen = _viewPinned || _isViewButtonHovered || _isViewPopupHovered;

        if (shouldOpen)
        {
            if (!ViewMenuPopup.IsOpen)
            {
                ViewMenuPopup.IsOpen = true;
            }
        }
        else
        {
            ViewMenuPopup.IsOpen = false;
        }
    }

    private void ScheduleViewPopupHoverClose()
    {
        if (_viewPinned)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_viewPinned || _isViewButtonHovered || _isViewPopupHovered)
            {
                return;
            }

            ViewMenuPopup.IsOpen = false;
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Updates the maximum width and height of the view menu popup to prevent it
    /// from extending beyond the bounds of the main window.
    /// </summary>
    private void UpdatePopupBounds()
    {
        if (_topLevel is null)
        {
            return;
        }

        // Calculate maximum width/height with some padding.
        var maxWidth = Math.Max(260, _topLevel.Bounds.Width - 30);
        var maxHeight = Math.Max(200, _topLevel.Bounds.Height - 30);

        ViewMenuPopupRoot.MaxWidth = maxWidth;
        ViewMenuPopupRoot.MaxHeight = maxHeight;
    }

}
