using Avalonia;
using Avalonia.Input;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    /// <summary>
    /// Handles <see cref="PointerEventArgs"/> events for mouse movement.
    /// Manages panning and initiation of box selection.
    /// </summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var p = e.GetPosition(this); // Current pointer position.

        // --- Panning Logic ---
        if (_isMiddlePanning || _isSpacePanning)
        {
            ClearHoverDebugHit(invalidateVisual: false);
            var delta = p - _lastPointer; // Calculate movement delta.
            _pan += new Vector(delta.X, delta.Y); // Apply delta to pan offset.
            InvalidateViewFrameSnapshot();
            _lastPointer = p; // Update last pointer position.

            var now = DateTime.UtcNow.Ticks;
            if (_lastPanRedrawTicks == 0 || now - _lastPanRedrawTicks >= PanRedrawIntervalTicks)
            {
                _lastPanRedrawTicks = now;
                RequestViewRefresh(); // Coalesce redraw + view notification to the next render frame.
            }

            e.Handled = true;
            return;
        }

        // --- Box Selection Logic ---
        if (_isLeftPointerDown)
        {
            ClearHoverDebugHit(invalidateVisual: false);
            if (!_isBoxSelecting) // If not yet box selecting, check if drag threshold is met.
            {
                var delta = p - _pointerDown; // Distance from initial mouse down.
                if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
                {
                    _isBoxSelecting = true; // Start box selection.
                    _boxStart = _pointerDown; // Box starts from the initial mouse down.
                }
            }

            if (_isBoxSelecting) // If currently box selecting, update box end point and redraw.
            {
                _boxEnd = p;
                var now = DateTime.UtcNow.Ticks;
                if (_lastBoxSelectionRedrawTicks == 0 || now - _lastBoxSelectionRedrawTicks >= BoxSelectionRedrawIntervalTicks)
                {
                    _lastBoxSelectionRedrawTicks = now;
                    RequestVisualRefresh();
                }
                e.Handled = true;
                return;
            }
        }

        _lastPointer = p;
        UpdateHoverDebugHit(p, e.KeyModifiers);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHoverDebugHit();
    }

}
