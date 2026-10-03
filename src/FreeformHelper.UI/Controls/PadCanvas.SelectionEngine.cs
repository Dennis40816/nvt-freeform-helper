using System.Diagnostics;
using Avalonia;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private sealed class PadCanvasSelectionEngine
    {
        private readonly PadCanvas _owner;

        public PadCanvasSelectionEngine(PadCanvas owner)
        {
            _owner = owner;
        }

        public HitResult HitTest(Point screenPoint, bool regularOnly = false)
        {
            var world = _owner.ScreenToWorld(screenPoint);
            var cadHit = TryHitCad(world);
            var regularHit = TryHitRegular(world);

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

        public HitResult HitTestForHoverDebug(Point screenPoint, bool regularOnly = false)
        {
            var world = _owner.ScreenToWorld(screenPoint);
            if (regularOnly)
            {
                return TryHitRegular(world, ignoreVisibility: true);
            }

            return HitTest(screenPoint, regularOnly: false);
        }

        public HitResult TryHitCad(Point2 world)
        {
            if (_owner.ShowCad && _owner.CadPads is { Count: > 0 })
            {
                _owner.EnsureCadSpatialIndex();

                if (_owner._cadIndex is not null)
                {
                    var indexed = TryHitCadByCandidates(world, _owner._cadIndex.Query(world));
                    if (indexed.kind != HitKind.None)
                    {
                        return indexed;
                    }
                }

                var fallback = TryHitCadByScan(world);
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

        private HitResult TryHitCadByScan(Point2 world)
        {
            if (_owner.CadPads is not { Count: > 0 })
            {
                return new HitResult(HitKind.None, 0);
            }

            return TryHitCadByCandidates(world, _owner.CadPads);
        }

        public HitResult TryHitRegular(Point2 world, bool ignoreVisibility = false)
        {
            if ((ignoreVisibility || _owner.ShowRegular) && _owner.RegularPads is { Count: > 0 })
            {
                _owner.EnsureRegularSpatialIndex();

                if (_owner._regularIndex is not null)
                {
                    var indexedPad = _owner._regularIndex.TryHit(world);
                    if (indexedPad is not null && indexedPad.Bounds.Contains(world))
                    {
                        return new HitResult(HitKind.Regular, indexedPad.Index);
                    }
                }

                // Row-local/column-local sizing can make per-row/per-column boundaries diverge.
                // In that case, bucket lookup may land on a neighbor cell; fall back to full scan.
                var linearFallback = TryHitRegularByScan(world);
                if (linearFallback.kind != HitKind.None)
                {
                    return linearFallback;
                }
            }

            return new HitResult(HitKind.None, 0);
        }

        public HitResult TryHitRegularByScan(Point2 world)
        {
            if (_owner.RegularPads is not { Count: > 0 })
            {
                return new HitResult(HitKind.None, 0);
            }

            RegularPad? best = null;
            foreach (var pad in _owner.RegularPads)
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

        public void SelectPadsInWorldRect(Rect2 wRect, bool additive, bool regularOnly)
        {
            var stopwatch = Stopwatch.StartNew();
            var cadCandidates = 0;
            var regularCandidates = 0;
            var usedCadIndex = false;
            var usedRegularIndex = false;
            var selectedCadIds = new HashSet<int>();
            var selectedRegularIndices = new HashSet<int>();

            if (!regularOnly && _owner.ShowCad && _owner.CadPads is { Count: > 0 })
            {
                _owner.EnsureCadSpatialIndex();
                if (_owner._cadIndex is not null)
                {
                    usedCadIndex = true;
                    foreach (var pad in _owner._cadIndex.Query(wRect))
                    {
                        cadCandidates++;
                        selectedCadIds.Add(pad.Id);
                    }
                }
                else
                {
                    cadCandidates = _owner.CadPads.Count;
                    foreach (var pad in _owner.CadPads)
                    {
                        if (pad.Bounds.Intersects(wRect))
                        {
                            selectedCadIds.Add(pad.Id);
                        }
                    }
                }
            }

            if (_owner.ShowRegular && _owner.RegularPads is { Count: > 0 })
            {
                _owner.EnsureRegularSpatialIndex();

                if (_owner._regularIndex is not null)
                {
                    usedRegularIndex = true;
                    foreach (var pad in _owner._regularIndex.Query(wRect))
                    {
                        regularCandidates++;
                        selectedRegularIndices.Add(pad.Index);
                    }
                }
                else
                {
                    regularCandidates = _owner.RegularPads.Count;
                    foreach (var pad in _owner.RegularPads)
                    {
                        if (pad.Bounds.Intersects(wRect))
                        {
                            selectedRegularIndices.Add(pad.Index);
                        }
                    }
                }
            }

            stopwatch.Stop();
            _owner.ApplyRectangleSelectionResult(
                selectedCadIds,
                selectedRegularIndices,
                additive,
                regularOnly,
                stopwatch.ElapsedMilliseconds,
                cadCandidates,
                regularCandidates,
                usedCadIndex,
                usedRegularIndex);
        }

        public Point GetPadCenter(HitResult hit)
        {
            if (hit.kind == HitKind.Cad && _owner.CadPads is { Count: > 0 })
            {
                foreach (var cad in _owner.CadPads)
                {
                    if (cad.Id != hit.idOrIndex)
                    {
                        continue;
                    }

                    return new Point(
                        (cad.Bounds.MinX + cad.Bounds.MaxX) / 2,
                        (cad.Bounds.MinY + cad.Bounds.MaxY) / 2);
                }
            }
            else if (hit.kind == HitKind.Regular && _owner.RegularPads is { Count: > 0 })
            {
                foreach (var pad in _owner.RegularPads)
                {
                    if (pad.Index != hit.idOrIndex)
                    {
                        continue;
                    }

                    return new Point(
                        (pad.Bounds.MinX + pad.Bounds.MaxX) / 2,
                        (pad.Bounds.MinY + pad.Bounds.MaxY) / 2);
                }
            }

            return new Point(0, 0);
        }
    }
}
