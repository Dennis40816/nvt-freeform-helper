using System.Reflection;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasCacheInvalidationTests
{
    [AvaloniaFact]
    public void RegularPadsChanged_KeepsCadGeometryCache()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
        };

        InvokePrivate(canvas, "EnsureCadGeometryCache");
        InvokePrivate(canvas, "EnsureCadSpatialIndex");
        var cadGeometryCacheBefore = GetPrivateField(canvas, "_cadGeometryCache");
        var cadSpatialIndexBefore = GetPrivateField(canvas, "_cadIndex");
        Assert.NotNull(cadGeometryCacheBefore);
        Assert.NotNull(cadSpatialIndexBefore);

        canvas.RegularPads = CreateRegularPads(offsetX: 100);

        var cadGeometryCacheAfter = GetPrivateField(canvas, "_cadGeometryCache");
        var cadSpatialIndexAfter = GetPrivateField(canvas, "_cadIndex");
        Assert.Same(cadGeometryCacheBefore, cadGeometryCacheAfter);
        Assert.Same(cadSpatialIndexBefore, cadSpatialIndexAfter);
    }

    [AvaloniaFact]
    public void CadPadsChanged_KeepsRegularSpatialIndex()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
        };

        InvokePrivate(canvas, "EnsureRegularSpatialIndex");
        var regularIndexBefore = GetPrivateField(canvas, "_regularIndex");
        Assert.NotNull(regularIndexBefore);

        canvas.CadPads = CreateCadPads(offsetX: 200);

        var regularIndexAfter = GetPrivateField(canvas, "_regularIndex");
        Assert.Same(regularIndexBefore, regularIndexAfter);
    }

    [AvaloniaFact]
    public void CadPadsChanged_ResetsCadCaches_AndPreservesRegularIndex()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
            ColorCadByArea = true,
        };

        InvokePrivate(canvas, "EnsureCadGeometryCache");
        InvokePrivate(canvas, "EnsureAreaColorCache");
        InvokePrivate(canvas, "EnsureCadSpatialIndex");
        InvokePrivate(canvas, "EnsureRegularSpatialIndex");

        var regularIndexBefore = GetPrivateField(canvas, "_regularIndex");
        var cadIndexBefore = GetPrivateField(canvas, "_cadIndex");
        Assert.NotNull(regularIndexBefore);
        Assert.NotNull(cadIndexBefore);
        Assert.NotNull(GetPrivateField(canvas, "_cadGeometryCache"));
        Assert.NotNull(GetPrivateField(canvas, "_cadAreaColorCache"));
        Assert.NotNull(GetPrivateField(canvas, "_cadAreaBrushCache"));

        canvas.CadPads = CreateCadPads(offsetX: 300);

        Assert.Null(GetPrivateField(canvas, "_cadGeometryCache"));
        Assert.Null(GetPrivateField(canvas, "_cadAreaColorCache"));
        Assert.Null(GetPrivateField(canvas, "_cadAreaBrushCache"));
        Assert.Null(GetPrivateField(canvas, "_cadIndex"));
        Assert.Same(regularIndexBefore, GetPrivateField(canvas, "_regularIndex"));
    }

    [AvaloniaFact]
    public void RegularPadsChanged_ResetsRegularIndex_AndPreservesCadGeometryCache()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
            ColorCadByArea = true,
        };

        InvokePrivate(canvas, "EnsureCadGeometryCache");
        InvokePrivate(canvas, "EnsureAreaColorCache");
        InvokePrivate(canvas, "EnsureRegularSpatialIndex");

        var cadGeometryCacheBefore = GetPrivateField(canvas, "_cadGeometryCache");
        var cadAreaColorCacheBefore = GetPrivateField(canvas, "_cadAreaColorCache");
        Assert.NotNull(cadGeometryCacheBefore);
        Assert.NotNull(cadAreaColorCacheBefore);
        Assert.NotNull(GetPrivateField(canvas, "_regularIndex"));

        canvas.RegularPads = CreateRegularPads(offsetX: 100);

        Assert.Null(GetPrivateField(canvas, "_regularIndex"));
        Assert.Same(cadGeometryCacheBefore, GetPrivateField(canvas, "_cadGeometryCache"));
        Assert.Same(cadAreaColorCacheBefore, GetPrivateField(canvas, "_cadAreaColorCache"));
    }

    [AvaloniaFact]
    public void BuildVisibleDrawLists_ReusesCache_WhenStateUnchanged()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
        };

        var viewport = new Rect2(-5, -5, 30, 30);
        InvokeBuildVisibleDrawLists(canvas, viewport, showRegular: true, showCad: true, lowDetailMode: false, lowDetailZoomThreshold: 0.3);
        var firstSnapshot = canvas.GetVisibleDrawPerfSnapshot();

        InvokeBuildVisibleDrawLists(canvas, viewport, showRegular: true, showCad: true, lowDetailMode: false, lowDetailZoomThreshold: 0.3);
        var secondSnapshot = canvas.GetVisibleDrawPerfSnapshot();

        Assert.False(firstSnapshot.QueryCacheHit);
        Assert.False(firstSnapshot.DrawListCacheHit);
        Assert.True(secondSnapshot.QueryCacheHit);
        Assert.True(secondSnapshot.DrawListCacheHit);
        Assert.Equal(firstSnapshot.VisibleCadSelected, secondSnapshot.VisibleCadSelected);
        Assert.Equal(firstSnapshot.VisibleCadUnselected, secondSnapshot.VisibleCadUnselected);
        Assert.Equal(firstSnapshot.VisibleRegularSelected, secondSnapshot.VisibleRegularSelected);
        Assert.Equal(firstSnapshot.VisibleRegularUnselected, secondSnapshot.VisibleRegularUnselected);
    }

    [AvaloniaFact]
    public void BuildVisibleDrawLists_Rebuilds_WhenSelectionChanges()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
        };

        var viewport = new Rect2(-5, -5, 30, 30);
        InvokeBuildVisibleDrawLists(canvas, viewport, showRegular: true, showCad: true, lowDetailMode: false, lowDetailZoomThreshold: 0.3);
        Assert.False(canvas.GetVisibleDrawPerfSnapshot().DrawListCacheHit);

        canvas.SetSelection([1], []);
        InvokeBuildVisibleDrawLists(canvas, viewport, showRegular: true, showCad: true, lowDetailMode: false, lowDetailZoomThreshold: 0.3);
        var snapshotAfterSelection = canvas.GetVisibleDrawPerfSnapshot();

        Assert.True(snapshotAfterSelection.QueryCacheHit);
        Assert.False(snapshotAfterSelection.DrawListCacheHit);
        Assert.Equal(1, snapshotAfterSelection.VisibleCadSelected);
    }

    [AvaloniaFact]
    public void BuildVisibleDrawLists_Rebuilds_WhenHighlightedCadPadsChange()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
        };

        var viewport = new Rect2(-5, -5, 30, 30);
        InvokeBuildVisibleDrawLists(canvas, viewport, showRegular: true, showCad: true, lowDetailMode: true, lowDetailZoomThreshold: 0.3);
        Assert.False(canvas.GetVisibleDrawPerfSnapshot().DrawListCacheHit);

        canvas.HighlightedCadPadIds = [1];
        InvokeBuildVisibleDrawLists(canvas, viewport, showRegular: true, showCad: true, lowDetailMode: true, lowDetailZoomThreshold: 0.3);
        var snapshotAfterHighlight = canvas.GetVisibleDrawPerfSnapshot();

        Assert.True(snapshotAfterHighlight.QueryCacheHit);
        Assert.False(snapshotAfterHighlight.DrawListCacheHit);
        Assert.True(snapshotAfterHighlight.VisibleCadUnselected >= 1);
    }

    [AvaloniaFact]
    public void BuildVisibleDrawLists_RebuildsQuery_WhenViewportChanges()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
        };

        InvokeBuildVisibleDrawLists(canvas, new Rect2(-5, -5, 30, 30), showRegular: true, showCad: true, lowDetailMode: false, lowDetailZoomThreshold: 0.3);
        var firstSnapshot = canvas.GetVisibleDrawPerfSnapshot();

        InvokeBuildVisibleDrawLists(canvas, new Rect2(100, 100, 140, 140), showRegular: true, showCad: true, lowDetailMode: false, lowDetailZoomThreshold: 0.3);
        var secondSnapshot = canvas.GetVisibleDrawPerfSnapshot();

        Assert.False(firstSnapshot.QueryCacheHit);
        Assert.False(secondSnapshot.QueryCacheHit);
        Assert.False(secondSnapshot.DrawListCacheHit);
    }

    [AvaloniaFact]
    public void BuildVisibleDrawLists_ReportsCadDecimation_WhenLowDetailDropsCandidates()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateManyCadPads(count: 400, columns: 20),
            RegularPads = CreateRegularPads(offsetX: 0),
        };

        SetPrivateField(canvas, "_zoom", 0.1d);
        InvokeBuildVisibleDrawLists(canvas, new Rect2(-5, -5, 120, 120), showRegular: false, showCad: true, lowDetailMode: true, lowDetailZoomThreshold: 0.3);
        var snapshot = canvas.GetVisibleDrawPerfSnapshot();

        Assert.True(snapshot.CadCandidates > snapshot.VisibleCadUnselected);
        Assert.True(snapshot.CadDecimationStep > 1 || snapshot.CadDecimatedCount > 0);
        Assert.True(snapshot.CadDecimatedCount > 0);
    }

    [AvaloniaFact]
    public void NotchPreviewCad_UsesPreciseGeometry_InLowDetailMode()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
            NotchCanvasPreviewItems =
            [
                new NotchCanvasPreviewItem(
                    CadPadId: 1,
                    ToRegularRatio: 1.0,
                    ToFullRatio: 1.0,
                    IsToFullEnabled: true,
                    ToFullPolygons: Array.Empty<Polygon2>(),
                    ToFullSeedPolygons: Array.Empty<Polygon2>(),
                    ToFullCandidatePolygons: Array.Empty<Polygon2>(),
                    ToFullFinalOutlinePolygons: Array.Empty<Polygon2>())
            ],
            ShowNotchToFullSeedOverlay = true,
        };

        var method = typeof(PadCanvas).GetMethod("ShouldUsePreciseCadGeometry", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(canvas, [1, false, false, true]);

        Assert.Equal(true, result);
    }

    [AvaloniaFact]
    public void NotchPreviewCad_SuppressesAreaFill_WhenStageOverlayVisible()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
            ColorCadByArea = true,
            NotchCanvasPreviewItems =
            [
                new NotchCanvasPreviewItem(
                    CadPadId: 1,
                    ToRegularRatio: 1.0,
                    ToFullRatio: 1.0,
                    IsToFullEnabled: true,
                    ToFullPolygons: Array.Empty<Polygon2>(),
                    ToFullSeedPolygons: Array.Empty<Polygon2>(),
                    ToFullCandidatePolygons: Array.Empty<Polygon2>(),
                    ToFullFinalOutlinePolygons: Array.Empty<Polygon2>())
            ],
            ShowNotchToFullSeedOverlay = true,
        };

        var method = typeof(PadCanvas).GetMethod("ShouldSuppressCadFillForNotchPreview", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(canvas, [1]);

        Assert.Equal(true, result);
    }

    [AvaloniaFact]
    public void NotchPreview_UsesBaseRegularStyling_WhenStageOverlayVisible()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
            NotchCanvasPreviewItems =
            [
                new NotchCanvasPreviewItem(
                    CadPadId: 1,
                    ToRegularRatio: 1.0,
                    ToFullRatio: 1.0,
                    IsToFullEnabled: true,
                    ToFullPolygons: Array.Empty<Polygon2>(),
                    ToFullSeedPolygons: Array.Empty<Polygon2>(),
                    ToFullCandidatePolygons: Array.Empty<Polygon2>(),
                    ToFullFinalOutlinePolygons: Array.Empty<Polygon2>())
            ],
            ShowNotchToFullSeedOverlay = true,
            HighlightFreeform = true,
        };

        var method = typeof(PadCanvas).GetMethod("ShouldUseBaseRegularStylingForNotchPreview", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(canvas, null);

        Assert.Equal(true, result);
    }

    [AvaloniaFact]
    public void NotchPreview_KeepsFreeformHatch_WhenStageOverlayVisible()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads =
            [
                new RegularPad(0, 0, 0, CreateRectPolygon(0, 0, 10, 10))
                {
                    Freeform = FreeformType.XYWay,
                },
            ],
            NotchCanvasPreviewItems =
            [
                new NotchCanvasPreviewItem(
                    CadPadId: 1,
                    ToRegularRatio: 1.0,
                    ToFullRatio: 1.0,
                    IsToFullEnabled: true,
                    ToFullPolygons: Array.Empty<Polygon2>(),
                    ToFullSeedPolygons: Array.Empty<Polygon2>(),
                    ToFullCandidatePolygons: Array.Empty<Polygon2>(),
                    ToFullFinalOutlinePolygons: Array.Empty<Polygon2>())
            ],
            ShowNotchToFullSeedOverlay = true,
            HighlightFreeform = true,
        };

        var method = typeof(PadCanvas).GetMethod("ShouldDrawFreeformHatch", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(canvas, [canvas.RegularPads[0]]);

        Assert.Equal(true, result);
    }

    [AvaloniaFact]
    public void NotchPreview_SuppressesFreeformAccentBorder_WhenStageOverlayVisible()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
            NotchCanvasPreviewItems =
            [
                new NotchCanvasPreviewItem(
                    CadPadId: 1,
                    ToRegularRatio: 1.0,
                    ToFullRatio: 1.0,
                    IsToFullEnabled: true,
                    ToFullPolygons: Array.Empty<Polygon2>(),
                    ToFullSeedPolygons: Array.Empty<Polygon2>(),
                    ToFullCandidatePolygons: Array.Empty<Polygon2>(),
                    ToFullFinalOutlinePolygons: Array.Empty<Polygon2>())
            ],
            ShowNotchToFullSeedOverlay = true,
            HighlightFreeform = true,
        };

        var method = typeof(PadCanvas).GetMethod("ShouldSuppressFreeformAccentBorderForNotchPreview", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(canvas, null);

        Assert.Equal(true, result);
    }

    [AvaloniaFact]
    public void NotchPreview_AggregateMode_SimplifiesSecondaryVisuals()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(offsetX: 0),
            RegularPads = CreateRegularPads(offsetX: 0),
            NotchCanvasPreviewItems =
            [
                new NotchCanvasPreviewItem(
                    CadPadId: 1,
                    ToRegularRatio: 1.0,
                    ToFullRatio: 1.0,
                    IsToFullEnabled: true,
                    ToFullPolygons: Array.Empty<Polygon2>(),
                    ToFullSeedPolygons: Array.Empty<Polygon2>(),
                    ToFullCandidatePolygons: Array.Empty<Polygon2>(),
                    ToFullFinalOutlinePolygons: Array.Empty<Polygon2>()),
                new NotchCanvasPreviewItem(
                    CadPadId: 2,
                    ToRegularRatio: 0.8,
                    ToFullRatio: 1.2,
                    IsToFullEnabled: true,
                    ToFullPolygons: Array.Empty<Polygon2>(),
                    ToFullSeedPolygons: Array.Empty<Polygon2>(),
                    ToFullCandidatePolygons: Array.Empty<Polygon2>(),
                    ToFullFinalOutlinePolygons: Array.Empty<Polygon2>())
            ],
            ShowNotchToFullFinalOverlay = true,
        };

        var method = typeof(PadCanvas).GetMethod("ShouldSimplifyAggregateNotchPreviewVisuals", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(canvas, null);

        Assert.Equal(true, result);
    }

    private static IReadOnlyList<CadPad> CreateCadPads(double offsetX)
    {
        return
        [
            new CadPad(1, "CAD-1", "L1", CreateRectPolygon(offsetX + 0, 0, offsetX + 10, 10)),
            new CadPad(2, "CAD-2", "L1", CreateRectPolygon(offsetX + 12, 0, offsetX + 22, 10)),
        ];
    }

    private static List<CadPad> CreateManyCadPads(int count, int columns)
    {
        var pads = new List<CadPad>(count);
        for (var i = 0; i < count; i++)
        {
            var row = i / columns;
            var col = i % columns;
            var minX = col * 5;
            var minY = row * 5;
            pads.Add(new CadPad(i + 1, $"CAD-{i + 1}", "L1", CreateRectPolygon(minX, minY, minX + 4, minY + 4)));
        }

        return pads;
    }

    private static IReadOnlyList<RegularPad> CreateRegularPads(double offsetX)
    {
        return
        [
            new RegularPad(0, 0, 0, CreateRectPolygon(offsetX + 0, 0, offsetX + 10, 10)),
            new RegularPad(0, 1, 1, CreateRectPolygon(offsetX + 10, 0, offsetX + 20, 10)),
            new RegularPad(1, 0, 2, CreateRectPolygon(offsetX + 0, 10, offsetX + 10, 20)),
            new RegularPad(1, 1, 3, CreateRectPolygon(offsetX + 10, 10, offsetX + 20, 20)),
        ];
    }

    private static Polygon2 CreateRectPolygon(double minX, double minY, double maxX, double maxY)
    {
        return new Polygon2(
            [
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY),
            ]);
    }

    private static void InvokePrivate(PadCanvas canvas, string methodName)
    {
        var method = typeof(PadCanvas).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(canvas, null);
    }

    private static void InvokeBuildVisibleDrawLists(
        PadCanvas canvas,
        Rect2 viewport,
        bool showRegular,
        bool showCad,
        bool lowDetailMode,
        double lowDetailZoomThreshold)
    {
        var method = typeof(PadCanvas).GetMethod("BuildVisibleDrawLists", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(canvas, [viewport, showRegular, showCad, lowDetailMode, lowDetailZoomThreshold]);
    }

    private static object? GetPrivateField(PadCanvas canvas, string fieldName)
    {
        var field = typeof(PadCanvas).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(canvas);
    }

    private static void SetPrivateField(PadCanvas canvas, string fieldName, object value)
    {
        var field = typeof(PadCanvas).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(canvas, value);
    }
}
