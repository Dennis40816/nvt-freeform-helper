using System.ComponentModel;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using InteractionSelectionChangedEventArgs = FreeformHelper.UI.Interaction.SelectionChangedEventArgs;

namespace FreeformHelper.UI.Views;

/// <summary>
/// This partial class of <see cref="FreeformHelperView"/> contains the logic related
/// to the interaction with the <see cref="PadCanvas"/> control, including event wiring,
/// displaying pad information popovers, and managing their layout.
/// </summary>
public sealed partial class FreeformHelperView
{
    // --- Pad Info Popover State ---
    private Rect2? _padInfoBoundsWorld; // World coordinates of the pad(s) for which info is shown.
    private IReadOnlyList<Rect2> _padInfoSelectedBounds = Array.Empty<Rect2>(); // Bounding boxes of all selected pads for layout.
    private PadInfoContext? _pendingPadInfoContext; // Context for a pad info request that's delayed due to dirty state.
    private INotifyPropertyChanged? _padInfoChangeNotifier; // Used to observe changes in the pad info ViewModel.
    private bool _hasPadInfoPendingChanges; // Flag indicating if the active pad info has unsaved changes.

    /// <summary>
    /// Wires up event handlers for the <see cref="PadCanvas"/> control.
    /// </summary>
    /// <param name="canvas">The <see cref="PadCanvas"/> instance.</param>
    private void WireCanvasEvents(PadCanvas canvas)
    {
        // Handle selection changes from the canvas.
        canvas.SelectionChanged += (_, e) =>
        {
            // If the pad info popover has unsaved changes, block new selections until resolved.
            if (HandleDirtyPadInfo()) return;
            if (DataContext is FreeformHelperViewModel vm)
            {
                vm.ApplyCanvasSelection(e.SelectedCadPadIds, e.SelectedRegularPadIndices);
            }
        };

        // Update pad info layout when the canvas view (zoom/pan) changes.
        canvas.ViewChanged += (_, _) => QueuePadInfoLayoutUpdate();

        // Handle context menu requests for CAD pads.
        canvas.CadPadContextRequested += async (_, e) =>
        {
            if (HandleDirtyPadInfo()) return; // Block if pad info is dirty.
            if (DataContext is not FreeformHelperViewModel vm) return;
            await ShowCadPadInfoAsync(vm, e.Pad);
        };

        // Handle context menu requests for Regular pads.
        canvas.RegularPadContextRequested += async (_, e) =>
        {
            if (HandleDirtyPadInfo()) return; // Block if pad info is dirty.
            if (DataContext is not FreeformHelperViewModel vm) return;
            await ShowRegularPadInfoAsync(vm, e.Pad);
        };
    }

