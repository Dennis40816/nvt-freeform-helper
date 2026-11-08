// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceCsvFrameSelectionBehaviorTests
{
    private const string IncompatibleCsv =
        """
        LogVer:"1.0",Xch:"2",Ych:"1",
        timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
        7,7
        """;

    private const string CompatibleCsv =
        """
        LogVer:"1.0",Xch:"1",Ych:"1",
        timestamp(mm:ss:nnn):"09:14:265",Frame index:"0",Break point index:"0",DiffData:
        100
        """;

    [Fact]
    public async Task ImportCsv_IncompatibleFileBeforeACompatibleFile_ShowsTheCompatibleFrameAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace([10]);
        var incompatible = TestFiles.WriteTempCsv(IncompatibleCsv);
        var compatible = TestFiles.WriteTempCsv(CompatibleCsv);
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([incompatible, compatible]);

        await vm.ImportCsvAsync();

        Assert.True(vm.HasCsvData);
        Assert.Single(vm.FrameOptions);
        Assert.Single(vm.SimulationOverlayItems);
        vm.SelectRegularPad(0);
        Assert.Equal("100", vm.SelectedBeforeText);
    }

    [Fact]
    public async Task ImportCsv_CompatibleFileBeforeAnIncompatibleFile_ShowsTheCompatibleFrameAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace([10]);
        var compatible = TestFiles.WriteTempCsv(CompatibleCsv);
        var incompatible = TestFiles.WriteTempCsv(IncompatibleCsv);
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([compatible, incompatible]);

        await vm.ImportCsvAsync();

        Assert.True(vm.HasCsvData);
        Assert.Single(vm.FrameOptions);
        Assert.Single(vm.SimulationOverlayItems);
        vm.SelectRegularPad(0);
        Assert.Equal("100", vm.SelectedBeforeText);
    }

    private static SimulationWorkspaceViewModel CreateWorkspace(IReadOnlyList<int> diffIndices)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(diffIndices);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);
    }
}
