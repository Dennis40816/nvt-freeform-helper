using FreeformHelper.UI.Controls;

namespace FreeformHelper.UI;

/// <summary>
/// Defines a narrow interface that allows a ViewModel to request view-level actions
/// related to a canvas control without needing to know the concrete implementation
/// of the canvas. This promotes loose coupling between the ViewModel and the View.
/// </summary>
public interface ICanvasHost
{
    /// <summary>
    /// Adjusts the canvas view to fit all its content within the visible area.
    /// </summary>
    void FitToContent();

    /// <summary>
    /// Resets the canvas view to its default zoom and pan settings.
    /// </summary>
    void ResetView();

    /// <summary>
    /// Invalidates the canvas, triggering a redraw of its content.
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Clears any currently selected items on the canvas.
    /// </summary>
    void ClearSelection();

    /// <summary>
    /// Sets the selection of items on the canvas.
    /// </summary>
    /// <param name="cadPadIds">A read-only collection of IDs of CAD pads to select.</param>
    /// <param name="regularPadIndices">A read-only collection of indices of regular pads to select.</param>
    void SetSelection(IReadOnlyCollection<int> cadPadIds, IReadOnlyCollection<int> regularPadIndices);

    /// <summary>
    /// Moves the current viewport to center on the selected pads without changing zoom.
    /// </summary>
    void FocusSelection();

    /// <summary>
    /// Centers the selected pads and enforces a minimum zoom level for clearer inspection.
    /// </summary>
    /// <param name="minZoom">Minimum zoom to apply after focusing.</param>
    void FocusSelectionWithMinZoom(double minZoom);

    /// <summary>
    /// Returns the latest selection pipeline performance snapshot from canvas.
    /// </summary>
    PadCanvasSelectionPerfSnapshot GetSelectionPerfSnapshot();

    /// <summary>
    /// Returns the latest visible-draw pipeline performance snapshot from canvas.
    /// </summary>
    PadCanvasVisibleDrawPerfSnapshot GetVisibleDrawPerfSnapshot();
}
