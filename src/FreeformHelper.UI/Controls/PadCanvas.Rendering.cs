using System.Diagnostics;
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
        var renderStarted = Stopwatch.GetTimestamp();
        base.Render(context); // Call base implementation for default rendering behavior.

        // Fill the background of the canvas using the themed brush.
        var backgroundStarted = Stopwatch.GetTimestamp();
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

            // --- Initialize Brushes and Pens for Regular Pads ---
            IBrush? regFillBase = null; // Base fill color for regular pads.
            IBrush? regFillSelected = null; // Fill color for selected regular pads.
            IBrush? regFillFreeform = null; // Fill color for freeform regular pads.
            Pen? regPenBase = null; // Base stroke pen for regular pads.
            Pen? regPenSelected = null; // Stroke pen for selected regular pads.
            Pen? regPenFreeform = null; // Stroke pen for freeform regular pads.
            Pen? regPenUnmatched = null; // Stroke pen for unmatched regular pads.

            // --- Initialize Brushes and Pens for CAD Pads ---
            IBrush? cadFillDefault = null; // Default fill color for CAD pads.
            IBrush? cadFillSelected = null; // Fill color for selected CAD pads.
            IBrush? cadFillOverlap = null; // Fill color for overlap-highlighted CAD pads.
            Pen? cadPen = null; // Base stroke pen for CAD pads.
            Pen? cadPenSelected = null; // Stroke pen for selected CAD pads.
            Pen? cadPenOverlap = null; // Stroke pen for overlap-highlighted CAD pads.

            if (showRegular)
            {
                var baseColor = RegularLineColor;
                var selectedColor = RegularSelectedColor ?? GetResourceColor("ColorCanvasRegularSelected", baseColor);
                var freeformColor = GetResourceColor("ColorCanvasRegularFreeform", baseColor);
                var unmatchedColor = GetResourceColor("ColorCanvasRegularUnmatched", baseColor);

                var regFillAlpha = GetResourceAlpha("CanvasRegularFillAlpha", 0.0);
                var regFillSelectedAlpha = RegularSelectedFillOpacity.HasValue
                    ? (byte)Math.Clamp((int)Math.Round(RegularSelectedFillOpacity.Value * 255.0), 0, 255)
                    : GetResourceAlpha("CanvasRegularSelectedFillAlpha", 0.0);
                var regFillFreeformAlpha = GetResourceAlpha("CanvasRegularFreeformFillAlpha", 0.0);
                var regStrokeAlpha = GetResourceAlpha("CanvasRegularStrokeAlpha", 0.0);
                var highlightAlpha = GetResourceAlpha("CanvasHighlightStrokeAlpha", regStrokeAlpha);

                // Create semi-transparent fill brushes for regular pads.
                var regBaseFillAlpha = ApplyOpacity(regFillAlpha, regFillOpacity);
                var regSelectedFillAlpha = ApplyOpacity(regFillSelectedAlpha, regFillOpacity);
                var regFreeformFillAlpha = ApplyOpacity(regFillFreeformAlpha, regFillOpacity);
                regFillBase = regBaseFillAlpha == 0
                    ? Brushes.Transparent
                    : GetBrush(Color.FromArgb(regBaseFillAlpha, baseColor.R, baseColor.G, baseColor.B));
                regFillSelected = regSelectedFillAlpha == 0
                    ? Brushes.Transparent
                    : GetBrush(Color.FromArgb(regSelectedFillAlpha, selectedColor.R, selectedColor.G, selectedColor.B));
                regFillFreeform = regFreeformFillAlpha == 0
                    ? Brushes.Transparent
                    : GetBrush(Color.FromArgb(regFreeformFillAlpha, freeformColor.R, freeformColor.G, freeformColor.B));

                // Create pens for regular pads, including different styles for various states.
                var baseStrokeAlpha = ApplyOpacity(regStrokeAlpha, regOpacity);
                var stateStrokeAlpha = ApplyOpacity(highlightAlpha, regOpacity);
                var highlightedRegularWidth = Math.Max(0.05, RegularLineWidth + highlightStrokeWidthAdjust);
                regPenBase = new Pen(GetBrush(Color.FromArgb(baseStrokeAlpha, baseColor.R, baseColor.G, baseColor.B)), RegularLineWidth);
                regPenSelected = new Pen(GetBrush(Color.FromArgb(stateStrokeAlpha, selectedColor.R, selectedColor.G, selectedColor.B)), selectedOutlineWidth);
                // Freeform remains color-coded, but does not get selection-style thick highlight.
                regPenFreeform = new Pen(GetBrush(Color.FromArgb(baseStrokeAlpha, freeformColor.R, freeformColor.G, freeformColor.B)), RegularLineWidth);
                regPenUnmatched = new Pen(GetBrush(Color.FromArgb(stateStrokeAlpha, unmatchedColor.R, unmatchedColor.G, unmatchedColor.B)), highlightedRegularWidth);
            }

            if (showCad)
            {
                if (!lowDetailMode && ColorCadByArea)
                {
                    EnsureAreaColorCache(); // Ensure area-based color cache for CAD pads is built.
                }

                // Create semi-transparent fill brushes for CAD pads.
                var cadFillDefaultColor = GetResourceColor("ColorCanvasCadFillDefault", Colors.Transparent);
                var cadFillSelectedColor = GetResourceColor("ColorCanvasCadFillSelected", Colors.Transparent);
                var cadFillOverlapColor = GetResourceColor("ColorCanvasCadOverlapFill", Colors.Transparent);
                var cadDefaultFillAlpha = ApplyOpacity(cadFillDefaultColor.A, cadFillOpacity);
                var cadSelectedFillAlpha = ApplyOpacity(cadFillSelectedColor.A, cadFillOpacity);
                var cadOverlapFillAlpha = ApplyOpacity(cadFillOverlapColor.A, cadFillOpacity);
                cadFillDefault = cadDefaultFillAlpha == 0
                    ? Brushes.Transparent
                    : GetBrush(Color.FromArgb(cadDefaultFillAlpha, cadFillDefaultColor.R, cadFillDefaultColor.G, cadFillDefaultColor.B));
                cadFillSelected = cadSelectedFillAlpha == 0
                    ? Brushes.Transparent
                    : GetBrush(Color.FromArgb(cadSelectedFillAlpha, cadFillSelectedColor.R, cadFillSelectedColor.G, cadFillSelectedColor.B));
                cadFillOverlap = cadOverlapFillAlpha == 0
                    ? Brushes.Transparent
                    : GetBrush(Color.FromArgb(cadOverlapFillAlpha, cadFillOverlapColor.R, cadFillOverlapColor.G, cadFillOverlapColor.B));

                var cadStrokeAlpha = GetResourceAlpha("CanvasCadStrokeAlpha", 0.0);
                var highlightAlpha = GetResourceAlpha("CanvasHighlightStrokeAlpha", cadStrokeAlpha);
                var cadSelectedColor = GetResourceColor("ColorCanvasCadSelected", CadLineColor);
                var cadOverlapColor = GetResourceColor("ColorCanvasCadOverlapStroke", cadSelectedColor);
                var overlapWidthAdd = GetResourceDouble("CanvasCadOverlapStrokeWidthAdd", selectedWidthAdd);
                var overlapWidth = Math.Max(CadLineWidth + 0.05, CadLineWidth + overlapWidthAdd + highlightStrokeWidthAdjust);

                // Create pens for CAD pads, including a thicker pen for selected state.
                var baseStrokeAlpha = ApplyOpacity(cadStrokeAlpha, cadOpacity);
                var selectedStrokeAlpha = ApplyOpacity(highlightAlpha, cadOpacity);
                cadPen = new Pen(GetBrush(Color.FromArgb(baseStrokeAlpha, CadLineColor.R, CadLineColor.G, CadLineColor.B)), CadLineWidth);
                cadPenSelected = new Pen(GetBrush(Color.FromArgb(selectedStrokeAlpha, cadSelectedColor.R, cadSelectedColor.G, cadSelectedColor.B)), selectedOutlineWidth);
                cadPenOverlap = new Pen(GetBrush(Color.FromArgb(selectedStrokeAlpha, cadOverlapColor.R, cadOverlapColor.G, cadOverlapColor.B)), overlapWidth);
            }

            var visibleBuildStarted = Stopwatch.GetTimestamp();
            BuildVisibleDrawLists(worldViewport, showRegular, showCad, lowDetailMode, lowDetailZoomThreshold);
            visibleBuildElapsedMs = ElapsedMilliseconds(visibleBuildStarted);

            // --- Rendering Order ---
            // 1) Unselected pads (regular -> CAD), 2) selected pads (regular -> CAD).
            // This keeps selected outlines above all unselected geometry.
            var geometryStarted = Stopwatch.GetTimestamp();

            // 1. Draw Regular Pads (unselected)
            if (showRegular)
            {
                foreach (var pad in _visibleRegularUnselected)
                {
                    DrawRegularPad(context, pad, isSelected: false, regFillBase!, regFillSelected!, regFillFreeform!, regPenBase!, regPenSelected!, regPenFreeform!, regPenUnmatched!);
                }
            }

            // 2. Draw CAD Pads (unselected)
            if (showCad)
            {
                foreach (var pad in _visibleCadUnselected)
                {
                    DrawCadPad(context, pad, isSelected: false, cadFillDefault!, cadFillSelected!, cadFillOverlap!, cadPen!, cadPenSelected!, cadPenOverlap!, lowDetailMode);
                }
            }

            // 3. Draw Regular Pads (selected)
            if (showRegular)
            {
                foreach (var pad in _visibleRegularSelected)
                {
                    DrawRegularPad(context, pad, isSelected: true, regFillBase!, regFillSelected!, regFillFreeform!, regPenBase!, regPenSelected!, regPenFreeform!, regPenUnmatched!);
                }
            }

            // 4. Draw CAD Pads (selected)
            if (showCad)
            {
                foreach (var pad in _visibleCadSelected)
                {
                    DrawCadPad(context, pad, isSelected: true, cadFillDefault!, cadFillSelected!, cadFillOverlap!, cadPen!, cadPenSelected!, cadPenOverlap!, lowDetailMode);
                }
            }
            geometryDrawElapsedMs = ElapsedMilliseconds(geometryStarted);

            if (!_isBoxSelecting && !secondaryVisualsDeferred)
            {
                var overlayStarted = Stopwatch.GetTimestamp();
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
            var labelStarted = Stopwatch.GetTimestamp();
            // Priority: Match labels stay first; notch ratio labels are placed around them.
            DrawMatchAllocationLabels(context, occupiedRatioLabelRects);
            DrawNotchToRegularLabels(context, occupiedRatioLabelRects);
            DrawSimulationRegularLabels(context);
            labelElapsedMs += ElapsedMilliseconds(labelStarted);
        }
        else if (!_isBoxSelecting && !secondaryVisualsDeferred)
        {
            var simulationLabelStarted = Stopwatch.GetTimestamp();
            DrawSimulationRegularLabels(context);
            labelElapsedMs += ElapsedMilliseconds(simulationLabelStarted);
        }
        var overlayAfterTransformStarted = Stopwatch.GetTimestamp();
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
        var labelTailStarted = Stopwatch.GetTimestamp();
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
