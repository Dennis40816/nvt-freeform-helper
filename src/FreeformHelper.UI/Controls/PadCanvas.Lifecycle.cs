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
        this.GetObservable(FitBoundsOverrideProperty).Subscribe(_ => RequestVisualRefresh());
        // For properties that only affect colors or visual styles, only specific caches are reset, or just visual invalidation.
        this.GetObservable(ColorCadByAreaProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(AreaBucketToleranceProperty).Subscribe(_ => InvalidateCadAreaCaches());
        this.GetObservable(MaxAreaBucketsProperty).Subscribe(_ => InvalidateCadAreaCaches());
        this.GetObservable(ShowCadProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowRegularProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowAxisLabelsProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(HighlightUnmatchedProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(MatchThresholdProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(HighlightFreeformProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowSimulationRegularOverlayProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(SimulationRegularOverlayColorModeProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(SimulationRegularOverlayViewModeProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(SimulationRegularOverlayThresholdValueProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowSimulationCopperPillarProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(SimulationCopperCenterXProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(SimulationCopperCenterYProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(SimulationCopperDiameterProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(CadLineWidthProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(CadLineColorProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(CadLineOpacityProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(CadFillOpacityProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(RegularLineWidthProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(HighlightStrokeWidthAdjustProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(RegularLineColorProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(RegularLineOpacityProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(RegularFillOpacityProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(RegularSelectedColorProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(RegularSelectedFillOpacityProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowDiffIndexOverlayProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowNotchCanvasPreviewProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowNotchToRegularLabelsProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowNotchToFullSeedOverlayProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowNotchToFullCandidateOverlayProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(ShowNotchToFullFinalOverlayProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(NotchPreviewStageProperty).Subscribe(_ => RequestVisualRefresh());
        this.GetObservable(HighlightedCadPadIdsProperty).Subscribe(ids =>
        {
            _highlightedCadIds.Clear();
            if (ids is not null)
            {
                foreach (var id in ids)
                {
                    _highlightedCadIds.Add(id);
                }
            }

            OnVisibleDrawHighlightChanged();
            RequestVisualRefresh();
        });
        this.GetObservable(DiffIndexOverrideCadPadIdsProperty).Subscribe(ids =>
        {
            _diffIndexOverrideCadIds.Clear();
            if (ids is not null)
            {
                foreach (var id in ids)
                {
                    _diffIndexOverrideCadIds.Add(id);
                }
            }

            RequestVisualRefresh();
        });
        this.GetObservable(DiffIndexAnchorCadPadIdsProperty).Subscribe(ids =>
        {
            _diffIndexAnchorCadPadIds.Clear();
            if (ids is not null)
            {
                foreach (var id in ids)
                {
                    _diffIndexAnchorCadPadIds.Add(id);
                }
            }

            RequestVisualRefresh();
        });
        this.GetObservable(NotchCanvasPreviewItemsProperty).Subscribe(items =>
        {
            _notchPreviewByCadId.Clear();
            _notchToFullSeedPolygons.Clear();
            _notchToFullCandidatePolygons.Clear();
            if (items is not null)
            {
                foreach (var item in items)
                {
                    _notchPreviewByCadId[item.CadPadId] = item;
                    if (!item.IsToFullEnabled)
                    {
                        continue;
                    }

                    if (item.ToFullSeedPolygons.Count > 0)
                    {
                        _notchToFullSeedPolygons.AddRange(item.ToFullSeedPolygons);
                    }

                    if (item.ToFullCandidatePolygons.Count > 0)
                    {
                        _notchToFullCandidatePolygons.AddRange(item.ToFullCandidatePolygons);
                    }
                }
            }

            RequestVisualRefresh();
        });
        this.GetObservable(SimulationRegularOverlayItemsProperty).Subscribe(items =>
        {
            _simulationOverlayByRegularPadId.Clear();
            _simulationOverlayMinValue = 0d;
            _simulationOverlayMaxValue = 0d;
            _simulationOverlayMaxAbsDelta = 0d;
            _simulationOverlayAutoNegativeClampAbs = 0d;
            _simulationOverlayAutoPositiveClamp = 0d;
            if (items is { Count: > 0 })
            {
                foreach (var item in items)
                {
                    _simulationOverlayByRegularPadId[item.RegularPadId] = item;
                }

                var autoScaleRange = SimulationColorScaleResolver.ComputeAutoScaleRange(items.Select(static item => item.DisplayValue));
                _simulationOverlayMinValue = autoScaleRange.Minimum;
                _simulationOverlayMaxValue = autoScaleRange.Maximum;
                _simulationOverlayAutoNegativeClampAbs = autoScaleRange.NegativeClampAbs;
                _simulationOverlayAutoPositiveClamp = autoScaleRange.PositiveClamp;
                _simulationOverlayMaxAbsDelta = items.Max(static item => Math.Abs(item.DeltaValue));
            }

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
