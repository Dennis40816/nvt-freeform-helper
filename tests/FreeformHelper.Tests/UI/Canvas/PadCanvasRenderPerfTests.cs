using System.Reflection;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasRenderPerfTests
{
    [AvaloniaFact]
    public void RecordRenderPerf_UpdatesSnapshot_AndIncrementsRevision()
    {
        var canvas = new PadCanvas();
        var viewFrame = canvas.GetViewFrameSnapshot();

        InvokeRecordRenderPerf(
            canvas,
            totalElapsedMs: 12,
            backgroundElapsedMs: 1,
            visibleBuildElapsedMs: 3,
            geometryDrawElapsedMs: 4,
            overlayElapsedMs: 2,
            labelElapsedMs: 2,
            viewFrameRevision: viewFrame.Revision,
            showRegular: true,
            showCad: true,
            lowDetailMode: false,
            secondaryVisualsDeferred: false);
        var firstSnapshot = canvas.GetRenderPerfSnapshot();

        InvokeRecordRenderPerf(
            canvas,
            totalElapsedMs: 20,
            backgroundElapsedMs: 2,
            visibleBuildElapsedMs: 5,
            geometryDrawElapsedMs: 7,
            overlayElapsedMs: 3,
            labelElapsedMs: 3,
            viewFrameRevision: viewFrame.Revision,
            showRegular: false,
            showCad: true,
            lowDetailMode: true,
            secondaryVisualsDeferred: true);
        var secondSnapshot = canvas.GetRenderPerfSnapshot();

        Assert.Equal(1, firstSnapshot.Revision);
        Assert.Equal(2, secondSnapshot.Revision);
        Assert.Equal(20, secondSnapshot.TotalElapsedMs);
        Assert.Equal(2, secondSnapshot.BackgroundElapsedMs);
        Assert.Equal(5, secondSnapshot.VisibleBuildElapsedMs);
        Assert.Equal(7, secondSnapshot.GeometryDrawElapsedMs);
        Assert.Equal(3, secondSnapshot.OverlayElapsedMs);
        Assert.Equal(3, secondSnapshot.LabelElapsedMs);
        Assert.Equal(viewFrame.Revision, secondSnapshot.ViewFrameRevision);
        Assert.False(secondSnapshot.ShowRegular);
        Assert.True(secondSnapshot.ShowCad);
        Assert.True(secondSnapshot.LowDetailMode);
        Assert.True(secondSnapshot.SecondaryVisualsDeferred);
    }

    [AvaloniaFact]
    public void RecordRenderPerf_CapturesCurrentVisibleDrawRevision()
    {
        var canvas = new PadCanvas
        {
            CadPads = CreateCadPads(),
            RegularPads = CreateRegularPads(),
        };

        InvokeBuildVisibleDrawLists(canvas, new Rect2(-5, -5, 30, 30));
        var visibleSnapshot = canvas.GetVisibleDrawPerfSnapshot();

        InvokeRecordRenderPerf(
            canvas,
            totalElapsedMs: 15,
            backgroundElapsedMs: 1,
            visibleBuildElapsedMs: 4,
            geometryDrawElapsedMs: 5,
            overlayElapsedMs: 2,
            labelElapsedMs: 3,
            viewFrameRevision: canvas.GetViewFrameSnapshot().Revision,
            showRegular: true,
            showCad: true,
            lowDetailMode: false,
            secondaryVisualsDeferred: false);
        var renderSnapshot = canvas.GetRenderPerfSnapshot();

        Assert.True(visibleSnapshot.Revision > 0);
        Assert.Equal(visibleSnapshot.Revision, renderSnapshot.VisibleDrawRevision);
    }

    private static void InvokeRecordRenderPerf(
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
        bool secondaryVisualsDeferred)
    {
        var method = typeof(PadCanvas).GetMethod("RecordRenderPerf", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(
            canvas,
            [
                totalElapsedMs,
                backgroundElapsedMs,
                visibleBuildElapsedMs,
                geometryDrawElapsedMs,
                overlayElapsedMs,
                labelElapsedMs,
                viewFrameRevision,
                showRegular,
                showCad,
                lowDetailMode,
                secondaryVisualsDeferred,
            ]);
    }

    private static void InvokeBuildVisibleDrawLists(PadCanvas canvas, Rect2 viewport)
    {
        var method = typeof(PadCanvas).GetMethod("BuildVisibleDrawLists", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(canvas, [viewport, true, true, false, 0.3]);
    }

    private static IReadOnlyList<CadPad> CreateCadPads()
    {
        return
        [
            new CadPad(1, "CAD-1", "L1", CreateRectPolygon(0, 0, 10, 10)),
            new CadPad(2, "CAD-2", "L1", CreateRectPolygon(12, 0, 22, 10)),
        ];
    }

    private static IReadOnlyList<RegularPad> CreateRegularPads()
    {
        return
        [
            new RegularPad(0, 0, 0, CreateRectPolygon(0, 0, 10, 10)),
            new RegularPad(0, 1, 1, CreateRectPolygon(10, 0, 20, 10)),
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
}
