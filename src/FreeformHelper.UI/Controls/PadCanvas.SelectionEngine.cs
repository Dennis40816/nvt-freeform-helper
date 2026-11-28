using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using static FreeformHelper.UI.Controls.PadCanvas;

namespace FreeformHelper.UI.Controls;

internal static class PadCanvasSelectionEngine
{
    public static HitResult HitTest(Point2 world, in PadCanvasQueryState state, bool regularOnly = false)
    {
        var cadHit = TryHitCad(world, state);
        var regularHit = TryHitRegular(world, state);

        // ALT modifier contract: "regular-only".
        // When enabled, CAD hit result must be ignored even if no regular pad is hit.
        if (regularOnly)
        {
            return regularHit;
        }

        if (cadHit.kind != HitKind.None)
        {
            return cadHit;
        }

        if (regularHit.kind != HitKind.None)
        {
            return regularHit;
        }

        return new HitResult(HitKind.None, 0);
    }

    private static HitResult TryHitCad(Point2 world, in PadCanvasQueryState state)
    {
        if (state.ShowCad && state.CadPads is { Count: > 0 })
        {
            if (state.CadIndex is not null)
            {
                var indexed = TryHitCadByCandidates(world, state.CadIndex.Query(world));
                if (indexed.kind != HitKind.None)
                {
                    return indexed;
                }
            }

            var fallback = TryHitCadByScan(world, state);
            if (fallback.kind != HitKind.None)
            {
                return fallback;
            }
        }

        return new HitResult(HitKind.None, 0);
    }

    private static HitResult TryHitCadByCandidates(Point2 world, IEnumerable<CadPad> candidates)
    {
        CadPad? best = null;
        foreach (var pad in candidates)
        {
            if (!pad.Bounds.Contains(world))
            {
                continue;
            }

            if (!pad.Polygon.Contains(world))
            {
                continue;
            }

            if (best is null || pad.Area < best.Area)
            {
                best = pad;
            }
        }

        return best is null
            ? new HitResult(HitKind.None, 0)
            : new HitResult(HitKind.Cad, best.Id);
    }

    private static HitResult TryHitCadByScan(Point2 world, in PadCanvasQueryState state)
    {
        if (state.CadPads is not { Count: > 0 })
        {
            return new HitResult(HitKind.None, 0);
        }

        return TryHitCadByCandidates(world, state.CadPads);
    }

    private static HitResult TryHitRegular(Point2 world, in PadCanvasQueryState state)
    {
        if (state.ShowRegular && state.RegularPads is { Count: > 0 })
        {
            if (state.RegularIndex is not null)
            {
                var indexedPad = state.RegularIndex.TryHit(world);
                if (indexedPad is not null && indexedPad.Bounds.Contains(world))
                {
                    return new HitResult(HitKind.Regular, indexedPad.Index);
                }
            }

            // Row-local/column-local sizing can make per-row/per-column boundaries diverge.
            // In that case, bucket lookup may land on a neighbor cell; fall back to full scan.
            var linearFallback = TryHitRegularByScan(world, state);
            if (linearFallback.kind != HitKind.None)
            {
                return linearFallback;
            }
        }

        return new HitResult(HitKind.None, 0);
    }

    private static HitResult TryHitRegularByScan(Point2 world, in PadCanvasQueryState state)
    {
        if (state.RegularPads is not { Count: > 0 })
        {
            return new HitResult(HitKind.None, 0);
        }

        RegularPad? best = null;
        foreach (var pad in state.RegularPads)
        {
            if (!pad.Bounds.Contains(world))
            {
                continue;
            }

            if (best is null || pad.Area < best.Area)
            {
                best = pad;
            }
        }

        return best is null
            ? new HitResult(HitKind.None, 0)
            : new HitResult(HitKind.Regular, best.Index);
    }

    public static (
        IReadOnlyCollection<int> CadIds,
        IReadOnlyCollection<int> RegularIndices,
        int CadCandidates,
        int RegularCandidates,
        bool UsedCadIndex,
        bool UsedRegularIndex) SelectPadsInWorldRect(Rect2 wRect, in PadCanvasQueryState state, bool regularOnly = false)
    {
        var cadCandidates = 0;
        var regularCandidates = 0;
        var usedCadIndex = false;
        var usedRegularIndex = false;
        var selectedCadIds = new HashSet<int>();
        var selectedRegularIndices = new HashSet<int>();

        if (!regularOnly && state.ShowCad && state.CadPads is { Count: > 0 })
        {
            if (state.CadIndex is not null)
            {
                usedCadIndex = true;
                foreach (var pad in state.CadIndex.Query(wRect))
                {
                    cadCandidates++;
                    selectedCadIds.Add(pad.Id);
                }
            }
            else
            {
                cadCandidates = state.CadPads.Count;
                foreach (var pad in state.CadPads)
                {
                    if (pad.Bounds.Intersects(wRect))
                    {
                        selectedCadIds.Add(pad.Id);
                    }
                }
            }
        }

        if (state.ShowRegular && state.RegularPads is { Count: > 0 })
        {
            if (state.RegularIndex is not null)
            {
                usedRegularIndex = true;
                foreach (var pad in state.RegularIndex.Query(wRect))
                {
                    regularCandidates++;
                    selectedRegularIndices.Add(pad.Index);
                }
            }
            else
            {
                regularCandidates = state.RegularPads.Count;
                foreach (var pad in state.RegularPads)
                {
                    if (pad.Bounds.Intersects(wRect))
                    {
                        selectedRegularIndices.Add(pad.Index);
                    }
                }
            }
        }

        return (selectedCadIds, selectedRegularIndices, cadCandidates, regularCandidates, usedCadIndex, usedRegularIndex);
    }
}
