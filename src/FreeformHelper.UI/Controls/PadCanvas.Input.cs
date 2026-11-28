using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    // --- Selection Debouncing for Single Click ---
    // These fields are used to differentiate between a single click for selection
    // and the start of a drag/box selection, or a double click.
    private static readonly TimeSpan _pendingSingleSelectionDelay = TimeSpan.FromMilliseconds(150);
    private DispatcherTimer? _pendingSingleTimer; // Timer to wait before confirming a single selection.
    private HitResult? _pendingSingleHit; // Stores the hit test result for a potential single selection.
    private int _pendingSelectionVersion; // Version of selection when single click was initiated.
    private int _selectionVersion; // Incremented whenever selection changes, used to invalidate pending selections.
    private HitResult? _lastSingleSelectedPad; // Tracks the last individually selected pad for extend selection.


    /// <summary>
    /// Handles <see cref="PointerPressedEventArgs"/> events, initiating various interactions.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        Focus(); // Ensure the canvas has focus to receive keyboard events.

        var props = e.GetCurrentPoint(this).Properties;
        _lastPointer = e.GetPosition(this); // Record current pointer position.

        // If any mouse button is pressed, cancel any pending single selections.
        if (props.IsLeftButtonPressed || props.IsRightButtonPressed)
        {
            CancelPendingSingleSelection();
        }

        // --- Left Button Pressed Logic ---
        if (props.IsLeftButtonPressed && !IsSpaceDown())
        {
            var modifiers = e.KeyModifiers;
            var additive = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Shift);

            if (TryHandleAxisLabelClick(_lastPointer, additive))
            {
                e.Handled = true;
                return;
            }

            if (e.ClickCount >= 2) // Double click
            {
                if (HandleDoubleClick(_lastPointer))
                {
                    e.Handled = true; // Mark event as handled if double click was processed.
                    return;
                }
            }
            // For single-click / box-select flow, defer hit-test to pointer release.
            // This keeps mouse-down responsive on large scenes where CAD hit-test can be costly.
        }

        // --- Middle Mouse Button Panning ---
        if (props.IsMiddleButtonPressed)
        {
            _isMiddlePanning = true;
            _lastPanRedrawTicks = 0;
            e.Pointer.Capture(this); // Capture pointer for continuous panning even if mouse leaves control.
            e.Handled = true;
            return;
        }

        // --- Right Mouse Button Context Menu / Selection ---
        if (props.IsRightButtonPressed && CreateContextPressHandler().Handle(_lastPointer, e.KeyModifiers))
        {
            e.Handled = true;
            return;
        }

        // --- Spacebar Panning (alternative to middle mouse) ---
        if (props.IsLeftButtonPressed && IsSpaceDown())
        {
            _isSpacePanning = true;
            _lastPanRedrawTicks = 0;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // --- Left Mouse Button Down (potential drag or box selection) ---
        if (props.IsLeftButtonPressed)
        {
            _isLeftPointerDown = true;
            _pointerDown = _lastPointer; // Store the initial mouse down position.
            _boxStart = _lastPointer; // Initialize box selection start point.
            _boxEnd = _lastPointer; // Initialize box selection end point.
            _lastBoxSelectionRedrawTicks = 0;
            e.Pointer.Capture(this); // Capture pointer for drag operations.
            e.Handled = true;
        }
    }

    private PadCanvasContextPressHandler CreateContextPressHandler() => new(new PadCanvasContextPressHandler.Services(
        HitTest,
        () => ClearSelectionInternal(),
        () => RequestVisualRefresh(),
        IsHitSelected,
        ApplyHitSelection,
        id => CadPads?.FirstOrDefault(p => p.Id == id),
        index => RegularPads?.FirstOrDefault(p => p.Index == index),
        pad => CadPadContextRequested?.Invoke(this, new CadPadContextRequestedEventArgs(pad)),
        pad => RegularPadContextRequested?.Invoke(this, new RegularPadContextRequestedEventArgs(pad))));

    /// <summary>
    /// Handles <see cref="PointerReleasedEventArgs"/> events, finalizing interactions.
    /// </summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // Release pointer capture if panning was active.
        if (_isMiddlePanning || _isSpacePanning)
        {
            _isMiddlePanning = false;
            _isSpacePanning = false;
            _lastPanRedrawTicks = 0;
            RequestViewRefresh();
            ReleaseGestureCapture(e.Pointer);
            e.Handled = true;
            return;
        }

        // --- Box Selection Completion ---
        if (_isBoxSelecting)
        {
            var modifiers = e.KeyModifiers;
            var additive = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Shift);
            var regularOnly = modifiers.HasFlag(KeyModifiers.Alt);

            var rect = new Rect(_boxStart, _boxEnd).Normalize(); // Normalize rect to ensure positive width/height.
            ApplyBoxSelection(rect, additive, regularOnly); // Apply selection based on the box.

            _isBoxSelecting = false; // Reset box selection state.
            _isLeftPointerDown = false; // Reset left pointer down flag before releasing capture.
            _lastBoxSelectionRedrawTicks = 0;
            ReleaseGestureCapture(e.Pointer);
            RequestVisualRefresh();
            RaiseSelectionChanged(); // Notify listeners of selection change.
            e.Handled = true;
            RefreshHoverDebugHitFromCurrentPointer(modifiers);
            return;
        }

        // --- Single Click Selection Completion ---
        if (_isLeftPointerDown)
        {
            CreateClickReleaseHandler().Handle(e.GetPosition(this), e.KeyModifiers);
            _isLeftPointerDown = false;
            ReleaseGestureCapture(e.Pointer);
            e.Handled = true;
            RefreshHoverDebugHitFromCurrentPointer(e.KeyModifiers);
        }
    }

    private PadCanvasClickReleaseHandler CreateClickReleaseHandler() => new(new PadCanvasClickReleaseHandler.Services(
        HitTest,
        () => _lastSingleSelectedPad,
        hit => _lastSingleSelectedPad = hit,
        ApplyExtendSelection,
        () => ClearSelectionInternal(),
        () => RequestVisualRefresh(),
        () => _selectedCadIds.Count + _selectedRegIdx.Count,
        IsHitSelected,
        SchedulePendingSingleSelection,
        ApplyHitSelection));
}
