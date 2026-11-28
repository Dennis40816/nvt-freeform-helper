using System.Diagnostics;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private void BuildVisibleDrawLists(
        Rect2 worldViewport,
        bool showRegular,
        bool showCad,
        bool lowDetailMode,
        double lowDetailZoomThreshold)
    {
        var stopwatch = Stopwatch.StartNew();
        var queryCacheKey = CreateVisibleDrawQueryCacheKey(worldViewport, showRegular, showCad);
        var queryCacheHit = TryUseVisibleDrawQueryCache(queryCacheKey, out var queryEntry);
        if (!queryCacheHit)
        {
            var candidates = PadCanvasVisibleDrawListBuilder.Query(worldViewport, CreateQueryState(showCad, showRegular));
            queryEntry = StoreVisibleDrawQueryCache(queryCacheKey, candidates.Regular, candidates.Cad);
        }

        var drawListCacheKey = CreateVisibleDrawListCacheKey(queryEntry, lowDetailMode, lowDetailZoomThreshold);
        if (TryUseVisibleDrawListCache(drawListCacheKey, out var cachedMetrics))
        {
            stopwatch.Stop();
            RecordVisibleDrawPerf(
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

        var state = CreateVisibleDrawState(showRegular, showCad, lowDetailMode, lowDetailZoomThreshold, queryEntry.CadCandidates.Length);
        var metrics = PadCanvasVisibleDrawListBuilder.Build(
            queryEntry.RegularCandidates, queryEntry.CadCandidates, state, VisibleDrawBuffers);
        stopwatch.Stop();
        StoreVisibleDrawListCache(drawListCacheKey, metrics);
        RecordVisibleDrawPerf(
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

    internal PadCanvasVisibleDrawBuffers VisibleDrawBuffers =>
        new(_visibleRegularUnselected, _visibleRegularSelected, _visibleCadUnselected, _visibleCadSelected);

    private PadCanvasVisibleDrawState CreateVisibleDrawState(
        bool showRegular, bool showCad, bool lowDetailMode, double lowDetailZoomThreshold, int cadCandidateCount)
    {
        var maxRegularStep = showRegular && lowDetailMode
            ? GetResourceDouble("CanvasLowDetailRegularDecimationMaxStep", 4.0) : 4.0;
        var decimateCad = showCad && lowDetailMode && cadCandidateCount > 0;
        var maxCadUnselected = decimateCad ? GetResourceDouble("CanvasLowDetailCadMaxVisibleUnselected", 2500) : 2500;
        var minCadScreenSize = decimateCad ? GetResourceDouble("CanvasLowDetailCadMinScreenSize", 0.9) : 0.0;
        return new PadCanvasVisibleDrawState(
            showRegular, showCad, lowDetailMode, _zoom, lowDetailZoomThreshold,
            maxRegularStep, maxCadUnselected, minCadScreenSize, _selectedRegIdx, _selectedCadIds, _highlightedCadIds);
    }
}

internal static class PadCanvasVisibleDrawListBuilder
{
    public static (RegularPad[] Regular, CadPad[] Cad) Query(Rect2 worldViewport, in PadCanvasQueryState state) =>
        (BuildRegularCandidates(worldViewport, state), BuildCadCandidates(worldViewport, state));

    public static PadCanvas.PadCanvasVisibleDrawListMetrics Build(
        RegularPad[] regular, CadPad[] cad, in PadCanvasVisibleDrawState state, in PadCanvasVisibleDrawBuffers buffers)
    {
        buffers.RegularUnselected.Clear();
        buffers.RegularSelected.Clear();
        buffers.CadUnselected.Clear();
        buffers.CadSelected.Clear();

        var regularDecimationStep = ComputeRegularDecimationStep(state);
        var regularDecimatedCount = BuildVisibleRegularDrawLists(regular, regularDecimationStep, state, buffers);
        var cadBuildPlan = CreateCadBuildPlan(cad, state);
        var cadDecimatedCount = BuildVisibleCadDrawLists(cad, cadBuildPlan, state, buffers);

        return new PadCanvas.PadCanvasVisibleDrawListMetrics(
            RegularCandidates: regular.Length,
            CadCandidates: cad.Length,
            VisibleRegularSelected: buffers.RegularSelected.Count,
            VisibleRegularUnselected: buffers.RegularUnselected.Count,
            VisibleCadSelected: buffers.CadSelected.Count,
            VisibleCadUnselected: buffers.CadUnselected.Count,
            RegularDecimationStep: regularDecimationStep,
            CadDecimationStep: cadBuildPlan.DecimationStep,
            RegularDecimatedCount: regularDecimatedCount,
            CadDecimatedCount: cadDecimatedCount);
    }

    private static RegularPad[] BuildRegularCandidates(Rect2 worldViewport, in PadCanvasQueryState state)
    {
        if (!state.ShowRegular || state.RegularPads is not { Count: > 0 })
        {
            return Array.Empty<RegularPad>();
        }

        if (state.RegularIndex is not null)
        {
            return state.RegularIndex.Query(worldViewport).ToArray();
        }

        return state.RegularPads
            .Where(pad => pad.Bounds.Intersects(worldViewport))
            .ToArray();
    }

    private static CadPad[] BuildCadCandidates(Rect2 worldViewport, in PadCanvasQueryState state)
    {
        var _cadIndex = state.CadIndex;
        if (!state.ShowCad || state.CadPads is not { Count: > 0 })
        {
            return Array.Empty<CadPad>();
        }

        if (_cadIndex is not null)
        {
            return _cadIndex.Query(worldViewport).ToArray();
        }

        return state.CadPads
            .Where(pad => pad.Bounds.Intersects(worldViewport))
            .ToArray();
    }

    private static int ComputeRegularDecimationStep(in PadCanvasVisibleDrawState state)
    {
        if (!state.ShowRegular || !state.LowDetailMode)
        {
            return 1;
        }

        return PadDrawDensityPolicy.RegularStep(
            state.ShowRegular, state.LowDetailMode, state.LowDetailZoomThreshold, state.Zoom, state.MaxRegularStep);
    }

    private static int BuildVisibleRegularDrawLists(
        RegularPad[] regularCandidates, int regularDecimationStep,
        in PadCanvasVisibleDrawState state, in PadCanvasVisibleDrawBuffers buffers)
    {
        var decimatedCount = 0;
        foreach (var pad in regularCandidates)
        {
            var isSelected = state.SelectedRegularIndices.Contains(pad.Index);
            if (PadDrawDensityPolicy.IsRegularDecimated(isSelected, pad.Row, pad.Col, regularDecimationStep))
            {
                decimatedCount++;
                continue;
            }

            if (isSelected)
            {
                buffers.RegularSelected.Add(pad);
            }
            else
            {
                buffers.RegularUnselected.Add(pad);
            }
        }

        return decimatedCount;
    }

    private static CadVisibleBuildPlan CreateCadBuildPlan(CadPad[] cadCandidates, in PadCanvasVisibleDrawState state)
    {
        if (!state.ShowCad || !state.LowDetailMode || cadCandidates.Length == 0)
        {
            return new CadVisibleBuildPlan(DecimationStep: 1, MinScreenSize: 0.0);
        }

        var visibleUnselectedCandidates = 0;
        foreach (var pad in cadCandidates)
        {
            if (state.SelectedCadIds.Contains(pad.Id) || state.HighlightedCadIds.Contains(pad.Id))
            {
                continue;
            }

            visibleUnselectedCandidates++;
        }

        var decimationStep = PadDrawDensityPolicy.CadStep(visibleUnselectedCandidates, state.MaxCadUnselected);
        return new CadVisibleBuildPlan(decimationStep, state.MinCadScreenSize);
    }

    private static int BuildVisibleCadDrawLists(
        CadPad[] cadCandidates, CadVisibleBuildPlan plan,
        in PadCanvasVisibleDrawState state, in PadCanvasVisibleDrawBuffers buffers)
    {
        var decimatedCount = 0;
        var cadUnselectedCounter = 0;
        foreach (var pad in cadCandidates)
        {
            var isSelected = state.SelectedCadIds.Contains(pad.Id);
            var isHighlighted = state.HighlightedCadIds.Contains(pad.Id);

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
                var widthOnScreen = pad.Bounds.Width * state.Zoom;
                var heightOnScreen = pad.Bounds.Height * state.Zoom;
                if (widthOnScreen < plan.MinScreenSize && heightOnScreen < plan.MinScreenSize)
                {
                    decimatedCount++;
                    continue;
                }
            }

            if (isSelected)
            {
                buffers.CadSelected.Add(pad);
            }
            else
            {
                buffers.CadUnselected.Add(pad);
            }
        }

        return decimatedCount;
    }

    private readonly record struct CadVisibleBuildPlan(int DecimationStep, double MinScreenSize);
}
