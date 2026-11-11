using FreeformHelper.Application.Services;
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
    private PadInfoOverlaySession? _padInfoSession; // UI thread only, retained across reattach.

    internal PadInfoContext? _pendingPadInfoContext
    {
        get => PadInfoSession.PendingContext;
        set => PadInfoSession.PendingContext = value;
    }

    private PadInfoOverlaySession PadInfoSession => _padInfoSession ??= new(
        () => DataContext as FreeformHelperViewModel,
        () => _padInfoOverlay,
        () => _padInfoPopover,
        () => _canvas,
        bounds => _padInfoBoundsWorld = bounds,
        bounds => _padInfoSelectedBounds = bounds,
        ShowPadInfoOverlay);

    private PadInfoOverlayRenderer PadInfoRenderer => _padInfoRenderer ??= new(
        this,
        () => _padInfoOverlay,
        () => _padInfoPopover,
        () => _padInfoLink,
        () => _padInfoAnchor,
        () => _canvas,
        point => _canvas!.WorldToScreen(point),
        () => _padInfoBoundsWorld,
        () => _padInfoSelectedBounds);

    private void QueuePadInfoLayoutUpdate() => PadInfoRenderer.QueueLayoutUpdate();

    private void UpdatePadInfoLayout() => PadInfoRenderer.UpdateLayout();

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
        canvas.CadPadContextRequested += OnCadPadContextRequested;

        // Handle context menu requests for Regular pads.
        canvas.RegularPadContextRequested += OnRegularPadContextRequested;
    }

    private void OnCadPadContextRequested(object? sender, PadCanvas.CadPadContextRequestedEventArgs e)
    {
        if (HandleDirtyPadInfo() || DataContext is not FreeformHelperViewModel vm) return;
        var pad = e.Pad;
        _uiEvents?.Run("Workspace.CadPadContext", _ => ShowCadPadInfoAsync(vm, pad), CancellationToken.None);
    }

    private void OnRegularPadContextRequested(object? sender, PadCanvas.RegularPadContextRequestedEventArgs e)
    {
        if (HandleDirtyPadInfo() || DataContext is not FreeformHelperViewModel vm) return;
        var pad = e.Pad;
        _uiEvents?.Run("Workspace.RegularPadContext", _ => ShowRegularPadInfoAsync(vm, pad), CancellationToken.None);
    }

    /// <summary>
    /// Displays a <see cref="CadPadInfoViewModel"/> popover for the given CAD pad.
    /// If multiple CAD pads are selected, it displays info for all of them.
    /// </summary>
    /// <param name="vm">The <see cref="FreeformHelperViewModel"/>.</param>
    /// <param name="pad">The <see cref="CadPad"/> that triggered the request.</param>
    private Task ShowCadPadInfoAsync(FreeformHelperViewModel vm, CadPad pad)
    {
        var selectedPads = PadInfoSelectionResolver.Resolve(vm.CadPads, vm.SelectedCadPadIds, pad); // Get selected CAD pads.
        var canSelectAreaBucket = vm.ColorCadByArea && selectedPads.Count == 1;
        var snapshot = vm.BuildCadPadInspectorSnapshot(
            pad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false);
        vm.PadInspectorUi.Snapshot = snapshot;
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
            canSelectAreaBucket ? () => vm.Layers.SelectCadAreaBucketFromPad(pad.Id) : null,
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
        var selectedPads = PadInfoSelectionResolver.Resolve(
            vm.RegularPads, vm.SelectedRegularPadIndices, pad); // Get selected regular pads.
        var snapshot = vm.BuildRegularPadInspectorSnapshot(pad.RegularPadId);
        vm.PadInspectorUi.Snapshot = snapshot;
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
    /// Attempts to open a pad information popover with the given ViewModel and bounds.
    /// Handles cases where a dirty popover might be active.
    /// </summary>
    /// <param name="vm">The <see cref="FreeformHelperViewModel"/>.</param>
    /// <param name="infoViewModel">The ViewModel containing the data for the popover.</param>
    /// <param name="bounds">The world coordinate bounding boxes of the pads for context.</param>
    private void TryOpenPadInfo(FreeformHelperViewModel vm, object infoViewModel, IEnumerable<Rect2> bounds)
        => PadInfoSession.TryOpenPadInfo(vm, infoViewModel, bounds);

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
        Dispatcher.Post(UpdatePadInfoLayout);
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
        => PadInfoSession.OnPadInfoChanged(e);

    private bool RequestClosePadInfo(PadInfoContext? replacement = null)
        => PadInfoSession.RequestClosePadInfo(replacement);

    private void ClosePadInfoViaState() => PadInfoSession.ClosePadInfoViaState();

    private void SetPadAreaHitTest(bool isEnabled) => PadInfoSession.SetPadAreaHitTest(isEnabled);

    private bool HandleDirtyPadInfo() => PadInfoSession.HandleDirtyPadInfo();

    private bool EnsureNoPendingEdits() => PadInfoSession.EnsureNoPendingEdits();
}
