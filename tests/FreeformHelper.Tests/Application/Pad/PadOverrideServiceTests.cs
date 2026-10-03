using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadOverrideServiceTests
{
    private static readonly double[] GridXEdges = [0.0, 10.0, 20.0];
    private static readonly double[] GridYEdges = [0.0, 10.0];

    [Fact]
    public void Capture_SavesFreeformAndCustomValues()
    {
        var grid = CreateGrid();
        grid.Pads[0].Freeform = FreeformType.XWay;

        var file = new ProjectFile();
        var cadValues = new Dictionary<int, double> { { 1, 2.5 } };
        _ = new PadOverrideService();
        PadOverrideService.Capture(file, grid, cadValues);

        Assert.Equal(cadValues, file.CadPadCustomValues);
        Assert.True(file.FreeformOverrides.ContainsKey(grid.Pads[0].Index));
        Assert.Equal(FreeformType.XWay, file.FreeformOverrides[grid.Pads[0].Index]);
    }

    [Fact]
    public void ApplyFreeformOverrides_RehydratesGrid()
    {
        var grid = CreateGrid();
        var file = new ProjectFile
        {
            FreeformOverrides = new Dictionary<int, FreeformType>
            {
                { grid.Pads[1].Index, FreeformType.YWay }
            }
        };
        _ = new PadOverrideService();
        PadOverrideService.ApplyFreeformOverrides(file, grid);

        Assert.Equal(FreeformType.YWay, grid.Pads[1].Freeform);
    }

    private static RegularGrid CreateGrid()
    {
        var pad0 = new RegularPad(0, 0, 0, new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        }));

        var pad1 = new RegularPad(0, 1, 1, new Polygon2(new[]
        {
            new Point2(10, 0),
            new Point2(20, 0),
            new Point2(20, 10),
            new Point2(10, 10),
        }));

        return new RegularGrid(
            rows: 1,
            cols: 2,
            xEdges: GridXEdges,
            yEdges: GridYEdges,
            pads: new List<RegularPad> { pad0, pad1 });
    }
}
