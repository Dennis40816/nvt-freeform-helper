using Avalonia.Input;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private IPointer? _normalReleasePointer;

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!ReferenceEquals(e.Pointer, _normalReleasePointer))
        {
            ResetGestureState();
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        ResetGestureState();
    }

    private void ReleaseGestureCapture(IPointer pointer)
    {
        // Capture(null) raises capture-lost synchronously after the release path ends its gesture.
        var previousNormalReleasePointer = _normalReleasePointer;
        _normalReleasePointer = pointer;
        try
        {
            pointer.Capture(null);
        }
        finally
        {
            _normalReleasePointer = previousNormalReleasePointer;
        }
    }

    private void ResetGestureState()
    {
        var hadBoxSelection = _isBoxSelecting;
        _isMiddlePanning = false;
        _isSpacePanning = false;
        _isSpacePressed = false;
        _isLeftPointerDown = false;
        _isBoxSelecting = false;
        _lastPanRedrawTicks = 0;
        _lastBoxSelectionRedrawTicks = 0;
        if (hadBoxSelection)
        {
            RequestVisualRefresh();
        }
    }
}
