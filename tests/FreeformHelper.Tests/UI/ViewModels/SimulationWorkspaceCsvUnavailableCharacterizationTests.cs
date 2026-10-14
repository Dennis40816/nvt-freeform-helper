using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the simulation workspace builds when the CSV source is selected but no compatible
/// CSV frame exists (issue #64). The source stays CSV, but the scenario is the manual baseline,
/// and it is supported, so the audit shows a result that does not come from any CSV data. These
/// tests describe the current result, not the wanted one. A fix that reports an explicit
/// unavailable CSV result changes the expected values here and must say so in its pull request.
/// </summary>
public sealed class SimulationWorkspaceCsvUnavailableCharacterizationTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] _diffIndices = [10, 11];

    [Fact]
    public void SelectingTheCsvSourceBeforeAnyImport_BuildsASupportedManualScenario()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.First(
            static option => option.Value == SimulationInputSourceMode.Csv);

        AssertManualScenarioUnderCsvSource(viewModel);
    }

    [Fact]
    public async Task ImportingOnlyIncompatibleCsv_BuildsASupportedManualScenario()
    {
        var incompatible = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"3",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:265",Frame index:"0",Break point index:"0",DiffData:
            7,7,7
            """);
        var viewModel = CreateViewModel();
        viewModel.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(new[] { incompatible });

        await viewModel.ImportCsvAsync();

        Assert.Empty(viewModel.FrameOptions);
        AssertManualScenarioUnderCsvSource(viewModel);
    }

    private static void AssertManualScenarioUnderCsvSource(SimulationWorkspaceViewModel viewModel)
    {
        Assert.True(viewModel.IsCsvSource);
        Assert.False(viewModel.HasCsvData);
        var snapshot = Assert.IsType<SimulationScenarioSnapshot>(viewModel.GetSimulationScenarioSnapshot());
        Assert.Equal(SimulationScenarioKind.Manual, snapshot.Scenario.Kind);
        Assert.True(snapshot is { Result.IsSupported: true });
    }

    private static SimulationWorkspaceViewModel CreateViewModel()
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
        var session = new SimulationWorkspaceSession(
            grid,
            table,
            NullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            CadPadsForCopperProjection: Array.Empty<CadPad>(),
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int>(),
            CadIcIndexByCadId: new Dictionary<int, int>());
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session);
    }
}
