using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Relative micro-benchmarks for grid construction and the pad-geometry accessors that the canvas hot paths call.
/// See docs/audits/perf-review.md. Each test checks that the compared variants give the same result, and
/// writes the relative cost table to the test output. No test asserts an absolute time.
/// </summary>
[Trait("Category", "Perf")]
public sealed class RegularGridBuildBenchmarkTests
{
    private const int Rows = 64;
    private const int Cols = 96;

    private readonly ITestOutputHelper _output;

    public RegularGridBuildBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PolygonBounds_RecomputedPerAccess_ComparedWithStructLoopAndCachedBounds()
    {
        var grid = RegularGridBuilder.BuildFromSettings(CreateSettings(WidthAdjustmentScope.ColumnGlobal, HeightAdjustmentScope.RowGlobal));
        var regularPolygons = grid.Pads.Select(pad => pad.Polygon).ToArray();
        var cadPolygons = regularPolygons.Select(polygon => CreateOctagonInside(polygon.Bounds)).ToArray();
        var cachedRegular = regularPolygons.Select(polygon => polygon.Bounds).ToArray();
        var cachedCad = cadPolygons.Select(polygon => polygon.Bounds).ToArray();

        for (var i = 0; i < regularPolygons.Length; i++)
        {
            Assert.Equal(regularPolygons[i].Bounds, ComputeBoundsStructLoop(regularPolygons[i]));
            Assert.Equal(cadPolygons[i].Bounds, ComputeBoundsStructLoop(cadPolygons[i]));
        }

        var sink = 0.0;
        var samples = new List<BenchmarkSample>
        {
            RelativeBenchmark.Measure("regular 4 vertices: Polygon2.Bounds (current)", () => sink += SumCurrent(regularPolygons), 200, warmupIterations: 100),
            RelativeBenchmark.Measure("regular 4 vertices: struct loop", () => sink += SumStructLoop(regularPolygons), 200, warmupIterations: 100),
            RelativeBenchmark.Measure("regular 4 vertices: cached Rect2", () => sink += SumCached(cachedRegular), 200, warmupIterations: 100),
            RelativeBenchmark.Measure("cad 8 vertices: Polygon2.Bounds (current)", () => sink += SumCurrent(cadPolygons), 200, warmupIterations: 100),
            RelativeBenchmark.Measure("cad 8 vertices: struct loop", () => sink += SumStructLoop(cadPolygons), 200, warmupIterations: 100),
            RelativeBenchmark.Measure("cad 8 vertices: cached Rect2", () => sink += SumCached(cachedCad), 200, warmupIterations: 100),
        };

        Assert.True(double.IsFinite(sink));
        _output.WriteLine(RelativeBenchmark.FormatTable(
            $"Polygon2.Bounds over {regularPolygons.Length} pads (one op = one pass over all pads)",
            samples,
            samples[0]));
    }

    [Fact]
    public void GridBuild_LocalScopes_ComparedWithGlobalScopesAndMapping()
    {
        var globalSettings = CreateSettings(WidthAdjustmentScope.ColumnGlobal, HeightAdjustmentScope.RowGlobal);
        var localSettings = CreateSettings(WidthAdjustmentScope.RowLocal, HeightAdjustmentScope.ColumnLocal);
        var global = RegularGridBuilder.BuildFromSettings(globalSettings);
        var local = RegularGridBuilder.BuildFromSettings(localSettings);

        Assert.Equal(global.Pads.Count, local.Pads.Count);
        for (var i = 0; i < global.Pads.Count; i++)
        {
            Assert.Equal(global.Pads[i].Bounds, local.Pads[i].Bounds);
        }

        var vertices = global.Pads.Select(pad => pad.Polygon.Vertices.ToArray()).ToArray();
        var keep = 0;
        var samples = new List<BenchmarkSample>
        {
            RelativeBenchmark.Measure("BuildFromSettings, global scopes", () => keep += RegularGridBuilder.BuildFromSettings(globalSettings).Pads.Count, 30),
            RelativeBenchmark.Measure("BuildFromSettings, local scopes (default)", () => keep += RegularGridBuilder.BuildFromSettings(localSettings).Pads.Count, 30),
            RelativeBenchmark.Measure("GridBuildService.Build (local scopes + diff-index mapping)", () => keep += GridBuildService.Build(null, localSettings, GridAlignmentMode.FromPanelAa).Pads.Count, 30),
            RelativeBenchmark.Measure("pad objects only (Polygon2 + RegularPad)", () => keep += BuildPadsOnly(vertices).Count, 30),
            RelativeBenchmark.Measure("RegularGrid.Bounds union (ctor pass)", () => keep += new RegularGrid(Rows, Cols, global.XEdges, global.YEdges, global.Pads).Rows, 30),
        };

        Assert.True(keep > 0);
        _output.WriteLine(RelativeBenchmark.FormatTable(
            $"Grid build {Rows}x{Cols} = {Rows * Cols} pads (one op = one full build)",
            samples,
            samples[0]));
    }

    private static GridSettings CreateSettings(WidthAdjustmentScope widthScope, HeightAdjustmentScope heightScope)
    {
        return new GridSettings
        {
            XChannels = Cols,
            YChannels = Rows,
            WidthScope = widthScope,
            HeightScope = heightScope,
            AlignmentMode = GridAlignmentMode.FromPanelAa,
            ActiveAreaWidth = Cols * 4.0,
            ActiveAreaHeight = Rows * 4.0,
        };
    }

    private static List<RegularPad> BuildPadsOnly(Point2[][] vertices)
    {
        var pads = new List<RegularPad>(vertices.Length);
        for (var i = 0; i < vertices.Length; i++)
        {
            pads.Add(new RegularPad(i / Cols, i % Cols, i, new Polygon2(vertices[i])));
        }

        return pads;
    }

    private static Polygon2 CreateOctagonInside(Rect2 cell)
    {
        var cx = (cell.MinX + cell.MaxX) * 0.5;
        var cy = (cell.MinY + cell.MaxY) * 0.5;
        var r = Math.Min(cell.Width, cell.Height) * 0.4;
        var points = new Point2[8];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = i * Math.PI / 4.0;
            points[i] = new Point2(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
        }

        return new Polygon2(points);
    }

    private static Rect2 ComputeBoundsStructLoop(Polygon2 polygon)
    {
        var vertices = polygon.Vertices;
        var minX = vertices[0].X;
        var minY = vertices[0].Y;
        var maxX = minX;
        var maxY = minY;
        for (var i = 1; i < vertices.Length; i++)
        {
            var p = vertices[i];
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        return new Rect2(minX, minY, maxX, maxY);
    }

    private static double SumCurrent(Polygon2[] polygons)
    {
        var sum = 0.0;
        foreach (var polygon in polygons)
        {
            sum += polygon.Bounds.MaxX;
        }

        return sum;
    }

    private static double SumStructLoop(Polygon2[] polygons)
    {
        var sum = 0.0;
        foreach (var polygon in polygons)
        {
            sum += ComputeBoundsStructLoop(polygon).MaxX;
        }

        return sum;
    }

    private static double SumCached(Rect2[] bounds)
    {
        var sum = 0.0;
        foreach (var rect in bounds)
        {
            sum += rect.MaxX;
        }

        return sum;
    }
}
