using Avalonia;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceViewsSmokeTests
{
    private const int SimulationNullDiffValue = 65535;
    private static readonly int[] SimulationDiffIndices = { 10, 11 };
    private static readonly string[] CoordinateLayerNames = ["AA.drawing", "SIG"];

    [AvaloniaFact]
    public void CoordinatePlannerWorkspaceView_Can_Layout_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new CoordinatePlannerWorkspaceView
        {
            DataContext = new CoordinatePlannerWorkspaceViewModel(
                new CoordinatePlannerWorkspaceUseCase(),
                new CoordinatePlannerWorkspaceSession(
                    BuildCoordinateGrid(),
                    new[]
                    {
                        BuildCadPad(1, "AA.drawing", 0d, 0d, 10d, 5d),
                        BuildCadPad(2, "SIG", 2d, 1d, 4d, 3d),
                    },
                    CoordinateLayerNames,
                    SourceRevision: 0,
                    DefaultMachineWidth: 100d,
                    DefaultMachineHeight: 50d,
                    DefaultPixelWidth: 1000,
                    DefaultPixelHeight: 500))
        };

        var size = new Size(1400, 900);
        view.Measure(size);
        view.Arrange(new Rect(size));

        Assert.NotNull(view);
    }

    [AvaloniaFact]
    public void SimulationWorkspaceView_Can_Layout_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var grid = BuildSimulationGrid();
        var view = new SimulationWorkspaceView
        {
            DataContext = new SimulationWorkspaceViewModel(
                new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
                new SimulationWorkspaceSession(
                    grid,
                    BuildSimulationTable(),
                    SimulationNullDiffValue,
                    SourceRevision: 0,
                    ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet()))
        };

        var size = new Size(1400, 900);
        view.Measure(size);
        view.Arrange(new Rect(size));

        Assert.NotNull(view);
    }

    private static RegularGrid BuildCoordinateGrid()
    {
        return TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d);
    }

    private static CadPad BuildCadPad(int id, string layer, double minX, double minY, double maxX, double maxY)
    {
        return TestGeometryFactory.CreateCadPad(id, layer, minX, minY, maxX, maxY);
    }

    private static RegularGrid BuildSimulationGrid()
    {
        return TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
    }

    private static NotchTable BuildSimulationTable()
    {
        var rows = new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, SimulationNullDiffValue, 0, 0),
                comment: "sample")
        };

        return new NotchTable(rows);
    }
}
