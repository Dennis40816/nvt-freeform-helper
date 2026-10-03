using System.ComponentModel;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    /// <summary>
    /// Enables or disables hit testing on the <see cref="PadCanvas"/>.
    /// Used to prevent interaction with pads while a popover is active or dirty.
    /// </summary>
    /// <param name="isEnabled">True to enable hit testing, false to disable.</param>
    private void SetPadAreaHitTest(bool isEnabled)
    {
        if (_canvas is null)
        {
            return;
        }

        _canvas.IsHitTestVisible = isEnabled;
    }

    /// <summary>
    /// Checks if the currently active pad info popover has pending changes.
    /// If so, it displays a confirmation prompt to the user and blocks further interaction with the canvas pads.
    /// </summary>
    /// <returns>True if the popover is dirty and a confirmation is shown, false otherwise.</returns>
    private bool HandleDirtyPadInfo()
    {
        // If the pad info popover exists, its DataContext is IPadInfoChangeTracking, and it has pending changes.
        if (_padInfoPopover?.DataContext is IPadInfoChangeTracking tracking && tracking.HasPendingChanges)
        {
            _padInfoPopover.ShowCloseConfirm(); // Show the confirmation dialog within the popover.
            SetPadAreaHitTest(false); // Disable hit testing on canvas pads to force user interaction with the popover.
            return true;
        }
        // Also block if the close confirmation is already visible.
        return _padInfoPopover?.IsCloseConfirmVisible == true;
    }

    private bool EnsureNoPendingEdits()
    {
        return !HandleDirtyPadInfo();
    }

    /// <summary>
    /// Observes property changes on the provided <see cref="IPadInfoChangeTracking"/> ViewModel.
    /// </summary>
    /// <param name="tracking">The ViewModel to observe, or null to stop observing.</param>
    private void ObservePadInfoChanges(IPadInfoChangeTracking? tracking)
    {
        // Unsubscribe from previous notifier if any.
        if (_padInfoChangeNotifier is not null)
        {
            _padInfoChangeNotifier.PropertyChanged -= OnPadInfoPropertyChanged;
        }

        _padInfoChangeNotifier = tracking as INotifyPropertyChanged;
        _hasPadInfoPendingChanges = tracking?.HasPendingChanges ?? false; // Update pending changes flag.

        // Subscribe to new notifier if provided.
        if (_padInfoChangeNotifier is not null)
        {
            _padInfoChangeNotifier.PropertyChanged += OnPadInfoPropertyChanged;
        }
    }

    /// <summary>
    /// Event handler for property changes on the observed pad info ViewModel.
    /// Updates the <see cref="_hasPadInfoPendingChanges"/> flag and refreshes pad area interaction.
    /// </summary>
    private void OnPadInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IPadInfoChangeTracking.HasPendingChanges))
        {
            return; // Only interested in HasPendingChanges property.
        }

        if (sender is IPadInfoChangeTracking tracking)
        {
            _hasPadInfoPendingChanges = tracking.HasPendingChanges;
            UpdatePadAreaInteraction(); // Refresh canvas interaction state.
        }
    }

    /// <summary>
    /// Updates whether interaction with canvas pads is enabled, based on the popover's dirty state
    /// or if a close confirmation is visible.
    /// </summary>
    private void UpdatePadAreaInteraction()
    {
        var shouldBlock = _hasPadInfoPendingChanges || (_padInfoPopover?.IsCloseConfirmVisible ?? false);
        SetPadAreaHitTest(!shouldBlock); // Enable hit testing if not blocked.
    }
}
