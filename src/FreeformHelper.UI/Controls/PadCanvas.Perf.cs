using System.Diagnostics;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private long _selectionPerfRevision;
    private long _visibleDrawPerfRevision;
    private long _renderPerfRevision;
    private PadCanvasSelectionPerfSnapshot _lastSelectionPerf;
    private PadCanvasVisibleDrawPerfSnapshot _lastVisibleDrawPerf;
    private PadCanvasRenderPerfSnapshot _lastRenderPerf;

    public PadCanvasSelectionPerfSnapshot GetSelectionPerfSnapshot()
    {
        return _lastSelectionPerf;
    }

    public PadCanvasVisibleDrawPerfSnapshot GetVisibleDrawPerfSnapshot()
    {
        return _lastVisibleDrawPerf;
    }

    public PadCanvasRenderPerfSnapshot GetRenderPerfSnapshot()
    {
        return _lastRenderPerf;
    }

    private void RecordSelectionPerf(
        long elapsedMs,
        int cadCandidates,
        int regularCandidates,
        int selectedCadCount,
        int selectedRegularCount,
        bool usedCadIndex,
        bool usedRegularIndex,
        bool regularOnly,
        bool additive)
    {
        _lastSelectionPerf = new PadCanvasSelectionPerfSnapshot(
            Revision: Interlocked.Increment(ref _selectionPerfRevision),
            ElapsedMs: elapsedMs,
            CadCandidates: cadCandidates,
            RegularCandidates: regularCandidates,
            SelectedCadCount: selectedCadCount,
            SelectedRegularCount: selectedRegularCount,
            UsedCadIndex: usedCadIndex,
            UsedRegularIndex: usedRegularIndex,
            RegularOnly: regularOnly,
            Additive: additive);
    }

    private void RecordVisibleDrawPerf(
        long elapsedMs,
        int regularCandidates,
        int cadCandidates,
        int visibleRegularSelected,
        int visibleRegularUnselected,
        int visibleCadSelected,
        int visibleCadUnselected,
        int regularDecimationStep,
        int cadDecimationStep,
        int regularDecimatedCount,
        int cadDecimatedCount,
        bool lowDetailMode,
        bool queryCacheHit,
        bool drawListCacheHit)
    {
        _lastVisibleDrawPerf = new PadCanvasVisibleDrawPerfSnapshot(
            Revision: Interlocked.Increment(ref _visibleDrawPerfRevision),
            ElapsedMs: elapsedMs,
            RegularCandidates: regularCandidates,
            CadCandidates: cadCandidates,
            VisibleRegularSelected: visibleRegularSelected,
            VisibleRegularUnselected: visibleRegularUnselected,
            VisibleCadSelected: visibleCadSelected,
            VisibleCadUnselected: visibleCadUnselected,
            RegularDecimationStep: regularDecimationStep,
            CadDecimationStep: cadDecimationStep,
            RegularDecimatedCount: regularDecimatedCount,
            CadDecimatedCount: cadDecimatedCount,
            LowDetailMode: lowDetailMode,
            QueryCacheHit: queryCacheHit,
            DrawListCacheHit: drawListCacheHit);
    }

    private void RecordRenderPerf(
        long totalElapsedMs,
        long backgroundElapsedMs,
        long visibleBuildElapsedMs,
        long geometryDrawElapsedMs,
        long overlayElapsedMs,
        long labelElapsedMs,
        long viewFrameRevision,
        bool showRegular,
        bool showCad,
        bool lowDetailMode,
        bool secondaryVisualsDeferred)
    {
        _lastRenderPerf = new PadCanvasRenderPerfSnapshot(
            Revision: Interlocked.Increment(ref _renderPerfRevision),
            TotalElapsedMs: totalElapsedMs,
            BackgroundElapsedMs: backgroundElapsedMs,
            VisibleBuildElapsedMs: visibleBuildElapsedMs,
            GeometryDrawElapsedMs: geometryDrawElapsedMs,
            OverlayElapsedMs: overlayElapsedMs,
            LabelElapsedMs: labelElapsedMs,
            ViewFrameRevision: viewFrameRevision,
            VisibleDrawRevision: _lastVisibleDrawPerf.Revision,
            ShowRegular: showRegular,
            ShowCad: showCad,
            LowDetailMode: lowDetailMode,
            SecondaryVisualsDeferred: secondaryVisualsDeferred);
    }

    private static long ElapsedMilliseconds(long startTimestamp)
    {
        return (long)Math.Round(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
    }
}

public readonly record struct PadCanvasSelectionPerfSnapshot(
    long Revision,
    long ElapsedMs,
    int CadCandidates,
    int RegularCandidates,
    int SelectedCadCount,
    int SelectedRegularCount,
    bool UsedCadIndex,
    bool UsedRegularIndex,
    bool RegularOnly,
    bool Additive);

public readonly record struct PadCanvasVisibleDrawPerfSnapshot(
    long Revision,
    long ElapsedMs,
    int RegularCandidates,
    int CadCandidates,
    int VisibleRegularSelected,
    int VisibleRegularUnselected,
    int VisibleCadSelected,
    int VisibleCadUnselected,
    int RegularDecimationStep,
    int CadDecimationStep,
    int RegularDecimatedCount,
    int CadDecimatedCount,
    bool LowDetailMode,
    bool QueryCacheHit,
    bool DrawListCacheHit)
{
    public bool CacheHit => DrawListCacheHit;
}

public readonly record struct PadCanvasRenderPerfSnapshot(
    long Revision,
    long TotalElapsedMs,
    long BackgroundElapsedMs,
    long VisibleBuildElapsedMs,
    long GeometryDrawElapsedMs,
    long OverlayElapsedMs,
    long LabelElapsedMs,
    long ViewFrameRevision,
    long VisibleDrawRevision,
    bool ShowRegular,
    bool ShowCad,
    bool LowDetailMode,
    bool SecondaryVisualsDeferred);
