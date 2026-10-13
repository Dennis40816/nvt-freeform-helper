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
/// compatible frames. The scenario takes the slider position as an index into the complete frame
/// list. With the frames [compatible A, incompatible B, compatible C], the second slider position
/// shows frame C but requests frame B. These tests describe the current result, not the wanted
/// one. A fix that resolves the option value once changes the expected values here and must say
/// so in its pull request.
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
    public async Task SelectingTheSecondListedFrame_RequestsTheIncompatibleFrameInstead()
    {
        var viewModel = await ImportFramesAsync();

        viewModel.SelectedFrameSliderIndex = 1;

        Assert.Equal("Frame 2/2", viewModel.FrameSummaryText);
        var snapshot = viewModel.GetSimulationScenarioSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.Scenario.SelectedFrameIndex);
        var simulation = Assert.IsType<SimulationScenarioSnapshot>(snapshot);
        Assert.True(simulation is { Result.IsSupported: false });
        Assert.True(simulation is { Result.Diagnostics: { } diagnostics } &&
            diagnostics.Any(static diagnostic => diagnostic.Contains("1x3", StringComparison.Ordinal)));
        Assert.Contains("1x3", viewModel.StatusText, StringComparison.Ordinal);
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
