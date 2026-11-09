using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>Typed access to the private state and seams of <see cref="PadCanvas"/>.</summary>
internal static class PadCanvasPrivateAccess
{
    // View state.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_isViewRefreshQueued")]
    public static extern ref bool IsViewRefreshQueued(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_zoom")]
    public static extern ref double Zoom(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pan")]
    public static extern ref Vector Pan(PadCanvas canvas);

    // Gesture state.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_isMiddlePanning")]
    public static extern ref bool IsMiddlePanning(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_isSpacePanning")]
    public static extern ref bool IsSpacePanning(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_isSpacePressed")]
    public static extern ref bool IsSpacePressed(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_isLeftPointerDown")]
    public static extern ref bool IsLeftPointerDown(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_isBoxSelecting")]
    public static extern ref bool IsBoxSelecting(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lastPanRedrawTicks")]
    public static extern ref long LastPanRedrawTicks(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_lastBoxSelectionRedrawTicks")]
    public static extern ref long LastBoxSelectionRedrawTicks(PadCanvas canvas);

    // Selection state.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_selectedCadIds")]
    public static extern ref HashSet<int> SelectedCadIds(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_selectedRegIdx")]
    public static extern ref HashSet<int> SelectedRegIdx(PadCanvas canvas);

    // Hover-debug state.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hoverDebugVisible")]
    public static extern ref bool HoverDebugVisible(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hoverDebugRegularOnlyMode")]
    public static extern ref bool HoverDebugRegularOnlyMode(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_hoverDebugHit")]
    public static extern ref PadCanvas.HitResult HoverDebugHit(PadCanvas canvas);

    // Caches.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cadGeometryCache")]
    public static extern ref Dictionary<int, Geometry>? CadGeometryCache(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cadAreaColorCache")]
    public static extern ref Dictionary<int, Color>? CadAreaColorCache(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cadAreaBrushCache")]
    public static extern ref Dictionary<int, ISolidColorBrush>? CadAreaBrushCache(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cadIndex")]
    public static extern ref PadCanvas.CadSpatialIndex? CadIndex(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_regularIndex")]
    public static extern ref PadCanvas.RegularSpatialIndex? RegularIndex(PadCanvas canvas);

    // Cache and index builders.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EnsureCadGeometryCache")]
    public static extern void EnsureCadGeometryCache(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EnsureAreaColorCache")]
    public static extern void EnsureAreaColorCache(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EnsureCadSpatialIndex")]
    public static extern void EnsureCadSpatialIndex(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "EnsureRegularSpatialIndex")]
    public static extern void EnsureRegularSpatialIndex(PadCanvas canvas);

    // Rendering seams.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BuildVisibleDrawLists")]
    public static extern void BuildVisibleDrawLists(
        PadCanvas canvas,
        Rect2 worldViewport,
        bool showRegular,
        bool showCad,
        bool lowDetailMode,
        double lowDetailZoomThreshold);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RecordRenderPerf")]
    public static extern void RecordRenderPerf(
        PadCanvas canvas,
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
        bool secondaryVisualsDeferred);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ShouldUsePreciseCadGeometry")]
    public static extern bool ShouldUsePreciseCadGeometry(PadCanvas canvas, int cadPadId, bool isSelected, bool isOverlapHighlighted, bool lowDetailMode);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ShouldSuppressCadFillForNotchPreview")]
    public static extern bool ShouldSuppressCadFillForNotchPreview(PadCanvas canvas, int cadPadId);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ShouldDrawFreeformHatch")]
    public static extern bool ShouldDrawFreeformHatch(PadCanvas canvas, RegularPad pad);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ShouldSuppressFreeformAccentBorderForNotchPreview")]
    public static extern bool ShouldSuppressFreeformAccentBorderForNotchPreview(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ShouldSimplifyAggregateNotchPreviewVisuals")]
    public static extern bool ShouldSimplifyAggregateNotchPreviewVisuals(PadCanvas canvas);

    // Hover-debug seams.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "HitTestForHoverDebug")]
    public static extern PadCanvas.HitResult HitTestForHoverDebug(PadCanvas canvas, Point screenPoint, bool regularOnly);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "BuildHoverDebugSnapshot")]
    public static extern PadCanvas.HoverDebugSnapshot? BuildHoverDebugSnapshot(PadCanvas canvas);

    // Selection and view seams.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SelectPadsInWorldRect")]
    public static extern void SelectPadsInWorldRect(PadCanvas canvas, Rect2 worldRect, bool additive, bool regularOnly);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RequestViewRefresh")]
    public static extern void RequestViewRefresh(PadCanvas canvas, bool raiseViewChanged);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RequestVisualRefresh")]
    public static extern void RequestVisualRefresh(PadCanvas canvas, bool raiseViewChanged);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InvalidateViewFrameSnapshot")]
    public static extern void InvalidateViewFrameSnapshot(PadCanvas canvas);

    // Best-match projections.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ResolveBestRegularByCad")]
    public static extern RegularPad? ResolveBestRegularByCad(PadCanvas canvas, int cadPadId);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ResolveBestCadIdByRegular")]
    public static extern int? ResolveBestCadIdByRegular(PadCanvas canvas, int regularPadId);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryBuildSimulationRegularFillBrush")]
    public static extern IBrush? TryBuildSimulationRegularFillBrush(PadCanvas canvas, RegularPad pad);

    // Pointer-press seams.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_axisLabelHits")]
    public static extern ref List<PadCanvas.AxisLabelHit> AxisLabelHits(PadCanvas canvas);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pendingSingleHit")]
    public static extern ref PadCanvas.HitResult? PendingSingleHit(PadCanvas canvas);
}
