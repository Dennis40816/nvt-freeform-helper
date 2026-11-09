using Avalonia;
using Avalonia.Input;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Handles a right-button press on a <see cref="PadCanvas"/>: it updates the selection and
/// raises the context request of the pad under the pointer.
/// The canvas passes its own operations in through <see cref="Services"/>.
/// </summary>
internal sealed class PadCanvasContextPressHandler
{
    private readonly Services _services;

    public PadCanvasContextPressHandler(Services services)
    {
        _services = services;
    }

    /// <summary>
    /// The canvas operations the handler needs. The canvas passes its own methods in.
    /// </summary>
    internal sealed record Services(
        Func<Point, bool, PadCanvas.HitResult> HitTest,
        Action ClearSelection,
        Action RequestVisualRefresh,
        Func<PadCanvas.HitResult, bool> IsHitSelected,
        Action<PadCanvas.HitResult, bool, bool> ApplyHitSelection,
        Func<int, CadPad?> FindCadPad,
        Func<int, RegularPad?> FindRegularPad,
        Action<CadPad> RaiseCadPadContext,
        Action<RegularPad> RaiseRegularPadContext);

    /// <summary>
    /// Returns true when the press was handled. Returns false when the caller should go on with the other buttons.
    /// </summary>
    public bool Handle(Point pointer, KeyModifiers modifiers)
    {
        var hit = _services.HitTest(pointer, modifiers.HasFlag(KeyModifiers.Alt));
        if (hit.kind == PadCanvas.HitKind.None)
        {
            _services.ClearSelection(); // Clear selection if nothing was hit.
            _services.RequestVisualRefresh();
            return true;
        }

        var isControlDown = modifiers.HasFlag(KeyModifiers.Control);
        var isAlreadySelected = _services.IsHitSelected(hit);

        if (isControlDown)
        {
            // Toggle the item. Additive keeps the existing selection.
            _services.ApplyHitSelection(hit, true, true);
        }
        else if (!isAlreadySelected)
        {
            // Without Ctrl, select a pad that is not selected yet. A selected pad keeps the selection as it is.
            _services.ApplyHitSelection(hit, false, false);
        }

        return RaiseContext(hit);
    }

    private bool RaiseContext(PadCanvas.HitResult hit)
    {
        if (hit.kind == PadCanvas.HitKind.Cad && _services.FindCadPad(hit.idOrIndex) is { } cad)
        {
            _services.RaiseCadPadContext(cad);
            return true;
        }

        if (hit.kind == PadCanvas.HitKind.Regular && _services.FindRegularPad(hit.idOrIndex) is { } pad)
        {
            _services.RaiseRegularPadContext(pad);
            return true;
        }

        return false;
    }
}