    /// <summary>
    /// Displays a <see cref="CadPadInfoViewModel"/> popover for the given CAD pad.
    /// If multiple CAD pads are selected, it displays info for all of them.
    /// </summary>
    /// <param name="vm">The <see cref="FreeformHelperViewModel"/>.</param>
    /// <param name="pad">The <see cref="CadPad"/> that triggered the request.</param>
    private Task ShowCadPadInfoAsync(FreeformHelperViewModel vm, CadPad pad)
    {
        var selectedPads = BuildCadSelectionList(vm, pad); // Get selected CAD pads.
        var canSelectAreaBucket = vm.ColorCadByArea && selectedPads.Count == 1;
        var snapshot = vm.BuildCadPadInspectorSnapshot(
            pad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false);
        vm.CurrentPadInspectorSnapshot = snapshot;
        vm.QueueDeferredCadInspectorSnapshotRefresh(pad.Id);
        var info = new CadPadInfoViewModel(
            selectedPads,
            id => vm.TryGetCadOutputFwDiffIndex(id, out var idx) ? idx : null,
            vm.GetCadOutputFwDiffIndexOverride,
            vm.IsCadOutputFwDiffIndexAnchorCadPad,
            vm.GetCadPadCustomValue, // Function to get custom value.
            vm.SetCadPadCustomValues, // Action to set custom value.
            vm.SetCadOutputFwDiffIndexOverride,
            vm.ClearCadOutputFwDiffIndexOverride,
            vm.SetCadOutputFwDiffIndexAnchorCadPad,
            vm.ClearCadOutputFwDiffIndexAnchorCadPad,
            () => ClosePadInfoViaState(), // Action to close popover.
            vm.ShowNotchDetailCommand, // Notch detail command.
            canSelectAreaBucket ? () => vm.SelectCadAreaBucketFromPad(pad.Id) : null,
            vm.GetMatchedRegularPadIds,
            vm.GetMatchedRegularLinks,
            vm.GetRegularPadIcDiff,
            vm.FocusCadMatches,
            vm.HighlightCadMatches,
            ids => vm.HighlightCadPadGroup(ids),
            ids => vm.HighlightRegularPadGroup(ids, "Target allocation highlighted"),
            vm.GetCadV22CompensationPreview,
            vm.GetCadV22CompensationDiagnostics,
            id => vm.GetCadV22TargetAllocationSummary(id),
            vm.CadOutputFwDiffAutoMode,
            snapshot,
            vm.CurrentNotchComputationMode);
        TryOpenPadInfo(vm, info, selectedPads.Select(p => p.Bounds)); // Open the popover.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Displays a <see cref="RegularPadInfoViewModel"/> popover for the given Regular pad.
    /// If multiple Regular pads are selected, it displays info for all of them.
    /// </summary>
    /// <param name="vm">The <see cref="FreeformHelperViewModel"/>.</param>
    /// <param name="pad">The <see cref="RegularPad"/> that triggered the request.</param>
    private Task ShowRegularPadInfoAsync(FreeformHelperViewModel vm, RegularPad pad)
    {
        var selectedPads = BuildRegularSelectionList(vm, pad); // Get selected regular pads.
        var snapshot = vm.BuildRegularPadInspectorSnapshot(pad.RegularPadId);
        vm.CurrentPadInspectorSnapshot = snapshot;
        var info = new RegularPadInfoViewModel(
            selectedPads,
            vm.ToDisplayRow, // Function to convert actual row to display row.
            id => vm.TryGetCadOutputFwDiffIndex(id, out var idx) ? idx : null,
            vm.GetMatchedCadPadIds,
            vm.GetMatchedCadLinks,
            vm.ApplySizingToCells, // Action to apply sizing changes.
            vm.ResetSizingForCells, // Action to reset sizing.
            vm.SetFreeformForPadIndices,
            id => vm.GetCrossIcOwnerSharesForRegularPad(id),
            id => vm.GetLastNotchRowsForRegularPad(id),
            ids => vm.HighlightCadPadGroup(ids),
            vm.FocusRegularMatches,
            vm.HighlightRegularMatches,
            () => ClosePadInfoViaState(),
            snapshot); // Action to close popover.
        TryOpenPadInfo(vm, info, selectedPads.Select(p => p.Bounds)); // Open the popover.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal implementation of <see cref="ICanvasHost"/> to allow the ViewModel
    /// to control the <see cref="PadCanvas"/> functions.
    /// </summary>
    private sealed class CanvasHost : ICanvasHost
    {
        private readonly FreeformHelperView _owner;
        private readonly PadCanvas _canvas;

        public CanvasHost(FreeformHelperView owner, PadCanvas canvas)
        {
            _owner = owner;
            _canvas = canvas;
        }

        public void FitToContent() => _canvas.FitToContent();
        public void ResetView() => _canvas.ResetView();
        public void Invalidate() => _canvas.InvalidateVisual();
        public void ClearSelection() => _canvas.ClearSelection();
        public void SetSelection(IReadOnlyCollection<int> cadPadIds, IReadOnlyCollection<int> regularPadIndices)
            => _canvas.SetSelection(cadPadIds, regularPadIndices);
        public void FocusSelection() => _canvas.FocusSelection();
        public void FocusSelectionWithMinZoom(double minZoom)
        {
            if (CanvasFocusViewportService.TryGetFocusTarget(
                    _canvas,
                    _owner.EnumerateFocusAwareFloatingWindows(),
                    out var targetPoint))
            {
                _canvas.FocusSelectionWithMinZoomAt(minZoom, targetPoint);
                return;
            }

            _canvas.FocusSelectionWithMinZoom(minZoom);
        }
        public PadCanvasSelectionPerfSnapshot GetSelectionPerfSnapshot() => _canvas.GetSelectionPerfSnapshot();
        public PadCanvasVisibleDrawPerfSnapshot GetVisibleDrawPerfSnapshot() => _canvas.GetVisibleDrawPerfSnapshot();
    }

    private IEnumerable<Avalonia.Controls.Window?> EnumerateFocusAwareFloatingWindows()
    {
        yield return _dxfEditChangeListWindow;
        yield return _notchExportSelectionWindow;
        yield return _indexMappingReportWindow;
    }

    /// <summary>
    /// Builds a list of <see cref="CadPad"/> objects for display in the info popover.
    /// If the clicked pad is part of a multi-selection, the entire selection is returned.
    /// Otherwise, only the clicked pad.
    /// </summary>
    /// <param name="vm">The <see cref="FreeformHelperViewModel"/>.</param>
    /// <param name="pad">The <see cref="CadPad"/> that was clicked.</param>
    /// <returns>A list of CAD pads to display info for.</returns>
    private static List<CadPad> BuildCadSelectionList(FreeformHelperViewModel vm, CadPad pad)
    {
        // If multiple pads are selected and the clicked pad is among them, display info for all selected.
        if (vm.SelectedCadPadIds.Count <= 1 || !vm.SelectedCadPadIds.Contains(pad.Id))
        {
            return new List<CadPad> { pad }; // Only the clicked pad.
        }

        return vm.CadPads.Where(p => vm.SelectedCadPadIds.Contains(p.Id)).ToList(); // All selected pads.
    }

    /// <summary>
    /// Builds a list of <see cref="RegularPad"/> objects for display in the info popover.
    /// Similar logic to <see cref="BuildCadSelectionList"/>.
    /// </summary>
    /// <param name="vm">The <see cref="FreeformHelperViewModel"/>.</param>
    /// <param name="pad">The <see cref="RegularPad"/> that was clicked.</param>
    /// <returns>A list of regular pads to display info for.</returns>
    private static List<RegularPad> BuildRegularSelectionList(FreeformHelperViewModel vm, RegularPad pad)
    {
        if (vm.SelectedRegularPadIndices.Count <= 1 || !vm.SelectedRegularPadIndices.Contains(pad.Index))
        {
            return new List<RegularPad> { pad };
        }

        return vm.RegularPads.Where(p => vm.SelectedRegularPadIndices.Contains(p.Index)).ToList();
    }

    /// <summary>
    /// Attempts to open a pad information popover with the given ViewModel and bounds.
    /// Handles cases where a dirty popover might be active.
    /// </summary>
    /// <param name="vm">The <see cref="FreeformHelperViewModel"/>.</param>
    /// <param name="infoViewModel">The ViewModel containing the data for the popover.</param>
    /// <param name="bounds">The world coordinate bounding boxes of the pads for context.</param>
    private void TryOpenPadInfo(FreeformHelperViewModel vm, object infoViewModel, IEnumerable<Rect2> bounds)
    {
        var context = new PadInfoContext(infoViewModel, BuildBounds(bounds), bounds.ToList());
        // If a dirty pad info is active, store this context as pending.
        if (RequestClosePadInfo(context))
        {
            return;
        }

        vm.InteractionState.OpenPadInfo(context); // Open the pad info via the InteractionState.
    }

    /// <summary>
    /// Shows the pad information overlay and triggers a layout update for the popover position.
    /// </summary>
    private void ShowPadInfoOverlay()
    {
        if (_padInfoOverlay is null)
        {
            return;
        }

        _padInfoOverlay.IsVisible = true;
        UpdatePadInfoLayout(); // Initial layout.
        // Post another layout update to ensure correct positioning after UI render cycle.
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePadInfoLayout);
    }

    /// <summary>
    /// Event handler for selection-changed notifications from the interaction state.
    /// Requests to close any active pad info popovers.
    /// </summary>
    private void OnInteractionSelectionChanged(object? sender, InteractionSelectionChangedEventArgs e)
    {
        RequestClosePadInfo();
    }

    /// <summary>
    /// Event handler for pad-info context changes from the interaction state.
    /// Manages the opening, closing, and content of the pad information popover.
    /// </summary>
    private void OnPadInfoChanged(object? sender, PadInfoChangedEventArgs e)
    {
        if (e.Context is null) // Pad info is being closed.
        {
            ClosePadInfo();
            if (DataContext is FreeformHelperViewModel vmOnClose)
            {
                vmOnClose.CurrentPadInspectorSnapshot = null;
            }
            // If there was a pending pad info context, open it now.
            if (_pendingPadInfoContext is not null && DataContext is FreeformHelperViewModel vm)
            {
                var pending = _pendingPadInfoContext;
                _pendingPadInfoContext = null;
                vm.InteractionState.OpenPadInfo(pending);
            }
            return;
        }

        // Pad info is being opened or updated.
        _padInfoBoundsWorld = e.Context.BoundsWorld;
        _padInfoSelectedBounds = e.Context.SelectedBounds;
        if (_padInfoPopover is not null)
        {
            _padInfoPopover.DataContext = e.Context.ViewModel; // Set ViewModel for the popover content.
        }

        ObservePadInfoChanges(e.Context.ViewModel as IPadInfoChangeTracking); // Observe changes for dirty state.
        UpdatePadAreaInteraction(); // Update canvas interaction based on popover state.
        ShowPadInfoOverlay(); // Show the popover.
    }

    /// <summary>
    /// Hides the pad information popover and resets its state.
    /// </summary>
    private void ClosePadInfo()
    {
        if (_padInfoOverlay is null)
        {
            return;
        }

        _padInfoOverlay.IsVisible = false; // Hide the overlay.
        if (_padInfoPopover is not null)
        {
            _padInfoPopover.DataContext = null; // Clear DataContext.
            _padInfoPopover.HideCloseConfirm(); // Hide any close confirmation.
        }
        ObservePadInfoChanges(null); // Stop observing changes.
        SetPadAreaHitTest(true); // Re-enable hit testing on pads.
        _padInfoBoundsWorld = null;
        _padInfoSelectedBounds = Array.Empty<Rect2>();
    }

    /// <summary>
    /// Requests to close the pad information popover, potentially prompting the user if there are unsaved changes.
    /// </summary>
    /// <param name="replacement">An optional <see cref="PadInfoContext"/> to open after closing, if not dirty.</param>
    /// <returns>True if the popover is dirty and a confirmation is shown, false otherwise.</returns>
    private bool RequestClosePadInfo(PadInfoContext? replacement = null)
    {
        // If popover is not visible or not present, no need to close.
        if (_padInfoOverlay is null || _padInfoPopover is null || !_padInfoOverlay.IsVisible)
        {
            _pendingPadInfoContext = null;
            return false;
        }

        // Check if the current pad info has unsaved changes.
        if (_padInfoPopover.DataContext is IPadInfoChangeTracking tracking && tracking.HasPendingChanges)
        {
            // If dirty, store the new context as pending and show a confirmation.
            _pendingPadInfoContext = replacement;
            _padInfoPopover.ShowCloseConfirm();
            SetPadAreaHitTest(false); // Disable hit testing on pads while confirmation is active.
            return true;
        }

        // If not dirty, close immediately and open replacement if provided.
        _pendingPadInfoContext = replacement;
        ClosePadInfoViaState();
        return true;
    }

    /// <summary>
    /// Requests the ViewModel to close the pad info popover.
    /// </summary>
    private void ClosePadInfoViaState()
    {
        if (DataContext is FreeformHelperViewModel vm)
        {
            vm.InteractionState.ClosePadInfo(); // Delegate to ViewModel.
            return;
        }

        ClosePadInfo(); // Fallback if ViewModel is not set.
    }

}
