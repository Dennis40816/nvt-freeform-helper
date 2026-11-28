using Avalonia;
using Avalonia.Media;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// This partial class of <see cref="PadCanvas"/> is responsible for rendering
/// the CAD pads, regular pads, and any associated visual overlays onto the canvas.
/// It applies various visual styles based on pad properties and user settings.
/// </summary>
public sealed partial class PadCanvas
{
    internal static (bool DrawAxisLabels, bool DrawHoverDebug) ResolveTailVisualPolicy(
        bool secondaryVisualsDeferred,
        bool showAxisLabels)
    {
        return (DrawAxisLabels: showAxisLabels, DrawHoverDebug: !secondaryVisualsDeferred);
    }

    /// <summary>
    /// Overrides the base render method to perform custom drawing operations.
    /// This is the core rendering loop for the PadCanvas.
    /// </summary>
    /// <param name="context">The <see cref="DrawingContext"/> used for rendering.</param>
    public override void Render(DrawingContext context)
    {
        var renderStarted = TimeProvider.System.GetTimestamp();
        base.Render(context); // Call base implementation for default rendering behavior.

        // Fill the background of the canvas using the themed brush.
        var backgroundStarted = TimeProvider.System.GetTimestamp();
        var background = GetResourceBrush("BrushCanvasBackground", Brushes.Transparent);
        context.FillRectangle(background, new Rect(Bounds.Size));
        var backgroundElapsedMs = ElapsedMilliseconds(backgroundStarted);

        long visibleBuildElapsedMs = 0;
        long geometryDrawElapsedMs = 0;
        long overlayElapsedMs = 0;
        long labelElapsedMs = 0;
        var renderFrame = ResolveRenderFrame();

        // Push the world-to-screen matrix (zoom, pan, and Y-axis inversion) onto the drawing context.
        // All drawing operations inside the scope are transformed by this matrix.
        using (context.PushTransform(renderFrame.ViewFrame.WorldToScreen))
        {
            EnsureFrameCaches(renderFrame);
            var (regularStyle, cadStyle) = ResolvePadStyles(renderFrame);

            var visibleBuildStarted = TimeProvider.System.GetTimestamp();
            BuildVisibleDrawLists(
                renderFrame.ViewFrame.WorldViewport,
                renderFrame.ShowRegular,
                renderFrame.ShowCad,
                renderFrame.LowDetailMode,
                renderFrame.LowDetailZoomThreshold);
            visibleBuildElapsedMs = ElapsedMilliseconds(visibleBuildStarted);

            // --- Rendering Order ---
            // 1) Unselected pads (regular -> CAD), 2) selected pads (regular -> CAD).
            // This keeps selected outlines above all unselected geometry.
            var geometryStarted = TimeProvider.System.GetTimestamp();

            var padPainter = new PadCanvasVisiblePadPainter(
                _visibleRegularUnselected,
                _visibleCadUnselected,
                _visibleRegularSelected,
                _visibleCadSelected,
                (ctx, pad, selected, style) => DrawRegularPad(ctx, pad, selected, style.FillBase, style.FillSelected, style.FillFreeform, style.PenBase, style.PenSelected, style.PenFreeform, style.PenUnmatched),
                (ctx, pad, selected, style, lowDetail) => DrawCadPad(ctx, pad, selected, style.FillDefault, style.FillSelected, style.FillOverlap, style.Pen, style.PenSelected, style.PenOverlap, lowDetail));
            padPainter.Draw(context, renderFrame.ShowRegular, renderFrame.ShowCad, regularStyle, cadStyle, renderFrame.LowDetailMode);
            geometryDrawElapsedMs = ElapsedMilliseconds(geometryStarted);

            if (!_isBoxSelecting && !renderFrame.SecondaryVisualsDeferred)
            {
                overlayElapsedMs += DrawWorldOverlays(context, renderFrame);
            }
        } // End of world-to-screen transform scope.

        var (screenOverlayElapsedMs, screenLabelElapsedMs) = DrawScreenLayer(context, renderFrame);
        overlayElapsedMs += screenOverlayElapsedMs;
        labelElapsedMs += screenLabelElapsedMs;

        RecordRenderPerf(
            totalElapsedMs: ElapsedMilliseconds(renderStarted),
            backgroundElapsedMs: backgroundElapsedMs,
            visibleBuildElapsedMs: visibleBuildElapsedMs,
            geometryDrawElapsedMs: geometryDrawElapsedMs,
            overlayElapsedMs: overlayElapsedMs,
            labelElapsedMs: labelElapsedMs,
            viewFrameRevision: renderFrame.ViewFrame.Revision,
            showRegular: renderFrame.ShowRegular,
            showCad: renderFrame.ShowCad,
            lowDetailMode: renderFrame.LowDetailMode,
            secondaryVisualsDeferred: renderFrame.SecondaryVisualsDeferred);
    }

