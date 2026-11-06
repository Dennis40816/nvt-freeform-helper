using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationWorkspaceCommandTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] _diffIndices = { 10, 11 };

    [Fact]
    public async Task ClearCsvCommand_WhenCopperSourceIsActive_ClearsCsvAndPreservesCopperValuesAsync()
    {
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        try
        {
            var viewModel = CreateViewModel();
            Assert.False(viewModel.ClearCsvCommand.CanExecute(null));
            viewModel.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(new[] { csvPath });
            await viewModel.ImportCsvAsync();
            Assert.Single(viewModel.CsvFiles);
            Assert.True(viewModel.HasCsvFiles);
            Assert.True(viewModel.HasCsvData);
            SetCopperSource(viewModel);
            viewModel.MoveCopperToWorldPoint(new Point2(0.5d, 0.5d));
            viewModel.SelectRegularPad(0);
            Assert.Equal("400", viewModel.SelectedBeforeText);
            Assert.True(viewModel.ClearCsvCommand.CanExecute(null));

            viewModel.ClearCsvCommand.Execute(null);

            Assert.Empty(viewModel.CsvFiles);
            Assert.False(viewModel.HasCsvFiles);
            Assert.False(viewModel.HasCsvData);
            Assert.False(viewModel.ClearCsvCommand.CanExecute(null));
            Assert.Empty(viewModel.FrameOptions);
            Assert.True(viewModel.IsCopperSource);
            Assert.Equal(0, viewModel.SelectedRegularPadId);
            Assert.Equal("400", viewModel.SelectedBeforeText);
            Assert.Equal("200", viewModel.SelectedAfterText);
        }
        finally
        {
            File.Delete(csvPath);
        }
    }

    [Fact]
    public void ClearSelectedOverrideCommand_AfterBaselineChanges_RestoresCurrentComputedValues()
    {
        var viewModel = CreateViewModel();
        viewModel.GlobalValue = 100d;
        viewModel.SelectRegularPad(0);
        Assert.False(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
        ApplyOverride(viewModel, regularPadId: 0, value: 123d);
        viewModel.GlobalValue = 200d;
        Assert.True(viewModel.HasSelectedOverride);
        Assert.Equal("123", viewModel.SelectedBeforeText);
        Assert.Equal("62", viewModel.SelectedAfterText);
        Assert.True(viewModel.ClearSelectedOverrideCommand.CanExecute(null));

        viewModel.ClearSelectedOverrideCommand.Execute(null);

        Assert.False(viewModel.HasSelectedOverride);
        Assert.False(viewModel.HasManualOverrides);
        Assert.False(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
        Assert.Equal(0, viewModel.SelectedRegularPadId);
        Assert.Equal(200d, viewModel.SelectedManualValue);
        Assert.Equal("200", viewModel.SelectedBeforeText);
        Assert.Equal("100", viewModel.SelectedAfterText);
    }

    [Fact]
    public void ClearAllManualOverridesCommand_WhenAreaIsFiltered_ClearsOverridesOutsideVisibleArea()
    {
        var viewModel = CreateViewModel();
        viewModel.GlobalValue = 100d;
        Assert.False(viewModel.ClearAllManualOverridesCommand.CanExecute(null));
        ApplyOverride(viewModel, regularPadId: 0, value: 123d);
        ApplyOverride(viewModel, regularPadId: 1, value: 234d);
        viewModel.SelectRegularPad(0);
        viewModel.SelectedAreaOption = viewModel.AreaOptions.Single(option => option.IcIndex == 0);
        Assert.Single(viewModel.SimulationOverlayItems);
        Assert.True(viewModel.HasSelectedOverride);
        Assert.True(viewModel.ClearAllManualOverridesCommand.CanExecute(null));

        viewModel.ClearAllManualOverridesCommand.Execute(null);

        Assert.False(viewModel.HasManualOverrides);
        Assert.False(viewModel.HasSelectedOverride);
        Assert.False(viewModel.ClearAllManualOverridesCommand.CanExecute(null));
        Assert.False(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
        Assert.Equal("100", viewModel.SelectedBeforeText);
        Assert.Equal("50", viewModel.SelectedAfterText);
        viewModel.SelectedAreaOption = viewModel.AreaOptions.Single(option => !option.IcIndex.HasValue);
        viewModel.SelectRegularPad(1);
        Assert.False(viewModel.HasSelectedOverride);
        Assert.False(viewModel.ClearSelectedOverrideCommand.CanExecute(null));
        Assert.Equal("100", viewModel.SelectedBeforeText);
        Assert.Equal("100", viewModel.SelectedAfterText);
    }

    [Fact]
    public void SelectCopperPathReplayRowCommand_IgnoresNullBeforeAndAfterSelection()
    {
        var viewModel = CreateViewModel();
        viewModel.CopperDiameter = 0.5d;
        viewModel.CopperPeakValue = 400d;
        viewModel.ReplayCopperPath(new Point2(0.5d, 0.5d), new Point2(1.5d, 0.5d), stepCount: 2);
        var initialCenter = new Point2(viewModel.CopperCenterX, viewModel.CopperCenterY);
        var initialStatus = viewModel.StatusText;
        Assert.Null(viewModel.SelectedCopperPathReplayRow);
        Assert.True(viewModel.SelectCopperPathReplayRowCommand.CanExecute(null));

        viewModel.SelectCopperPathReplayRowCommand.Execute(null);

        Assert.Null(viewModel.SelectedCopperPathReplayRow);
        Assert.True(viewModel.IsManualSource);
        Assert.Equal(initialCenter.X, viewModel.CopperCenterX);
        Assert.Equal(initialCenter.Y, viewModel.CopperCenterY);
        Assert.Equal(initialStatus, viewModel.StatusText);
        var row = viewModel.CopperPathReplayRows[0];
        Assert.True(viewModel.SelectCopperPathReplayRowCommand.CanExecute(row));

        viewModel.SelectCopperPathReplayRowCommand.Execute(row);

        Assert.Same(row, viewModel.SelectedCopperPathReplayRow);
        Assert.True(viewModel.IsCopperSource);
        Assert.Equal(row.CenterX, viewModel.CopperCenterX);
        Assert.Equal(row.CenterY, viewModel.CopperCenterY);
        viewModel.SelectRegularPad(0);
        Assert.Equal("400", viewModel.SelectedBeforeText);
        Assert.Equal("200", viewModel.SelectedAfterText);
        var selectedStatus = viewModel.StatusText;
        Assert.True(viewModel.SelectCopperPathReplayRowCommand.CanExecute(null));

        viewModel.SelectCopperPathReplayRowCommand.Execute(null);

        Assert.Same(row, viewModel.SelectedCopperPathReplayRow);
        Assert.True(viewModel.IsCopperSource);
        Assert.Equal(row.CenterX, viewModel.CopperCenterX);
        Assert.Equal(row.CenterY, viewModel.CopperCenterY);
        Assert.Equal(0, viewModel.SelectedRegularPadId);
        Assert.Equal("400", viewModel.SelectedBeforeText);
        Assert.Equal(selectedStatus, viewModel.StatusText);
    }

    private static void ApplyOverride(SimulationWorkspaceViewModel viewModel, int regularPadId, double value)
    {
        viewModel.SelectRegularPad(regularPadId);
        viewModel.SelectedManualValue = value;
        Assert.True(viewModel.ApplySelectedOverrideCommand.CanExecute(null));
        viewModel.ApplySelectedOverrideCommand.Execute(null);
        Assert.True(viewModel.HasSelectedOverride);
    }

    private static void SetCopperSource(SimulationWorkspaceViewModel viewModel)
    {
        viewModel.CopperDiameter = 0.5d;
        viewModel.CopperPeakValue = 400d;
        viewModel.CopperBaselineValue = 0d;
        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Copper);
    }

    private static SimulationWorkspaceViewModel CreateViewModel()
    {
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession());
    }

    private static SimulationWorkspaceSession BuildSession()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(_diffIndices, icIndexSelector: static col => col);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, NullDiffValue, 0, 0),
                comment: "sample"),
        });

        return new SimulationWorkspaceSession(
            grid,
            table,
            NullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            CadPadsForCopperProjection: new[]
            {
                TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d),
                TestGeometryFactory.CreateCadPad(101, "SENSOR", 1d, 0d, 2d, 1d),
            },
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int>
            {
                [100] = 10,
                [101] = 11,
            },
            CadIcIndexByCadId: new Dictionary<int, int>
            {
                [100] = 0,
                [101] = 1,
            });
    }
}
