using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>Handles coordinate planner array corner dragging.</summary>
internal sealed class CoordinatePlannerArrayCornerDragController(
    Control owner,
    Func<CoordinatePlannerWorkspaceViewModel?> getViewModel,
    Func<PadCanvas> getCanvas)
{
    private string? _draggedArrayCornerKey;

    public void ConfigureHandle(
        Ellipse handle,
        string key,
        string label)
    {
        handle.Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(handle, label);
        handle.PointerPressed += (_, e) => OnArrayCornerHandlePointerPressed(key, e);
    }

    private void OnArrayCornerHandlePointerPressed(
        string key,
        PointerPressedEventArgs e)
    {
        if (getViewModel() is not { } viewModel ||
            !e.GetCurrentPoint(owner).Properties.IsLeftButtonPressed ||
            !viewModel.BeginCustomArrayCornerEdit(key))
        {
            return;
        }

        _draggedArrayCornerKey = key;
        e.Pointer.Capture(owner);
        MoveDraggedArrayCorner(e);
        e.Handled = true;
    }

    public void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        if (!e.GetCurrentPoint(owner).Properties.IsLeftButtonPressed)
        {
            CompleteDraggedArrayCorner(e);
            return;
        }

        MoveDraggedArrayCorner(e);
        e.Handled = true;
    }

    public void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        MoveDraggedArrayCorner(e);
        CompleteDraggedArrayCorner(e);
        e.Handled = true;
    }

    private void MoveDraggedArrayCorner(PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null ||
            getViewModel() is not { } viewModel)
        {
            return;
        }

        var canvas = getCanvas();
        var worldPoint = canvas.ScreenToWorld(e.GetPosition(canvas));
        viewModel.MoveCustomArrayCornerToWorldPoint(_draggedArrayCornerKey, worldPoint);
    }

    private void CompleteDraggedArrayCorner(PointerEventArgs e)
    {
        if (_draggedArrayCornerKey is null)
        {
            return;
        }

        if (getViewModel() is { } viewModel)
        {
            viewModel.CompleteCustomArrayCornerEdit(_draggedArrayCornerKey);
        }

        _draggedArrayCornerKey = null;
        e.Pointer.Capture(null);
    }
}
