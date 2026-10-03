using System.Diagnostics;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private sealed class PadCanvasVisibleDrawListBuilder
    {
        private readonly PadCanvas _owner;

        public PadCanvasVisibleDrawListBuilder(PadCanvas owner)
        {
            _owner = owner;
        }

        public void Build(
            Rect2 worldViewport,
            bool showRegular,
            bool showCad,
            bool lowDetailMode,
            double lowDetailZoomThreshold)
        {
            var stopwatch = Stopwatch.StartNew();
            var queryCacheKey = _owner.CreateVisibleDrawQueryCacheKey(worldViewport, showRegular, showCad);
            var queryCacheHit = _owner.TryUseVisibleDrawQueryCache(queryCacheKey, out var queryEntry);
            if (!queryCacheHit)
            {
                queryEntry = _owner.StoreVisibleDrawQueryCache(
                    queryCacheKey,
                    BuildRegularCandidates(worldViewport, showRegular),
                    BuildCadCandidates(worldViewport, showCad));
            }

            var drawListCacheKey = _owner.CreateVisibleDrawListCacheKey(queryEntry, lowDetailMode, lowDetailZoomThreshold);
            if (_owner.TryUseVisibleDrawListCache(drawListCacheKey, out var cachedMetrics))
            {
                stopwatch.Stop();
                _owner.RecordVisibleDrawPerf(
                    elapsedMs: stopwatch.ElapsedMilliseconds,
                    regularCandidates: cachedMetrics.RegularCandidates,
                    cadCandidates: cachedMetrics.CadCandidates,
                    visibleRegularSelected: cachedMetrics.VisibleRegularSelected,
                    visibleRegularUnselected: cachedMetrics.VisibleRegularUnselected,
                    visibleCadSelected: cachedMetrics.VisibleCadSelected,
                    visibleCadUnselected: cachedMetrics.VisibleCadUnselected,
                    regularDecimationStep: cachedMetrics.RegularDecimationStep,
                    cadDecimationStep: cachedMetrics.CadDecimationStep,
                    regularDecimatedCount: cachedMetrics.RegularDecimatedCount,
                    cadDecimatedCount: cachedMetrics.CadDecimatedCount,
                    lowDetailMode: lowDetailMode,
                    queryCacheHit: queryCacheHit,
                    drawListCacheHit: true);
                return;
            }

            _owner._visibleRegularUnselected.Clear();
            _owner._visibleRegularSelected.Clear();
            _owner._visibleCadUnselected.Clear();
            _owner._visibleCadSelected.Clear();

            var regularDecimationStep = ComputeRegularDecimationStep(showRegular, lowDetailMode, lowDetailZoomThreshold);
            var regularDecimatedCount = BuildVisibleRegularDrawLists(queryEntry.RegularCandidates, regularDecimationStep);

            var cadBuildPlan = CreateCadBuildPlan(showCad, lowDetailMode, queryEntry.CadCandidates);
            var cadDecimatedCount = BuildVisibleCadDrawLists(queryEntry.CadCandidates, cadBuildPlan);

            stopwatch.Stop();
            var metrics = new PadCanvasVisibleDrawListMetrics(
                RegularCandidates: queryEntry.RegularCandidates.Length,
                CadCandidates: queryEntry.CadCandidates.Length,
                VisibleRegularSelected: _owner._visibleRegularSelected.Count,
                VisibleRegularUnselected: _owner._visibleRegularUnselected.Count,
                VisibleCadSelected: _owner._visibleCadSelected.Count,
                VisibleCadUnselected: _owner._visibleCadUnselected.Count,
                RegularDecimationStep: regularDecimationStep,
                CadDecimationStep: cadBuildPlan.DecimationStep,
                RegularDecimatedCount: regularDecimatedCount,
                CadDecimatedCount: cadDecimatedCount);
            _owner.StoreVisibleDrawListCache(drawListCacheKey, metrics);
            _owner.RecordVisibleDrawPerf(
                elapsedMs: stopwatch.ElapsedMilliseconds,
                regularCandidates: metrics.RegularCandidates,
                cadCandidates: metrics.CadCandidates,
                visibleRegularSelected: metrics.VisibleRegularSelected,
                visibleRegularUnselected: metrics.VisibleRegularUnselected,
                visibleCadSelected: metrics.VisibleCadSelected,
                visibleCadUnselected: metrics.VisibleCadUnselected,
                regularDecimationStep: metrics.RegularDecimationStep,
                cadDecimationStep: metrics.CadDecimationStep,
                regularDecimatedCount: metrics.RegularDecimatedCount,
                cadDecimatedCount: metrics.CadDecimatedCount,
                lowDetailMode: lowDetailMode,
                queryCacheHit: queryCacheHit,
                drawListCacheHit: false);
        }

        private RegularPad[] BuildRegularCandidates(Rect2 worldViewport, bool showRegular)
        {
            if (!showRegular || _owner.RegularPads is not { Count: > 0 })
            {
                return Array.Empty<RegularPad>();
            }

            if (_owner._regularIndex is not null)
            {
                return _owner._regularIndex.Query(worldViewport).ToArray();
            }

            return _owner.RegularPads
                .Where(pad => pad.Bounds.Intersects(worldViewport))
                .ToArray();
        }

        private CadPad[] BuildCadCandidates(Rect2 worldViewport, bool showCad)
        {
            if (!showCad || _owner.CadPads is not { Count: > 0 })
            {
                return Array.Empty<CadPad>();
            }

            if (_owner._cadIndex is not null)
            {
                return _owner._cadIndex.Query(worldViewport).ToArray();
            }

            return _owner.CadPads
                .Where(pad => pad.Bounds.Intersects(worldViewport))
                .ToArray();
        }

        private int ComputeRegularDecimationStep(bool showRegular, bool lowDetailMode, double lowDetailZoomThreshold)
        {
            if (!showRegular || !lowDetailMode)
            {
                return 1;
            }

            var maxStep = Math.Max(1, (int)Math.Round(_owner.GetResourceDouble("CanvasLowDetailRegularDecimationMaxStep", 4.0)));
            var zoomRatio = lowDetailZoomThreshold / Math.Max(_owner._zoom, 1e-6);
            return Math.Clamp((int)Math.Ceiling(zoomRatio), 1, maxStep);
        }

        private int BuildVisibleRegularDrawLists(RegularPad[] regularCandidates, int regularDecimationStep)
        {
            var decimatedCount = 0;
            foreach (var pad in regularCandidates)
            {
                var isSelected = _owner._selectedRegIdx.Contains(pad.Index);
                if (!isSelected && regularDecimationStep > 1)
                {
                    if ((Math.Abs(pad.Row) % regularDecimationStep) != 0 && (Math.Abs(pad.Col) % regularDecimationStep) != 0)
                    {
                        decimatedCount++;
                        continue;
                    }
                }

                if (isSelected)
                {
                    _owner._visibleRegularSelected.Add(pad);
                }
                else
                {
                    _owner._visibleRegularUnselected.Add(pad);
                }
            }

            return decimatedCount;
        }

        private CadVisibleBuildPlan CreateCadBuildPlan(bool showCad, bool lowDetailMode, CadPad[] cadCandidates)
        {
            if (!showCad || !lowDetailMode || cadCandidates.Length == 0)
            {
                return new CadVisibleBuildPlan(DecimationStep: 1, MinScreenSize: 0.0);
            }

            var maxVisibleUnselected = Math.Max(100, (int)Math.Round(_owner.GetResourceDouble("CanvasLowDetailCadMaxVisibleUnselected", 2500)));
            var visibleUnselectedCandidates = 0;
            foreach (var pad in cadCandidates)
            {
                if (_owner._selectedCadIds.Contains(pad.Id) || _owner._highlightedCadIds.Contains(pad.Id))
                {
                    continue;
                }

                visibleUnselectedCandidates++;
            }

            var decimationStep = Math.Max(1, (int)Math.Ceiling(visibleUnselectedCandidates / (double)maxVisibleUnselected));
            var minScreenSize = _owner.GetResourceDouble("CanvasLowDetailCadMinScreenSize", 0.9);
            return new CadVisibleBuildPlan(decimationStep, minScreenSize);
        }

        private int BuildVisibleCadDrawLists(CadPad[] cadCandidates, CadVisibleBuildPlan plan)
        {
            var decimatedCount = 0;
            var cadUnselectedCounter = 0;
            foreach (var pad in cadCandidates)
            {
                var isSelected = _owner._selectedCadIds.Contains(pad.Id);
                var isHighlighted = _owner._highlightedCadIds.Contains(pad.Id);

                if (!isSelected && plan.DecimationStep > 1)
                {
                    if (!isHighlighted)
                    {
                        cadUnselectedCounter++;
                        if (((cadUnselectedCounter - 1) % plan.DecimationStep) != 0)
                        {
                            decimatedCount++;
                            continue;
                        }
                    }
                }

                if (!isSelected && plan.MinScreenSize > 0.0 && !isHighlighted)
                {
                    var widthOnScreen = pad.Bounds.Width * _owner._zoom;
                    var heightOnScreen = pad.Bounds.Height * _owner._zoom;
                    if (widthOnScreen < plan.MinScreenSize && heightOnScreen < plan.MinScreenSize)
                    {
                        decimatedCount++;
                        continue;
                    }
                }

                if (isSelected)
                {
                    _owner._visibleCadSelected.Add(pad);
                }
                else
                {
                    _owner._visibleCadUnselected.Add(pad);
                }
            }

            return decimatedCount;
        }

        private readonly record struct CadVisibleBuildPlan(int DecimationStep, double MinScreenSize);
    }
}
