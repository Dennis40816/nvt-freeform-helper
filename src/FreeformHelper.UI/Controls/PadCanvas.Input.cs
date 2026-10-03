using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    // --- Selection Debouncing for Single Click ---
    // These fields are used to differentiate between a single click for selection
    // and the start of a drag/box selection, or a double click.
    private static readonly TimeSpan PendingSingleSelectionDelay = TimeSpan.FromMilliseconds(150);
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
        if (props.IsRightButtonPressed)
        {
            var modifiers = e.KeyModifiers;
            var hit = HitTest(_lastPointer, modifiers.HasFlag(KeyModifiers.Alt));
            if (hit.kind == HitKind.None)
            {
                ClearSelectionInternal(); // Clear selection if nothing was hit.
                RequestVisualRefresh();
                e.Handled = true;
                return;
            }

            var isControlDown = modifiers.HasFlag(KeyModifiers.Control);

            var isAlreadySelected = IsHitSelected(hit);

            // If Ctrl is held, perform additive toggle.
            if (isControlDown)
            {
                // Toggle the item. Additive ensures existing selection is not cleared.
                ApplyHitSelection(hit, toggle: true, additive: true);
            }
            else
            {
                // If Ctrl is not held, and the item is not already selected, select it (non-toggle, non-additive).
                // If it IS already selected and Ctrl is not held, do nothing to selection, just raise context menu.
                if (!isAlreadySelected)
                {
                    ApplyHitSelection(hit, toggle: false, additive: false);
                }
            }

            // Raise context menu events based on the type of hit item.
            if (hit.kind == HitKind.Cad && CadPads is { Count: > 0 })
            {
                var cad = CadPads.FirstOrDefault(p => p.Id == hit.idOrIndex);
                if (cad is not null)
                {
                    CadPadContextRequested?.Invoke(this, new CadPadContextRequestedEventArgs(cad));
                    e.Handled = true;
                    return;
                }
            }

            if (hit.kind == HitKind.Regular && RegularPads is { Count: > 0 })
            {
                var pad = RegularPads.FirstOrDefault(p => p.Index == hit.idOrIndex);
                if (pad is not null)
                {
                    RegularPadContextRequested?.Invoke(this, new RegularPadContextRequestedEventArgs(pad));
                    e.Handled = true;
                    return;
                }
            }
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
            e.Pointer.Capture(null);
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
            _lastBoxSelectionRedrawTicks = 0;
            e.Pointer.Capture(null); // Release pointer capture.
            RequestVisualRefresh();
            RaiseSelectionChanged(); // Notify listeners of selection change.
            e.Handled = true;
            _isLeftPointerDown = false; // Reset left pointer down flag.
            RefreshHoverDebugHitFromCurrentPointer(modifiers);
            return;
        }

        // --- Single Click Selection Completion ---
        if (_isLeftPointerDown)
        {
            var modifiers = e.KeyModifiers;
            var hit = HitTest(e.GetPosition(this), modifiers.HasFlag(KeyModifiers.Alt)); // Perform hit test at release position.

            // Handle Shift for extend selection (if not also Ctrl)
            if (modifiers.HasFlag(KeyModifiers.Shift) && !modifiers.HasFlag(KeyModifiers.Control) && hit.kind != HitKind.None)
            {
                if (_lastSingleSelectedPad.HasValue)
                {
                    ApplyExtendSelection(_lastSingleSelectedPad.Value, hit);
                }
                else // If no anchor, just select the current hit, and set it as anchor
                {
                    ClearSelectionInternal(); // Clear existing selections if no anchor
                    ApplyHitSelection(hit, toggle: false, additive: false);
                    _lastSingleSelectedPad = hit;
                }
            }
            else // All other single left clicks (Ctrl, no modifiers, or both Ctrl+Shift)
            {
                var toggle = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Shift); // True if Ctrl or Shift is pressed
                var additive = toggle; // If toggle, it's also additive (doesn't clear others)

                if (hit.kind == HitKind.None && !toggle && !additive)
                {
                    ClearSelectionInternal(); // Clear selection if no item hit and no modifiers
                    _lastSingleSelectedPad = null; // Clear anchor if nothing is selected
                    RequestVisualRefresh(); // Coalesce stale-highlight redraw without forcing synchronous layout.
                }
                else
                {
                    var totalSelected = _selectedCadIds.Count + _selectedRegIdx.Count;
                    var alreadySelected = IsHitSelected(hit);

                    // If multiple items are selected and the user clicks on an already selected item
                    // without toggle/additive modifiers, it's a potential single selection intent.
                    if (!toggle && !additive && alreadySelected && totalSelected > 1)
                    {
                        SchedulePendingSingleSelection(hit); // Schedule a delayed single selection.
                    }
                    else
                    {
                        ApplyHitSelection(hit, toggle, additive); // Apply immediate selection.
                        _lastSingleSelectedPad = hit; // Update anchor for single selection
                    }
                }
            }

            e.Pointer.Capture(null);
            e.Handled = true;
            _isLeftPointerDown = false;
            RefreshHoverDebugHitFromCurrentPointer(modifiers);
        }
    }
}
