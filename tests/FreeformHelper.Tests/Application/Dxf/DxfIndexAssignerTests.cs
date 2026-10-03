using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfIndexAssignerTests
{
    private static readonly int[] ExpectedRightToLeftOrder = [1, 2];
    private static readonly int[] ExpectedLeftToRightOrder = [3, 4, 1, 2];

    [Fact]
    public void OrderPads_RespectsColumnLocalRowHeights_WhenAssigningScanRows()
    {
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            AlignmentMode = GridAlignmentMode.FromPanelAa,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
            HeightScope = HeightAdjustmentScope.ColumnLocal,
            ColumnWidths = new List<double> { 1, 1 },
            RowHeights = new List<double> { 1, 1 },
            ColumnHeightOverrides = new List<List<double>>
            {
                new() { 1, 1 },
                new() { 1.8, 0.2 }
            }
        };

        var grid = new RegularGridBuilder().BuildFromSettings(settings);
        var assigner = new DxfIndexAssigner();

        var topLeft = CreateCadPad(id: 1, x0: 0.20, y0: 1.40, x1: 0.40, y1: 1.60);      // row=1, col=0
        var lowerRightTallColumn = CreateCadPad(id: 2, x0: 1.40, y0: 1.50, x1: 1.60, y1: 1.70); // row=0, col=1 in local-height column

        var ordered = DxfIndexAssigner.OrderPads(
            new List<CadPad> { lowerRightTallColumn, topLeft },
            grid,
            ScanOrder.RightToLeft_TopToBottom);

        Assert.Equal(ExpectedRightToLeftOrder, ordered.Select(p => p.Id).ToArray());
    }

    [Fact]
    public void OrderPads_UsesScanOrder_RowThenColumn_ForUniformGrid()
    {
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            AlignmentMode = GridAlignmentMode.FromPanelAa,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
            HeightScope = HeightAdjustmentScope.RowGlobal,
            ColumnWidths = new List<double> { 1, 1 },
            RowHeights = new List<double> { 1, 1 }
        };

        var grid = new RegularGridBuilder().BuildFromSettings(settings);
        var assigner = new DxfIndexAssigner();

        var bottomLeft = CreateCadPad(id: 1, x0: 0.20, y0: 0.20, x1: 0.40, y1: 0.40);
        var bottomRight = CreateCadPad(id: 2, x0: 1.20, y0: 0.20, x1: 1.40, y1: 0.40);
        var topLeft = CreateCadPad(id: 3, x0: 0.20, y0: 1.20, x1: 0.40, y1: 1.40);
        var topRight = CreateCadPad(id: 4, x0: 1.20, y0: 1.20, x1: 1.40, y1: 1.40);

        var ordered = DxfIndexAssigner.OrderPads(
            new List<CadPad> { bottomLeft, topRight, topLeft, bottomRight },
            grid,
            ScanOrder.LeftToRight_TopToBottom);

        Assert.Equal(ExpectedLeftToRightOrder, ordered.Select(p => p.Id).ToArray());
    }

    private static CadPad CreateCadPad(int id, double x0, double y0, double x1, double y1)
    {
        return new CadPad(
            id,
            $"CAD{id}",
            "L1",
            new Polygon2(new[]
            {
                new Point2(x0, y0),
                new Point2(x1, y0),
                new Point2(x1, y1),
                new Point2(x0, y1)
            }));
    }
}
