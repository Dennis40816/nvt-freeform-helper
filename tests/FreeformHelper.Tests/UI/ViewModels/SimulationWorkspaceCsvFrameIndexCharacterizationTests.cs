using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how the simulation workspace maps the frame slider to a CSV frame when some frames are
/// incompatible with the grid (issue #64). The slider and the frame option list contain only the
/// compatible frames. The scenario resolves the slider position to the global index of the listed
/// frame. With the frames [compatible A, incompatible B, compatible C], the second slider
/// position shows frame C and requests frame C.
/// </summary>
public sealed class SimulationWorkspaceCsvFrameIndexCharacterizationTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] _diffIndices = [10, 11];

    [Fact]
    public async Task FrameOptions_WithAnIncompatibleFrameInTheMiddle_ListOnlyTheCompatibleFramesWithTheirGlobalIndex()
    {
        var viewModel = await ImportFramesAsync();

        Assert.Equal(2, viewModel.FrameOptions.Count);
        Assert.Equal(0, viewModel.FrameOptions[0].Value);
        Assert.Equal(2, viewModel.FrameOptions[1].Value);
        Assert.Equal("Frame 1/2", viewModel.FrameSummaryText);
    }

    [Fact]
    public async Task SelectingTheSecondListedFrame_RequestsThatFrameByItsGlobalIndex()
    {
        var viewModel = await ImportFramesAsync();

        viewModel.SelectedFrameSliderIndex = 1;

        Assert.Equal("Frame 2/2", viewModel.FrameSummaryText);
        var snapshot = viewModel.GetSimulationScenarioSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.Scenario.SelectedFrameIndex);
        var simulation = Assert.IsType<SimulationScenarioSnapshot>(snapshot);
        Assert.True(simulation is { SimulationResult.IsSupported: true });
        Assert.DoesNotContain("1x3", viewModel.StatusText, StringComparison.Ordinal);
    }

    private static async Task<SimulationWorkspaceViewModel> ImportFramesAsync()
    {
        var compatibleFirst = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        var incompatibleMiddle = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"3",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:265",Frame index:"0",Break point index:"0",DiffData:
            7,7,7
            """);
        var compatibleLast = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:365",Frame index:"0",Break point index:"0",DiffData:
            200,80
            """);
        var paths = new[] { compatibleFirst, incompatibleMiddle, compatibleLast };
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSession())
        {
            PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(paths),
        };

        await viewModel.ImportCsvAsync();
        return viewModel;
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
            CadPadsForCopperProjection: Array.Empty<CadPad>(),
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int>(),
            CadIcIndexByCadId: new Dictionary<int, int>());
    }
}
