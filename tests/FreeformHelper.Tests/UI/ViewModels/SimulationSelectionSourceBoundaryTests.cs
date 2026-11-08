// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationSelectionSourceBoundaryTests
{
    [Fact]
    public async Task SelectedOverride_WithCsvLoaded_ChangesManualValuesWithoutChangingTheCsvProjectionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.GlobalValue = 20d;
        var path = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([path]);
        await vm.ImportCsvAsync();
        Assert.True(vm.HasCsvData);
        Assert.True(vm.IsCsvSource);
        vm.SelectRegularPad(0);
        Assert.True(vm.ApplySelectedOverrideCommand.CanExecute(null));

        vm.SelectedManualValue = 240d;
        vm.ApplySelectedOverrideCommand.Execute(null);

        Assert.True(vm.HasSelectedOverride);
        Assert.Equal("100", vm.SelectedBeforeText);
        Assert.Equal("50", vm.SelectedAfterText);
        vm.SelectedInputSourceOption = vm.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Manual);
        Assert.Equal("240", vm.SelectedBeforeText);
        Assert.Equal("120", vm.SelectedAfterText);
        Assert.Equal("Manual override", vm.SelectedSourceText);
        vm.SelectedInputSourceOption = vm.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Csv);
        Assert.True(vm.ClearSelectedOverrideCommand.CanExecute(null));

        vm.ClearSelectedOverrideCommand.Execute(null);

        Assert.False(vm.HasManualOverrides);
        Assert.False(vm.ClearSelectedOverrideCommand.CanExecute(null));
        Assert.Equal(20d, vm.SelectedManualValue);
        Assert.Equal("100", vm.SelectedBeforeText);
        Assert.Equal("50", vm.SelectedAfterText);
        vm.SelectedInputSourceOption = vm.SourceOptions.Single(option => option.Value == SimulationInputSourceMode.Manual);
        Assert.Equal("20", vm.SelectedBeforeText);
        Assert.Equal("10", vm.SelectedAfterText);
        Assert.Equal("Global baseline", vm.SelectedSourceText);
    }

    [Fact]
    public void ManualPreset_EqualToCurrentBaseline_RecomputesValuesAfterClearingAnUnselectedPadsOverride()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.GlobalValue = 100d;
        vm.SelectRegularPad(0);
        vm.SelectedManualValue = 240d;
        vm.ApplySelectedOverrideCommand.Execute(null);
        Assert.Equal("240", vm.SelectedBeforeText);
        vm.SelectRegularPad(-1);

        vm.ApplyManualPresetCommand.Execute("100");

        Assert.Equal(100d, vm.GlobalValue);
        Assert.False(vm.HasManualOverrides);
        Assert.False(vm.HasSelectedRegular);
        Assert.False(vm.ClearAllManualOverridesCommand.CanExecute(null));
        Assert.Equal("Manual preset applied: uniform 100.", vm.StatusText);
        vm.SelectRegularPad(0);
        Assert.Equal(100d, vm.SelectedManualValue);
        Assert.Equal("100", vm.SelectedBeforeText);
        Assert.Equal("50", vm.SelectedAfterText);
        Assert.Equal("Global baseline", vm.SelectedSourceText);
    }

    [Fact]
    public void SelectionAndHover_UnknownOrFilteredTargets_DoNotReplaceTheCurrentInspectorWithUnknownData()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.GlobalValue = 100d;
        vm.SelectRegularPad(0);
        var header = vm.SelectedRegularHeaderText;
        var impact = Assert.Single(vm.SelectedImpactItems);

        vm.SelectRegularPad(999);

        Assert.Equal(0, vm.SelectedRegularPadId);
        Assert.Equal(header, vm.SelectedRegularHeaderText);
        Assert.Equal("100", vm.SelectedBeforeText);
        Assert.Equal("50", vm.SelectedAfterText);
        Assert.Equal(impact, Assert.Single(vm.SelectedImpactItems));
        Assert.Null(vm.BuildHoverTipText(999));
        Assert.NotNull(vm.BuildHoverTipText(0));

        vm.SelectedAreaOption = vm.AreaOptions.Single(option => option.IcIndex == 1);

        Assert.Null(vm.BuildHoverTipText(0));
        Assert.NotNull(vm.BuildHoverTipText(1));
        Assert.False(vm.HasSelectedRegular);
        Assert.Empty(vm.SelectedImpactItems);
        Assert.False(vm.ApplySelectedOverrideCommand.CanExecute(null));
        Assert.Equal("-", vm.SelectedBeforeText);
    }

    private static SimulationWorkspaceViewModel CreateWorkspace()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11], icIndexSelector: static col => col);
        var table = new NotchTable(
        [
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, 65535, 0, 0),
                comment: "selection source sample"),
        ]);
        var session = new SimulationWorkspaceSession(
            grid, table, 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);
    }
}
