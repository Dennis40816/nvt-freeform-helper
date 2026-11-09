using System.Reactive.Linq;
using Avalonia;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Controls;

public sealed partial class PadCanvas
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PadCanvas"/> class.
    /// </summary>
    public PadCanvas()
    {
        ClipToBounds = true; // Ensure content outside the control's bounds is clipped.
        _selectionEngine = new PadCanvasSelectionEngine(this);
        _visibleDrawListBuilder = new PadCanvasVisibleDrawListBuilder(this);

        AttachedToVisualTree += OnAttachedToVisualTree;
        ResourcesChanged += OnResourcesChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;

        // Subscribe to changes in properties that affect rendering or internal caches.
        // Use granular cache invalidation to avoid rebuilding unrelated caches.
        this.GetObservable(CadPadsProperty).Subscribe(_ => InvalidateCadDataCaches());
        this.GetObservable(RegularPadsProperty).Subscribe(_ => InvalidateRegularDataCaches());
        // For properties that only affect colors or visual styles, only specific caches are reset, or just visual invalidation.
        PadCanvasRefreshSubscriptions.SubscribeVisualRefresh(this, () => RequestVisualRefresh());
        this.GetObservable(AreaBucketToleranceProperty).Subscribe(_ => InvalidateCadAreaCaches());
        this.GetObservable(MaxAreaBucketsProperty).Subscribe(_ => InvalidateCadAreaCaches());
        this.GetObservable(HighlightedCadPadIdsProperty).Subscribe(ids =>
        {
            PadCanvasOverlayData.ReplaceIds(_highlightedCadIds, ids);
            OnVisibleDrawHighlightChanged();
            RequestVisualRefresh();
        });
        this.GetObservable(DiffIndexOverrideCadPadIdsProperty).Subscribe(ids =>
        {
            PadCanvasOverlayData.ReplaceIds(_diffIndexOverrideCadIds, ids);
            RequestVisualRefresh();
        });
        this.GetObservable(DiffIndexAnchorCadPadIdsProperty).Subscribe(ids =>
        {
            PadCanvasOverlayData.ReplaceIds(_diffIndexAnchorCadPadIds, ids);
            RequestVisualRefresh();
        });
        this.GetObservable(NotchCanvasPreviewItemsProperty).Subscribe(items =>
        {
            PadCanvasOverlayData.ReplaceNotchPreview(_notchPreviewByCadId, _notchToFullSeedPolygons, _notchToFullCandidatePolygons, items);
            RequestVisualRefresh();
        });
        this.GetObservable(SimulationRegularOverlayItemsProperty).Subscribe(items =>
        {
            var scale = PadCanvasOverlayData.ReplaceSimulationOverlay(
                _simulationOverlayByRegularPadId,
                items,
                static item => item.RegularPadId,
                static item => item.DisplayValue,
                static item => item.DeltaValue);
            _simulationOverlayMinValue = scale.MinValue;
            _simulationOverlayMaxValue = scale.MaxValue;
            _simulationOverlayMaxAbsDelta = scale.MaxAbsDelta;
            _simulationOverlayAutoNegativeClampAbs = scale.AutoNegativeClampAbs;
            _simulationOverlayAutoPositiveClamp = scale.AutoPositiveClamp;
            RequestVisualRefresh();
        });
        this.GetObservable(PadMatchLinksProperty).Subscribe(links =>
        {
            RebuildMatchLinkIndex(links);
            RequestVisualRefresh();
        });
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        ApplyThemeDefaults();
    }

    private void OnResourcesChanged(object? sender, Avalonia.Controls.ResourcesChangedEventArgs e)
    {
        RefreshThemeResources();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        RefreshThemeResources();
    }

    private void RefreshThemeResources()
    {
        _labelLayoutCache.Clear();
        _cadAreaColorCache = null;
        _cadAreaBrushCache = null;
        OnVisibleDrawResourceChanged();
        ApplyThemeDefaults();
        RequestVisualRefresh();
    }

    private void ApplyThemeDefaults()
    {
        if (!IsSet(CadLineWidthProperty))
        {
            var width = GetResourceDouble("CanvasCadLineWidth");
            if (width > 0)
            {
                CadLineWidth = width;
            }
        }

        if (!IsSet(RegularLineWidthProperty))
        {
            var width = GetResourceDouble("CanvasRegularLineWidth");
            if (width > 0)
            {
                RegularLineWidth = width;
            }
        }

        if (!IsSet(CadLineColorProperty))
        {
            var color = GetResourceColor("ColorCanvasCadLine");
            if (color != default)
            {
                CadLineColor = color;
            }
        }

        if (!IsSet(RegularLineColorProperty))
        {
            var color = GetResourceColor("ColorCanvasRegularLine");
            if (color != default)
            {
                RegularLineColor = color;
            }
        }
    }

    /// <summary>
    /// Invalidates the visual display and resets CAD-derived caches.
    /// This is called when CAD pads change.
    /// </summary>
    private void InvalidateCadDataCaches()
    {
        _cachedWorldBounds = null;
        _cadAreaColorCache = null;
        _cadAreaBrushCache = null;
        _cadGeometryCache = null;
        _cadIndex = null;
        _cadPadById = null;
        OnVisibleDrawCadDataChanged();
        ClearHoverDebugHit(invalidateVisual: false);
        RequestVisualRefresh();
    }

    /// <summary>
    /// Invalidates the visual display and resets regular-grid derived caches.
    /// This is called when regular pads change.
    /// </summary>
    private void InvalidateRegularDataCaches()
    {
        _cachedWorldBounds = null;
        _regularIndex = null;
        _regularPadById = null;
        OnVisibleDrawRegularDataChanged();
        ClearHoverDebugHit(invalidateVisual: false);
        RequestVisualRefresh();
    }

    /// <summary>
    /// Invalidates area-color caches without touching geometry/spatial caches.
    /// </summary>
    private void InvalidateCadAreaCaches()
    {
        _cadAreaColorCache = null;
        _cadAreaBrushCache = null;
        RequestVisualRefresh();
    }

    private void RebuildMatchLinkIndex(IReadOnlyList<PadMatchLink>? links)
    {
        _matchLinksByCadId.Clear();
        _matchLinksByRegularId.Clear();
        if (links is null || links.Count == 0)
        {
            return;
        }

        foreach (var link in links)
        {
            if (!_matchLinksByCadId.TryGetValue(link.CadPadId, out var cadLinks))
            {
                cadLinks = new List<PadMatchLink>();
                _matchLinksByCadId[link.CadPadId] = cadLinks;
            }

            cadLinks.Add(link);

            if (!_matchLinksByRegularId.TryGetValue(link.RegularPadId, out var regularLinks))
            {
                regularLinks = new List<PadMatchLink>();
                _matchLinksByRegularId[link.RegularPadId] = regularLinks;
            }

            regularLinks.Add(link);
        }
    }

    /// <summary>
    /// Raises the <see cref="ViewChanged"/> event.
    /// </summary>
    private void RaiseViewChanged()
    {
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }
}
