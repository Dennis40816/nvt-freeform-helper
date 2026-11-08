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
        var renderLowDetailMode = false;
        var renderShowRegular = false;
        var renderShowCad = false;
        var secondaryVisualsDeferred = false;
        var renderFrame = ResolveRenderFrame();

        // Create a transformation matrix from world coordinates to screen coordinates.
        // This accounts for zoom, pan, and inverts the Y-axis (world Y-up to screen Y-down).
        var worldToScreen = renderFrame.ViewFrame.WorldToScreen;

        // Push the transformation matrix onto the drawing context.
        // All subsequent drawing operations will be transformed by this matrix.
        using (context.PushTransform(worldToScreen))
        {
            var worldViewport = renderFrame.ViewFrame.WorldViewport;
            // Determine if CAD or regular pads should be shown based on properties and data availability.
            var showRegular = renderFrame.ShowRegular;
            var showCad = renderFrame.ShowCad;
            var cadOpacity = renderFrame.CadOpacity;
            var regOpacity = renderFrame.RegularOpacity;
            var cadFillOpacity = renderFrame.CadFillOpacity;
            var regFillOpacity = renderFrame.RegularFillOpacity;
            var lowDetailZoomThreshold = renderFrame.LowDetailZoomThreshold;
            var selectedWidthAdd = renderFrame.SelectedWidthAdd;
            var lowDetailMode = renderFrame.LowDetailMode;
            renderLowDetailMode = lowDetailMode;
            renderShowRegular = showRegular;
            renderShowCad = showCad;
            secondaryVisualsDeferred = renderFrame.SecondaryVisualsDeferred;
            var highlightStrokeWidthAdjust = renderFrame.HighlightStrokeWidthAdjust;
            var selectedOutlineWidth = renderFrame.SelectedOutlineWidth;

            // Ensure necessary caches are populated only when they are actually needed.
            if (showRegular)
            {
                EnsureRegularSpatialIndex();
            }

            if (showCad)
            {
                EnsureCadSpatialIndex();
            }

            if (showCad && ShouldEnsureCadGeometryCache(lowDetailMode))
            {
                EnsureCadGeometryCache();
            }

            if (lowDetailMode)
            {
                cadFillOpacity = 0.0;
                regFillOpacity = 0.0;
            }

            // The brushes and pens of each pad kind come from the style builder.
            RegularPadStyle? regularStyle = null;
            CadPadStyle? cadStyle = null;
            var styleBuilder = new PadCanvasPadStyleBuilder(new PadCanvasPadStyleBuilder.Services(
                GetResourceColor,
                GetResourceAlpha,
                GetResourceDouble,
                GetBrush,
                ApplyOpacity));

            if (showRegular)
            {
                regularStyle = styleBuilder.BuildRegular(new PadCanvasPadStyleBuilder.RegularInput(
                    RegularLineColor,
                    RegularSelectedColor,
                    RegularSelectedFillOpacity,
                    RegularLineWidth,
                    regOpacity,
                    regFillOpacity,
                    selectedOutlineWidth,
                    highlightStrokeWidthAdjust));
            }

            if (showCad)
            {
                if (!lowDetailMode && ColorCadByArea)
                {
                    EnsureAreaColorCache(); // Ensure area-based color cache for CAD pads is built.
                }

                cadStyle = styleBuilder.BuildCad(new PadCanvasPadStyleBuilder.CadInput(
                    CadLineColor,
                    CadLineWidth,
                    cadOpacity,
                    cadFillOpacity,
                    selectedOutlineWidth,
                    selectedWidthAdd,
                    highlightStrokeWidthAdjust));
            }

            var visibleBuildStarted = TimeProvider.System.GetTimestamp();
            BuildVisibleDrawLists(worldViewport, showRegular, showCad, lowDetailMode, lowDetailZoomThreshold);
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
            padPainter.Draw(context, showRegular, showCad, regularStyle, cadStyle, lowDetailMode);
            geometryDrawElapsedMs = ElapsedMilliseconds(geometryStarted);

            if (!_isBoxSelecting && !secondaryVisualsDeferred)
            {
                var overlayStarted = TimeProvider.System.GetTimestamp();
                DrawDiffIndexOverlay(context, showCad, lowDetailMode, cadOpacity);
                if (ShowNotchToFullSeedOverlay)
                {
                    DrawNotchToFullSeedOverlay(context, showCad, cadOpacity, selectedOutlineWidth);
                }

                if (ShowNotchToFullCandidateOverlay)
                {
                    DrawNotchToFullCandidateOverlay(context, showCad, cadOpacity, selectedOutlineWidth);
                }
                overlayElapsedMs += ElapsedMilliseconds(overlayStarted);
            }
        } // End of world-to-screen transform scope.

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
            var cadOpacityFinal = Math.Clamp(CadLineOpacity, 0.0, 1.0);
            var highlightStrokeWidthAdjustFinal = HighlightStrokeWidthAdjust;
            var selectedWidthAddFinal = GetResourceDouble("CanvasCadSelectedStrokeWidthAdd", 0.8);
            var selectedOutlineBaseFinal = Math.Max(CadLineWidth, RegularLineWidth);
            var selectedOutlineWidthFinal = Math.Max(
                selectedOutlineBaseFinal + 0.05,
                selectedOutlineBaseFinal + selectedWidthAddFinal + highlightStrokeWidthAdjustFinal);
            using (context.PushTransform(worldToScreen))
            {
                DrawNotchToFullOverlay(context, showCadFinal, cadOpacityFinal, selectedOutlineWidthFinal);
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

        RecordRenderPerf(
            totalElapsedMs: ElapsedMilliseconds(renderStarted),
            backgroundElapsedMs: backgroundElapsedMs,
            visibleBuildElapsedMs: visibleBuildElapsedMs,
            geometryDrawElapsedMs: geometryDrawElapsedMs,
            overlayElapsedMs: overlayElapsedMs,
            labelElapsedMs: labelElapsedMs,
            viewFrameRevision: renderFrame.ViewFrame.Revision,
            showRegular: renderShowRegular,
            showCad: renderShowCad,
            lowDetailMode: renderLowDetailMode,
            secondaryVisualsDeferred: secondaryVisualsDeferred);
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