    // Ensure necessary caches are populated only when they are actually needed.
    private void EnsureFrameCaches(PadCanvasRenderFrame renderFrame)
    {
        if (renderFrame.ShowRegular)
        {
            EnsureRegularSpatialIndex();
        }

        if (renderFrame.ShowCad)
        {
            EnsureCadSpatialIndex();
        }

        if (renderFrame.ShowCad && ShouldEnsureCadGeometryCache(renderFrame.LowDetailMode))
        {
            EnsureCadGeometryCache();
        }
    }

    // The brushes and pens of each pad kind come from the style builder.
    private (RegularPadStyle? Regular, CadPadStyle? Cad) ResolvePadStyles(PadCanvasRenderFrame renderFrame)
    {
        var cadFillOpacity = renderFrame.LowDetailMode ? 0.0 : renderFrame.CadFillOpacity;
        var regFillOpacity = renderFrame.LowDetailMode ? 0.0 : renderFrame.RegularFillOpacity;
        var styleBuilder = new PadCanvasPadStyleBuilder(new PadCanvasPadStyleBuilder.Services(
            GetResourceColor,
            GetResourceAlpha,
            GetResourceDouble,
            GetBrush,
            ApplyOpacity));
        RegularPadStyle? regularStyle = null;
        CadPadStyle? cadStyle = null;

        if (renderFrame.ShowRegular)
        {
            regularStyle = styleBuilder.BuildRegular(new PadCanvasPadStyleBuilder.RegularInput(
                RegularLineColor,
                RegularSelectedColor,
                RegularSelectedFillOpacity,
                RegularLineWidth,
                renderFrame.RegularOpacity,
                regFillOpacity,
                renderFrame.SelectedOutlineWidth,
                renderFrame.HighlightStrokeWidthAdjust));
        }

        if (renderFrame.ShowCad)
        {
            if (!renderFrame.LowDetailMode && ColorCadByArea)
            {
                EnsureAreaColorCache(); // Ensure area-based color cache for CAD pads is built.
            }

            cadStyle = styleBuilder.BuildCad(new PadCanvasPadStyleBuilder.CadInput(
                CadLineColor,
                CadLineWidth,
                renderFrame.CadOpacity,
                cadFillOpacity,
                renderFrame.SelectedOutlineWidth,
                renderFrame.SelectedWidthAdd,
                renderFrame.HighlightStrokeWidthAdjust));
        }

        return (regularStyle, cadStyle);
    }

    // Draws the overlays that sit in world coordinates, right above the pads. Returns the elapsed milliseconds.
    private long DrawWorldOverlays(DrawingContext context, PadCanvasRenderFrame renderFrame)
    {
        var overlayStarted = TimeProvider.System.GetTimestamp();
        DrawDiffIndexOverlay(context, renderFrame.ShowCad, renderFrame.LowDetailMode, renderFrame.CadOpacity);
        if (ShowNotchToFullSeedOverlay)
        {
            DrawNotchToFullSeedOverlay(context, renderFrame.ShowCad, renderFrame.CadOpacity, renderFrame.SelectedOutlineWidth);
        }

        if (ShowNotchToFullCandidateOverlay)
        {
            DrawNotchToFullCandidateOverlay(context, renderFrame.ShowCad, renderFrame.CadOpacity, renderFrame.SelectedOutlineWidth);
        }

        return ElapsedMilliseconds(overlayStarted);
    }

