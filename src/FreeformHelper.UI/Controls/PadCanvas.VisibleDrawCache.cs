using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    private long _visibleDrawQueryRevision;
    private long _visibleDrawCadDataRevision;
    private long _visibleDrawRegularDataRevision;
    private long _visibleDrawSelectionRevision;
    private long _visibleDrawHighlightRevision;
    private long _visibleDrawResourceRevision;
    private bool _hasVisibleDrawQueryCache;
    private PadCanvasVisibleDrawQueryCacheKey _visibleDrawQueryCacheKey;
    private PadCanvasVisibleDrawQueryCacheEntry _visibleDrawQueryCacheEntry;
    private bool _hasVisibleDrawListCache;
    private PadCanvasVisibleDrawListCacheKey _visibleDrawListCacheKey;
    private PadCanvasVisibleDrawListMetrics _visibleDrawListCacheMetrics;

    private void OnVisibleDrawCadDataChanged()
    {
        _visibleDrawCadDataRevision++;
        InvalidateVisibleDrawQueryCache();
    }

    private void OnVisibleDrawRegularDataChanged()
    {
        _visibleDrawRegularDataRevision++;
        InvalidateVisibleDrawQueryCache();
    }

    private void OnVisibleDrawSelectionChanged()
    {
        _visibleDrawSelectionRevision++;
        InvalidateVisibleDrawListCache();
    }

    private void OnVisibleDrawHighlightChanged()
    {
        _visibleDrawHighlightRevision++;
        InvalidateVisibleDrawListCache();
    }

    private void OnVisibleDrawResourceChanged()
    {
        _visibleDrawResourceRevision++;
        InvalidateVisibleDrawListCache();
    }

    private void InvalidateVisibleDrawQueryCache()
    {
        _hasVisibleDrawQueryCache = false;
        InvalidateVisibleDrawListCache();
    }

    private void InvalidateVisibleDrawListCache()
    {
        _hasVisibleDrawListCache = false;
    }

    private PadCanvasVisibleDrawQueryCacheKey CreateVisibleDrawQueryCacheKey(
        Rect2 worldViewport,
        bool showRegular,
        bool showCad)
    {
        return new PadCanvasVisibleDrawQueryCacheKey(
            WorldViewportMinX: worldViewport.MinX,
            WorldViewportMinY: worldViewport.MinY,
            WorldViewportMaxX: worldViewport.MaxX,
            WorldViewportMaxY: worldViewport.MaxY,
            ShowRegular: showRegular,
            ShowCad: showCad,
            CadDataRevision: _visibleDrawCadDataRevision,
            RegularDataRevision: _visibleDrawRegularDataRevision);
    }

    private bool TryUseVisibleDrawQueryCache(
        PadCanvasVisibleDrawQueryCacheKey key,
        out PadCanvasVisibleDrawQueryCacheEntry entry)
    {
        if (_hasVisibleDrawQueryCache && _visibleDrawQueryCacheKey.Equals(key))
        {
            entry = _visibleDrawQueryCacheEntry;
            return true;
        }

        entry = default;
        return false;
    }

    private PadCanvasVisibleDrawQueryCacheEntry StoreVisibleDrawQueryCache(
        PadCanvasVisibleDrawQueryCacheKey key,
        RegularPad[] regularCandidates,
        CadPad[] cadCandidates)
    {
        var entry = new PadCanvasVisibleDrawQueryCacheEntry(
            Revision: ++_visibleDrawQueryRevision,
            RegularCandidates: regularCandidates,
            CadCandidates: cadCandidates);
        _visibleDrawQueryCacheKey = key;
        _visibleDrawQueryCacheEntry = entry;
        _hasVisibleDrawQueryCache = true;
        return entry;
    }

    private PadCanvasVisibleDrawListCacheKey CreateVisibleDrawListCacheKey(
        PadCanvasVisibleDrawQueryCacheEntry queryEntry,
        bool lowDetailMode,
        double lowDetailZoomThreshold)
    {
        return new PadCanvasVisibleDrawListCacheKey(
            QueryRevision: queryEntry.Revision,
            Zoom: _zoom,
            LowDetailMode: lowDetailMode,
            LowDetailZoomThreshold: lowDetailZoomThreshold,
            SelectionRevision: _visibleDrawSelectionRevision,
            HighlightRevision: _visibleDrawHighlightRevision,
            ResourceRevision: _visibleDrawResourceRevision);
    }

    private bool TryUseVisibleDrawListCache(PadCanvasVisibleDrawListCacheKey key, out PadCanvasVisibleDrawListMetrics metrics)
    {
        if (_hasVisibleDrawListCache && _visibleDrawListCacheKey.Equals(key))
        {
            metrics = _visibleDrawListCacheMetrics;
            return true;
        }

        metrics = default;
        return false;
    }

    private void StoreVisibleDrawListCache(PadCanvasVisibleDrawListCacheKey key, PadCanvasVisibleDrawListMetrics metrics)
    {
        _visibleDrawListCacheKey = key;
        _visibleDrawListCacheMetrics = metrics;
        _hasVisibleDrawListCache = true;
    }

    private readonly record struct PadCanvasVisibleDrawQueryCacheKey(
        double WorldViewportMinX,
        double WorldViewportMinY,
        double WorldViewportMaxX,
        double WorldViewportMaxY,
        bool ShowRegular,
        bool ShowCad,
        long CadDataRevision,
        long RegularDataRevision);

    private readonly record struct PadCanvasVisibleDrawQueryCacheEntry(
        long Revision,
        RegularPad[] RegularCandidates,
        CadPad[] CadCandidates);

    private readonly record struct PadCanvasVisibleDrawListCacheKey(
        long QueryRevision,
        double Zoom,
        bool LowDetailMode,
        double LowDetailZoomThreshold,
        long SelectionRevision,
        long HighlightRevision,
        long ResourceRevision);

    private readonly record struct PadCanvasVisibleDrawListMetrics(
        int RegularCandidates,
        int CadCandidates,
        int VisibleRegularSelected,
        int VisibleRegularUnselected,
        int VisibleCadSelected,
        int VisibleCadUnselected,
        int RegularDecimationStep,
        int CadDecimationStep,
        int RegularDecimatedCount,
        int CadDecimatedCount);
}
