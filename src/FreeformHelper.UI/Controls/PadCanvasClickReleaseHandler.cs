using Avalonia;
using Avalonia.Input;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Handles the release of a left-button click on a <see cref="PadCanvas"/> that did not become a box selection:
/// it changes the selection for the pad under the pointer, with the Shift and Ctrl variants.
/// The canvas passes its own operations in through <see cref="Services"/>.
/// </summary>
internal sealed class PadCanvasClickReleaseHandler
{
    private readonly Services _services;

    public PadCanvasClickReleaseHandler(Services services)
    {
        _services = services;
    }

    /// <summary>
    /// The canvas operations the handler needs. The canvas passes its own methods in.
    /// </summary>
    internal sealed record Services(
        Func<Point, bool, PadCanvas.HitResult> HitTest,
        Func<PadCanvas.HitResult?> GetAnchor,
        Action<PadCanvas.HitResult?> SetAnchor,
        Action<PadCanvas.HitResult, PadCanvas.HitResult> ApplyExtendSelection,
        Action ClearSelection,
        Action RequestVisualRefresh,
        Func<int> GetSelectedCount,
        Func<PadCanvas.HitResult, bool> IsHitSelected,
        Action<PadCanvas.HitResult> SchedulePendingSingleSelection,
        Action<PadCanvas.HitResult, bool, bool> ApplyHitSelection);

    public void Handle(Point releasePosition, KeyModifiers modifiers)
    {
        var hit = _services.HitTest(releasePosition, modifiers.HasFlag(KeyModifiers.Alt));
        var isControlDown = modifiers.HasFlag(KeyModifiers.Control);

        // Shift extends the selection from the anchor. Ctrl and Shift together act like Ctrl.
        if (modifiers.HasFlag(KeyModifiers.Shift) && !isControlDown && hit.kind != PadCanvas.HitKind.None)
        {
            ExtendSelection(hit);
            return;
        }

        ApplyClick(hit, isControlDown || modifiers.HasFlag(KeyModifiers.Shift));
    }

    private void ExtendSelection(PadCanvas.HitResult hit)
    {
        if (_services.GetAnchor() is { } anchor)
        {
            _services.ApplyExtendSelection(anchor, hit);
            return;
        }

        // Without an anchor, select the pad and make it the anchor.
        _services.ClearSelection();
        _services.ApplyHitSelection(hit, false, false);
        _services.SetAnchor(hit);
    }

    private void ApplyClick(PadCanvas.HitResult hit, bool additive)
    {
        if (hit.kind == PadCanvas.HitKind.None && !additive)
        {
            _services.ClearSelection(); // Nothing hit and no modifier: clear the selection and the anchor.
            _services.SetAnchor(null);
            _services.RequestVisualRefresh();
            return;
        }

        // A click on one of several selected pads may start a drag. Wait before it becomes the only selection.
        if (!additive && _services.IsHitSelected(hit) && _services.GetSelectedCount() > 1)
        {
            _services.SchedulePendingSingleSelection(hit);
            return;
        }

        _services.ApplyHitSelection(hit, additive, additive);
        _services.SetAnchor(hit);
    }
}
