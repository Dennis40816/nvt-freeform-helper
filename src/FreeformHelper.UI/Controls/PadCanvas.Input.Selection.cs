using Avalonia;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private void CommitSelectionMutation(
        Action<HashSet<int>, HashSet<int>> mutate,
        bool clearExisting,
        bool invalidateVisual,
        bool notifySelectionChanged,
        Action? afterMutation = null)
    {
        if (clearExisting)
        {
            ResetSelectionSets();
        }

        mutate(_selectedCadIds, _selectedRegIdx);
        afterMutation?.Invoke();

        if (invalidateVisual)
        {
            InvalidateVisual();
        }

        if (notifySelectionChanged)
        {
            RaiseSelectionChanged();
        }
    }

    private void ResetSelectionSets()
    {
        _selectedCadIds.Clear();
        _selectedRegIdx.Clear();
    }

    private void ApplyRectangleSelectionResult(
        IReadOnlyCollection<int> cadPadIds,
        IReadOnlyCollection<int> regularPadIndices,
        bool additive,
        bool regularOnly,
        long elapsedMs,
        int cadCandidates,
        int regularCandidates,
        bool usedCadIndex,
        bool usedRegularIndex)
    {
        CommitSelectionMutation(
            (selectedCadIds, selectedRegIdx) =>
            {
                foreach (var id in cadPadIds)
                {
                    selectedCadIds.Add(id);
                }

                foreach (var index in regularPadIndices)
                {
                    selectedRegIdx.Add(index);
                }
            },
            clearExisting: !additive,
            invalidateVisual: false,
            notifySelectionChanged: false,
            afterMutation: () => RecordSelectionPerf(
                elapsedMs: elapsedMs,
                cadCandidates: cadCandidates,
                regularCandidates: regularCandidates,
                selectedCadCount: _selectedCadIds.Count,
                selectedRegularCount: _selectedRegIdx.Count,
                usedCadIndex: usedCadIndex,
                usedRegularIndex: usedRegularIndex,
                regularOnly: regularOnly,
                additive: additive));
    }

    private void ReplaceSelectionCore(
        IEnumerable<int> cadPadIds,
        IEnumerable<int> regularPadIndices,
        bool invalidateVisual,
        bool notifySelectionChanged,
        Action? afterMutation = null)
    {
        CommitSelectionMutation(
            (selectedCadIds, selectedRegIdx) =>
            {
                foreach (var id in cadPadIds.Distinct())
                {
                    selectedCadIds.Add(id);
                }

                foreach (var index in regularPadIndices.Distinct())
                {
                    selectedRegIdx.Add(index);
                }
            },
            clearExisting: true,
            invalidateVisual: invalidateVisual,
            notifySelectionChanged: notifySelectionChanged,
            afterMutation: afterMutation);
    }

    private bool TryHandleAxisLabelClick(Point screenPoint, bool additive)
    {
        if (_axisLabelHits.Count == 0 || RegularPads is null || RegularPads.Count == 0)
        {
            return false;
        }

        var hit = HitTestAxisLabel(screenPoint);
        if (hit is null)
        {
            return false;
        }

        CancelPendingSingleSelection();
        _lastSingleSelectedPad = null;
        ApplyAxisLabelSelection(hit.Value, additive);
        return true;
    }

    private AxisLabelHit? HitTestAxisLabel(Point screenPoint)
    {
        for (var i = _axisLabelHits.Count - 1; i >= 0; i--)
        {
            var hit = _axisLabelHits[i];
            if (hit.Bounds.Contains(screenPoint))
            {
                return hit;
            }
        }

        return null;
    }

    private void ApplyAxisLabelSelection(AxisLabelHit hit, bool additive)
    {
        CommitSelectionMutation(
            (selectedCadIds, selectedRegIdx) =>
            {
                if (RegularPads is not { Count: > 0 })
                {
                    return;
                }

                switch (hit.Kind)
                {
                    case AxisLabelKind.Row:
                        foreach (var pad in RegularPads)
                        {
                            if (pad.Row == hit.Index)
                            {
                                selectedRegIdx.Add(pad.Index);
                            }
                        }
                        break;
                    case AxisLabelKind.Column:
                        foreach (var pad in RegularPads)
                        {
                            if (pad.Col == hit.Index)
                            {
                                selectedRegIdx.Add(pad.Index);
                            }
                        }
                        break;
                    case AxisLabelKind.Ic:
                        foreach (var pad in RegularPads)
                        {
                            if (pad.IcIndex == hit.Index)
                            {
                                selectedRegIdx.Add(pad.Index);
                            }
                        }
                        break;
                }
            },
            clearExisting: !additive,
            invalidateVisual: true,
            notifySelectionChanged: true);
    }

    // Defines the kind of item hit during a hit test.
    private enum HitKind { None, Cad, Regular }

    // Represents the result of a hit test, indicating the type of item and its ID/index.
    private readonly record struct HitResult(HitKind kind, int idOrIndex);

    /// <summary>
    /// Performs a hit test at the given screen point to identify which pad (if any) was clicked.
    /// Prioritizes CAD pads over regular pads, and smaller pads over larger overlapping ones.
    /// </summary>
    /// <param name="screenPoint">The point in screen coordinates.</param>
    /// <param name="regularOnly">If true, hit-test runs in regular-only mode (CAD is ignored).</param>
    /// <returns>A <see cref="HitResult"/> indicating the hit item.</returns>
    private HitResult HitTest(Point screenPoint, bool regularOnly = false)
    {
        return _selectionEngine.HitTest(screenPoint, regularOnly);
    }

    private HitResult HitTestForHoverDebug(Point screenPoint, bool regularOnly = false)
    {
        return _selectionEngine.HitTestForHoverDebug(screenPoint, regularOnly);
    }

    private HitResult TryHitCad(Point2 world)
    {
        return _selectionEngine.TryHitCad(world);
    }

    private HitResult TryHitRegular(Point2 world)
    {
        return _selectionEngine.TryHitRegular(world);
    }

    private HitResult TryHitRegularByScan(Point2 world)
    {
        return _selectionEngine.TryHitRegularByScan(world);
    }

    /// <summary>
    /// Applies a selection change based on a hit test result and modifiers.
    /// </summary>
    /// <param name="hit">The result of the hit test.</param>
    /// <param name="toggle">If true, selection is toggled; otherwise, item is selected.</param>
    /// <param name="additive">If true, selection is added/removed; otherwise, it's a new selection.</param>
    private void ApplyHitSelection(HitResult hit, bool toggle, bool additive)
    {
        CommitSelectionMutation(
            (selectedCadIds, selectedRegIdx) =>
            {
                if (hit.kind == HitKind.Cad)
                {
                    if (toggle && selectedCadIds.Contains(hit.idOrIndex))
                    {
                        selectedCadIds.Remove(hit.idOrIndex);
                    }
                    else
                    {
                        selectedCadIds.Add(hit.idOrIndex);
                    }
                }
                else if (hit.kind == HitKind.Regular)
                {
                    if (toggle && selectedRegIdx.Contains(hit.idOrIndex))
                    {
                        selectedRegIdx.Remove(hit.idOrIndex);
                    }
                    else
                    {
                        selectedRegIdx.Add(hit.idOrIndex);
                    }
                }
            },
            clearExisting: !additive,
            invalidateVisual: true,
            notifySelectionChanged: true);
    }

    /// <summary>
    /// Applies an extend selection, selecting all pads between an anchor pad and a current hit pad.    
    /// This clears existing selections and then selects the range.
    /// </summary>
    /// <param name="anchorHit">The result of the hit test for the anchor pad (last single selected).</param>
    /// <param name="currentHit">The result of the hit test for the currently clicked pad.</param>      
    private void ApplyExtendSelection(HitResult anchorHit, HitResult currentHit)
    {
        // Determine the bounding rectangle covering both anchor and current hit.
        Point anchorCenter = GetPadCenter(anchorHit);
        Point currentCenter = GetPadCenter(currentHit);

        var worldRect = new Rect2(
            Math.Min(anchorCenter.X, currentCenter.X),
            Math.Min(anchorCenter.Y, currentCenter.Y),
            Math.Max(anchorCenter.X, currentCenter.X),
            Math.Max(anchorCenter.Y, currentCenter.Y)
        );
        // Inflate slightly to ensure pads whose centers are exactly on the edge are included
        worldRect = new Rect2(worldRect.MinX - 1.0, worldRect.MinY - 1.0, worldRect.MaxX + 1.0, worldRect.MaxY + 1.0);

        SelectPadsInWorldRect(worldRect, additive: false);

        InvalidateVisual();
        RaiseSelectionChanged();
    }
    // Helper method to get the center of a pad.
    private Point GetPadCenter(HitResult hit)
    {
        return _selectionEngine.GetPadCenter(hit);
    }

    /// <summary>
    /// Handles double-click events, which typically trigger a context menu or specific action for the clicked item.
    /// </summary>
    /// <param name="screenPoint">The screen coordinates of the double click.</param>
    /// <returns>True if the double click was handled, false otherwise.</returns>
    private bool HandleDoubleClick(Point screenPoint)
    {
        var hit = HitTest(screenPoint);
        if (hit.kind == HitKind.None)
        {
            ClearSelectionInternal(); // Clear selection if double-clicked on empty space.
            InvalidateVisual();
            return true;
        }

        CancelPendingSingleSelection(); // Cancel any pending single selections.
        var alreadySelected = IsHitSelected(hit);
        var totalSelected = _selectedCadIds.Count + _selectedRegIdx.Count;
        // If the item is not selected, or it's the only selected item, select it.
        if (!alreadySelected || totalSelected <= 1)
        {
            ApplyHitSelection(hit, toggle: false, additive: false);
        }

        // Raise context menu events based on the type of double-clicked item.
        if (hit.kind == HitKind.Cad && CadPads is { Count: > 0 })
        {
            var cad = CadPads.FirstOrDefault(p => p.Id == hit.idOrIndex);
            if (cad is not null)
            {
                CadPadContextRequested?.Invoke(this, new CadPadContextRequestedEventArgs(cad));
                return true;
            }
        }

        if (hit.kind == HitKind.Regular && RegularPads is { Count: > 0 })
        {
            var pad = RegularPads.FirstOrDefault(p => p.Index == hit.idOrIndex);
            if (pad is not null)
            {
                RegularPadActivated?.Invoke(this, new RegularPadContextRequestedEventArgs(pad));
                RegularPadContextRequested?.Invoke(this, new RegularPadContextRequestedEventArgs(pad));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Schedules a single selection event after a short delay.
    /// This is used to differentiate between a click for selection and the start of a drag.
    /// </summary>
    private void SchedulePendingSingleSelection(HitResult hit)
    {
        CancelPendingSingleSelection(); // Ensure no other pending selection is active.

        _pendingSingleHit = hit;
        _pendingSelectionVersion = _selectionVersion; // Capture current selection version.
        _pendingSingleTimer = new DispatcherTimer { Interval = PendingSingleSelectionDelay };
        _pendingSingleTimer.Tick += (_, _) =>
        {
            _pendingSingleTimer?.Stop();
            _pendingSingleTimer = null;
            ApplyPendingSingleSelection(); // Execute the pending selection after delay.
        };
        _pendingSingleTimer.Start();
    }

    /// <summary>
    /// Cancels any currently scheduled single selection.
    /// </summary>
    private void CancelPendingSingleSelection()
    {
        _pendingSingleTimer?.Stop();
        _pendingSingleTimer = null;
        _pendingSingleHit = null;
    }

    /// <summary>
    /// Applies the pending single selection if it's still valid (i.e., no other selection changes occurred).
    /// </summary>
    private void ApplyPendingSingleSelection()
    {
        if (_pendingSingleHit is null) return;

        // If the selection changed since this pending selection was scheduled, invalidate it.
        if (_selectionVersion != _pendingSelectionVersion)
        {
            _pendingSingleHit = null;
            return;
        }

        var hit = _pendingSingleHit.Value;
        _pendingSingleHit = null;
        ApplyHitSelection(hit, toggle: false, additive: false); // Apply the single selection.
    }

    /// <summary>
    /// Checks if the item identified by <see cref="HitResult"/> is currently selected.
    /// </summary>
    /// <param name="hit">The hit test result.</param>
    /// <returns>True if the item is selected, false otherwise.</returns>
    private bool IsHitSelected(HitResult hit)
    {
        if (hit.kind == HitKind.Cad)
        {
            return _selectedCadIds.Contains(hit.idOrIndex);
        }

        if (hit.kind == HitKind.Regular)
        {
            return _selectedRegIdx.Contains(hit.idOrIndex);
        }

        return false;
    }

    /// <summary>
    /// Applies a box selection to pads within the specified screen rectangle.
    /// </summary>
    /// <param name="screenRect">The selection rectangle in screen coordinates.</param>
    /// <param name="additive">If true, adds to existing selection; otherwise, clears previous selection.</param>
    /// <param name="regularOnly">If true, only regular pads are selected inside the box.</param>
    private void ApplyBoxSelection(Rect screenRect, bool additive, bool regularOnly)
    {
        // Convert screen rectangle corners to world coordinates.
        var worldA = ScreenToWorld(screenRect.TopLeft);
        var worldB = ScreenToWorld(screenRect.BottomRight);

        // Create a world-coordinate rectangle from the converted points.
        var wRect = new Rect2(
            Math.Min(worldA.X, worldB.X),
            Math.Min(worldA.Y, worldB.Y),
            Math.Max(worldA.X, worldB.X),
            Math.Max(worldA.Y, worldB.Y));

        SelectPadsInWorldRect(wRect, additive, regularOnly);
    }

    /// <summary>
    /// Selects all pads within the specified world-coordinate rectangle.
    /// </summary>
    /// <param name="wRect">The selection rectangle in world coordinates.</param>
    /// <param name="additive">If true, adds to existing selection; otherwise, clears previous selection.</param>
    /// <param name="regularOnly">If true, only regular pads are selected inside the rectangle.</param>
    private void SelectPadsInWorldRect(Rect2 wRect, bool additive, bool regularOnly = false)
    {
        _selectionEngine.SelectPadsInWorldRect(wRect, additive, regularOnly);
    }

    /// <summary>
    /// Clears all currently selected CAD and regular pads internally.
    /// </summary>
    private void ClearSelectionInternal(bool notify = true)
    {
        CommitSelectionMutation(
            static (_, _) => { },
            clearExisting: true,
            invalidateVisual: false,
            notifySelectionChanged: notify,
            afterMutation: () => RecordSelectionPerf(
                elapsedMs: 0,
                cadCandidates: 0,
                regularCandidates: 0,
                selectedCadCount: 0,
                selectedRegularCount: 0,
                usedCadIndex: false,
                usedRegularIndex: false,
                regularOnly: false,
                additive: false));
    }

    /// <summary>
    /// Public method to clear all selections and trigger a redraw.
    /// </summary>
    public void ClearSelection()
    {
        ClearSelectionInternal();
        InvalidateVisual();
    }

    /// <summary>
    /// Public method to programmatically set the current selection of pads.
    /// </summary>
    /// <param name="cadPadIds">The IDs of CAD pads to select.</param>
    /// <param name="regularPadIndices">The indices of regular pads to select.</param>
    public void SetSelection(IEnumerable<int> cadPadIds, IEnumerable<int> regularPadIndices)
    {
        ReplaceSelectionCore(
            cadPadIds,
            regularPadIndices,
            invalidateVisual: true,
            notifySelectionChanged: true,
            afterMutation: () => RecordSelectionPerf(
                elapsedMs: 0,
                cadCandidates: _selectedCadIds.Count,
                regularCandidates: _selectedRegIdx.Count,
                selectedCadCount: _selectedCadIds.Count,
                selectedRegularCount: _selectedRegIdx.Count,
                usedCadIndex: false,
                usedRegularIndex: false,
                regularOnly: false,
                additive: false));
    }

    /// <summary>
    /// Selects all available CAD and regular pads on the canvas.
    /// </summary>
    public void SelectAllPads()
    {
        ReplaceSelectionCore(
            CadPads?.Select(pad => pad.Id) ?? Enumerable.Empty<int>(),
            RegularPads?.Select(pad => pad.Index) ?? Enumerable.Empty<int>(),
            invalidateVisual: true,
            notifySelectionChanged: true,
            afterMutation: () => RecordSelectionPerf(
                elapsedMs: 0,
                cadCandidates: _selectedCadIds.Count,
                regularCandidates: _selectedRegIdx.Count,
                selectedCadCount: _selectedCadIds.Count,
                selectedRegularCount: _selectedRegIdx.Count,
                usedCadIndex: false,
                usedRegularIndex: false,
                regularOnly: false,
                additive: false));
    }

    /// <summary>
    /// Raises the <see cref="SelectionChanged"/> event, incrementing the selection version.
    /// </summary>
    private void RaiseSelectionChanged()
    {
        _selectionVersion++; // Increment version to invalidate old pending selections.
        OnVisibleDrawSelectionChanged();
        SelectionChanged?.Invoke(this, new PadCanvasSelectionChangedEventArgs(_selectedCadIds, _selectedRegIdx));
    }

    /// <summary>
    /// Event arguments for when a context menu is requested for a regular pad.
    /// </summary>
    public sealed class RegularPadContextRequestedEventArgs : EventArgs
    {
        public RegularPadContextRequestedEventArgs(RegularPad pad) => Pad = pad;
        public RegularPad Pad { get; }
    }

    /// <summary>
    /// Event arguments for when a context menu is requested for a CAD pad.
    /// </summary>
    public sealed class CadPadContextRequestedEventArgs : EventArgs
    {
        public CadPadContextRequestedEventArgs(CadPad pad) => Pad = pad;
        public CadPad Pad { get; }
    }
}
