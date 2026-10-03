using Avalonia;
using Avalonia.Input;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    /// <summary>
    /// Handles <see cref="PointerWheelEventArgs"/> events for zooming.
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var pos = e.GetPosition(this); // Mouse position in screen coordinates.
        var delta = e.Delta.Y; // Scroll direction and magnitude.
        var hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // Touchpad two-finger scrolling typically reports precision wheel deltas.
        // Use it for panning, while keeping Ctrl+wheel zoom behavior unchanged.
        if (!hasCtrl && ShouldTreatWheelAsPan(e.Delta))
        {
            var panScale = GetResourceDouble("CanvasWheelPanScale", 48.0);
            _pan += new Vector(e.Delta.X * panScale, e.Delta.Y * panScale);
            InvalidateViewFrameSnapshot();
            BeginTransientLowDetailNavigation();
            RequestViewRefresh();
            e.Handled = true;
            return;
        }

        var factor = Math.Pow(1.15, delta); // Default zoom factor.
        if (hasCtrl)
        {
            factor = Math.Pow(1.25, delta); // Increased zoom factor if Control key is pressed.
        }

        ZoomAt(pos, factor); // Apply zoom centered at mouse position.
        e.Handled = true;
    }

    private static bool ShouldTreatWheelAsPan(Vector delta)
    {
        var hasHorizontal = Math.Abs(delta.X) > 1e-3;
        if (hasHorizontal)
        {
            return true;
        }

        var absY = Math.Abs(delta.Y);
        var isCoarseStep = Math.Abs(absY - Math.Round(absY)) < 1e-6;
        return absY > 1e-3 && !isCoarseStep;
    }

    /// <summary>
    /// Handles <see cref="OnKeyDown(KeyEventArgs)"/> keyboard shortcuts.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Space)
        {
            _isSpacePressed = true; // Set flag for spacebar panning.
            return;
        }

        if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
        {
            RefreshHoverDebugHitFromCurrentPointer(e.KeyModifiers | KeyModifiers.Alt);
            return;
        }

        if (e.Key == Key.F) // 'F' key to fit content.
        {
            FitToContent();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.A) // Ctrl+A to select all.
        {
            SelectAllPads();
            e.Handled = true;
            return;
        }

        // --- View Nudging with Shift + Arrow Keys ---
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            var step = 35.0; // Nudge amount.
            if (e.Key == Key.Left) _pan += new Vector(step, 0);
            else if (e.Key == Key.Right) _pan += new Vector(-step, 0);
            else if (e.Key == Key.Up) _pan += new Vector(0, step);
            else if (e.Key == Key.Down) _pan += new Vector(0, -step);
            else return; // If it's not an arrow key, don't handle.

            InvalidateViewFrameSnapshot();
            BeginTransientLowDetailNavigation();
            RequestViewRefresh();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles <see cref="OnKeyUp(KeyEventArgs)"/> events.
    /// </summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.Key == Key.Space)
        {
            _isSpacePressed = false; // Clear flag for spacebar panning.
            return;
        }

        if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
        {
            RefreshHoverDebugHitFromCurrentPointer(e.KeyModifiers);
        }
    }

    /// <summary>
    /// Checks if the spacebar is currently pressed.
    /// </summary>
    private bool IsSpaceDown()
    {
        return _isSpacePressed;
    }
}
