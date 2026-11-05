using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Relative frame-cost benchmarks for <see cref="PadCanvas.Render"/>. Every scenario renders the same synthetic
/// panel into an off-screen Skia bitmap and is reported against the static regular-only frame.
/// See docs/audits/perf-review.md. No test asserts an absolute time.
/// </summary>
[Collection("HeadlessUiSerial")]
[Trait("Category", "Perf")]
public sealed class PadCanvasRenderBenchmarkTests
{
    private const int Rows = 64;
    private const int Cols = 96;
    private const double CellSize = 4.0;
    private const int ViewWidth = 1280;
    private const int ViewHeight = 800;
    private const int Frames = 20;

    private readonly ITestOutputHelper _output;

    public PadCanvasRenderBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public async Task RenderFrame_RelativeCostByScenario()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var regularPads = CreateRegularPads(freeformEveryNth: 0);
        var freeformPads = CreateRegularPads(freeformEveryNth: 4);
        var cadPads = CreateCadPads();

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            ShowCad = false,
            ShowAxisLabels = false,
            HighlightFreeform = false,
            RegularPads = regularPads,
            CadPads = cadPads,
        };
        var window = new Window { Width = ViewWidth, Height = ViewHeight, Content = canvas };

        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            using var bitmap = new RenderTargetBitmap(new PixelSize(ViewWidth, ViewHeight));
            SetView(canvas);

            var samples = new List<BenchmarkSample>();
            samples.Add(MeasureFrames("A regular only, static view", canvas, bitmap, pan: false));

            canvas.ShowAxisLabels = true;
            samples.Add(MeasureFrames("B + axis labels", canvas, bitmap, pan: false));
            canvas.ShowAxisLabels = false;

            canvas.RegularPads = freeformPads;
            canvas.HighlightFreeform = false;
            samples.Add(MeasureFrames("C0 25% freeform pads, hatch off", canvas, bitmap, pan: false));
            canvas.HighlightFreeform = true;
            samples.Add(MeasureFrames("C1 25% freeform pads, hatch on", canvas, bitmap, pan: false));
            canvas.HighlightFreeform = false;
            canvas.RegularPads = regularPads;

            samples.Add(MeasureFrames("D regular only, pan 1px every frame", canvas, bitmap, pan: true));

            canvas.ShowCad = true;
            samples.Add(MeasureFrames("E regular + CAD, static view", canvas, bitmap, pan: false));
            samples.Add(MeasureFrames("F regular + CAD, pan 1px every frame", canvas, bitmap, pan: true));

            var perf = canvas.GetVisibleDrawPerfSnapshot();
            Assert.True(perf.RegularCandidates > 0);
            Assert.True(perf.CadCandidates > 0);

            _output.WriteLine(RelativeBenchmark.FormatTable(
                $"PadCanvas.Render, {Rows * Cols} regular + {Rows * Cols} CAD pads, {ViewWidth}x{ViewHeight} (one op = one frame)",
                samples,
                samples[0]));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FirstFrameAfterGridChange_ComparedWithSteadyFrame()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var gridA = CreateRegularPads(freeformEveryNth: 0);
        var gridB = CreateRegularPads(freeformEveryNth: 0);
        var canvas = new PadCanvas { ShowRegular = true, ShowAxisLabels = false, RegularPads = gridA };
        var window = new Window { Width = ViewWidth, Height = ViewHeight, Content = canvas };

        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            using var bitmap = new RenderTargetBitmap(new PixelSize(ViewWidth, ViewHeight));
            SetView(canvas);

            var steady = MeasureFrames("steady frame", canvas, bitmap, pan: false);
            var toggle = false;
            var rebuilt = RelativeBenchmark.Measure(
                "first frame after RegularPads change (index + visible lists rebuilt)",
                () =>
                {
                    toggle = !toggle;
                    canvas.RegularPads = toggle ? gridB : gridA;
                    RenderOnce(canvas, bitmap);
                },
                Frames);

            Assert.True(canvas.GetVisibleDrawPerfSnapshot().RegularCandidates > 0);
            _output.WriteLine(RelativeBenchmark.FormatTable(
                $"Grid change to first frame, {Rows * Cols} regular pads",
                new[] { steady, rebuilt },
                steady));
        }
        finally
        {
            window.Close();
        }
    }

    private static BenchmarkSample MeasureFrames(string name, PadCanvas canvas, RenderTargetBitmap bitmap, bool pan)
    {
        var direction = 1.0;
        return RelativeBenchmark.Measure(
            name,
            () =>
            {
                if (pan)
                {
                    PadCanvasPrivateAccess.Pan(canvas) += new Vector(direction, 0);
                    direction = -direction;
                }

                RenderOnce(canvas, bitmap);
            },
            Frames);
    }

    private static void RenderOnce(PadCanvas canvas, RenderTargetBitmap bitmap)
    {
        using var context = bitmap.CreateDrawingContext();
        canvas.Render(context);
    }

    private static void SetView(PadCanvas canvas)
    {
        // The grid spans 384 x 256 world units. Zoom 2.8 keeps it on screen with room for the axis labels.
        PadCanvasPrivateAccess.Zoom(canvas) = 2.8;
        PadCanvasPrivateAccess.Pan(canvas) = new Vector(80, 780);
    }

    private static RegularPad[] CreateRegularPads(int freeformEveryNth)
    {
        var pads = new RegularPad[Rows * Cols];
        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                var index = (r * Cols) + c;
                var pad = TestGeometryFactory.CreateRegularPad(
                    r,
                    c,
                    index,
                    c * CellSize,
                    r * CellSize,
                    (c + 1) * CellSize,
                    (r + 1) * CellSize,
                    icIndex: c / 32);
                if (freeformEveryNth > 0 && (r + c) % freeformEveryNth == 0)
                {
                    pad.Freeform = FreeformType.XYWay;
                }

                pads[index] = pad;
            }
        }

        return pads;
    }

    private static CadPad[] CreateCadPads()
    {
        var pads = new CadPad[Rows * Cols];
        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Cols; c++)
            {
                var index = (r * Cols) + c;
                var cx = (c + 0.5) * CellSize;
                var cy = (r + 0.5) * CellSize;
                var radius = CellSize * 0.4;
                var points = new Point2[8];
                for (var i = 0; i < points.Length; i++)
                {
                    var angle = i * Math.PI / 4.0;
                    points[i] = new Point2(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
                }

                pads[index] = new CadPad(index, $"P{index}", "PADS", new Polygon2(points));
            }
        }

        return pads;
    }
}