    // Draws labels, the final To-Full result, the box selection and the axis labels on top of the pads.
    private (long OverlayElapsedMs, long LabelElapsedMs) DrawScreenLayer(DrawingContext context, PadCanvasRenderFrame renderFrame)
    {
        long overlayElapsedMs = 0;
        long labelElapsedMs = 0;
        var secondaryVisualsDeferred = renderFrame.SecondaryVisualsDeferred;
        var occupiedRatioLabelRects = new List<Rect>();
        if (!_isBoxSelecting && !secondaryVisualsDeferred && !ShouldSimplifyAggregateNotchPreviewVisuals())
        {
            var labelStarted = TimeProvider.System.GetTimestamp();
            // Priority: Match labels stay first; notch ratio labels are placed around them.
            DrawMatchAllocationLabels(context, occupiedRatioLabelRects);
            DrawNotchToRegularLabels(context, occupiedRatioLabelRects);
            DrawSimulationRegularLabels(context);
            labelElapsedMs += ElapsedMilliseconds(labelStarted);
        }
        else if (!_isBoxSelecting && !secondaryVisualsDeferred)
        {
            var simulationLabelStarted = TimeProvider.System.GetTimestamp();
            DrawSimulationRegularLabels(context);
            labelElapsedMs += ElapsedMilliseconds(simulationLabelStarted);
        }
        var overlayAfterTransformStarted = TimeProvider.System.GetTimestamp();
        DrawSimulationCopperPillar(context);
        DrawSelectionOverlay(context); // Draw any additional selection indicators (e.g., selection rectangle).

        // Draw To-Full final result near the end so it stays on top of other canvas geometry.
        var showCadFinal = ShowCad && CadPads is { Count: > 0 };
        var hasToFullPreview = _notchPreviewByCadId.Values.Any(item => item.IsToFullEnabled);
        if (!_isBoxSelecting && !secondaryVisualsDeferred && ShowNotchToFullFinalOverlay && showCadFinal && hasToFullPreview)
        {
            using (context.PushTransform(renderFrame.ViewFrame.WorldToScreen))
            {
                DrawNotchToFullOverlay(context, showCadFinal, renderFrame.CadOpacity, renderFrame.SelectedOutlineWidth);
            }
        }

        // Draw the box selection rectangle if active.
        if (_isBoxSelecting)
        {
            var rect = new Rect(_boxStart, _boxEnd).Normalize(); // Normalize ensures positive width/height.
            var selectionStroke = GetResourceBrush("BrushCanvasSelection", Brushes.Transparent);
            var selectionFill = GetResourceBrush("BrushCanvasSelectionFill", Brushes.Transparent);
            var selectionThickness = GetResourceDouble("CanvasSelectionStrokeThickness", GetResourceDouble("Space1", 1.0));
            var stroke = new Pen(selectionStroke, selectionThickness);
            context.DrawRectangle(stroke, rect);
            context.FillRectangle(selectionFill, rect);
        }
        overlayElapsedMs += ElapsedMilliseconds(overlayAfterTransformStarted);

        var tailVisualPolicy = ResolveTailVisualPolicy(secondaryVisualsDeferred, ShowAxisLabels);
        var labelTailStarted = TimeProvider.System.GetTimestamp();
        if (tailVisualPolicy.DrawAxisLabels)
        {
            DrawAxisLabels(context); // Keep IC / row / column labels visible during navigation.
        }

        if (tailVisualPolicy.DrawHoverDebug)
        {
            DrawHoverDebugOverlay(context); // Hover debug remains deferrable because it is secondary inspection UI.
        }

        labelElapsedMs += ElapsedMilliseconds(labelTailStarted);
        return (overlayElapsedMs, labelElapsedMs);
    }

    private void BuildVisibleDrawLists(Rect2 worldViewport, bool showRegular, bool showCad, bool lowDetailMode, double lowDetailZoomThreshold)
    {
        _visibleDrawListBuilder.Build(worldViewport, showRegular, showCad, lowDetailMode, lowDetailZoomThreshold);
    }

    private bool ShouldEnsureCadGeometryCache(bool lowDetailMode)
    {
        if (!lowDetailMode)
        {
            return true;
        }

        return _selectedCadIds.Count > 0 ||
               _highlightedCadIds.Count > 0 ||
               _notchPreviewByCadId.Count > 0 ||
               _diffIndexOverrideCadIds.Count > 0 ||
               _diffIndexAnchorCadPadIds.Count > 0;
    }

}
