using Avalonia;
using Avalonia.Controls;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// A custom Avalonia <see cref="Control"/> that serves as the main drawing surface
/// for displaying <see cref="CadPad"/> and <see cref="RegularPad"/> objects.
/// It handles rendering, user interaction (panning, zooming, selection), and display settings.
/// </summary>
public sealed partial class PadCanvas : Control
{
    /// <summary>
    /// Event raised when a context menu is requested for a CAD pad.
    /// </summary>
    public event EventHandler<CadPadContextRequestedEventArgs>? CadPadContextRequested;

    /// <summary>
    /// Event raised when the selection of pads on the canvas changes.
    /// </summary>
    public event EventHandler<PadCanvasSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Event raised when a context menu is requested for a regular pad.
    /// </summary>
    public event EventHandler<RegularPadContextRequestedEventArgs>? RegularPadContextRequested;

    /// <summary>
    /// Event raised when a regular pad is activated via double click.
    /// </summary>
    public event EventHandler<RegularPadContextRequestedEventArgs>? RegularPadActivated;

    /// <summary>
    /// Event raised when the view (zoom or pan) of the canvas changes.
    /// </summary>
    public event EventHandler? ViewChanged;

    public int? TryGetRegularPadIndexAt(Point screenPoint)
    {
        var hit = HitTest(screenPoint, regularOnly: true);
        return hit.kind == HitKind.Regular ? hit.idOrIndex : null;
    }
}
