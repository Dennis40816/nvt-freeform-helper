// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceSelectionGuardTests
{
    [Fact]
    public void ManualPreset_InvalidInput_PreservesBaselineOverrideAndInspector()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.GlobalValue = 100d;
        vm.SelectRegularPad(0);
        vm.SelectedManualValue = 240d;
        vm.ApplySelectedOverrideCommand.Execute(null);
        var status = vm.StatusText;

        vm.ApplyManualPresetCommand.Execute("invalid");

        Assert.Equal(100d, vm.GlobalValue);
        Assert.True(vm.HasSelectedOverride);
        Assert.Equal(240d, vm.SelectedManualValue);
        Assert.Equal("240", vm.SelectedBeforeText);
        Assert.Equal("120", vm.SelectedAfterText);
        Assert.Equal("Manual override", vm.SelectedSourceText);
        Assert.Equal(status, vm.StatusText);
    }

    [Fact]
    public void OverrideCommands_WithoutSelectionOrOverrides_PreserveThePresetAndUnappliedEditorValue()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.ApplyManualPresetCommand.Execute("100");
        vm.SelectedManualValue = 240d;
        Assert.False(vm.HasSelectedRegular);
        Assert.False(vm.ApplySelectedOverrideCommand.CanExecute(null));
        Assert.False(vm.ClearSelectedOverrideCommand.CanExecute(null));
        Assert.False(vm.ClearAllManualOverridesCommand.CanExecute(null));

        vm.ApplySelectedOverrideCommand.Execute(null);

        Assert.False(vm.HasManualOverrides);
        Assert.Equal(240d, vm.SelectedManualValue);
        vm.ClearSelectedOverrideCommand.Execute(null);
        Assert.Equal(240d, vm.SelectedManualValue);
        vm.ClearAllManualOverridesCommand.Execute(null);

        Assert.False(vm.HasManualOverrides);
        Assert.False(vm.HasSelectedRegular);
        Assert.Equal(100d, vm.GlobalValue);
        Assert.Equal("Manual preset applied: uniform 100.", vm.StatusText);
        vm.SelectRegularPad(0);
        Assert.Equal("100", vm.SelectedBeforeText);
        Assert.Equal("50", vm.SelectedAfterText);
    }

    [Fact]
    public void RandomizeVisibleValues_WhenSelectedIcHasNoActivePads_PreservesValuesAndOtherIcOverrides()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.GlobalValue = 100d;
        vm.SelectRegularPad(0);
        vm.SelectedManualValue = 240d;
        vm.ApplySelectedOverrideCommand.Execute(null);
        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => option.IcIndex == 1);
        vm.SelectRegularPad(1);
        var status = vm.StatusText;
        Assert.False(vm.RandomizeVisibleValuesCommand.CanExecute(null));

        vm.RandomizeVisibleValuesCommand.Execute(null);

        Assert.Equal(1, vm.SelectedRegularPadId);
        Assert.False(vm.HasSelectedOverride);
        Assert.Equal("0", vm.SelectedBeforeText);
        Assert.Equal("0", vm.SelectedAfterText);
        Assert.Equal(status, vm.StatusText);
        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => option.IcIndex == 0);
        vm.SelectRegularPad(0);
        Assert.True(vm.HasSelectedOverride);
        Assert.Equal(240d, vm.SelectedManualValue);
        Assert.Equal("240", vm.SelectedBeforeText);
        Assert.Equal("120", vm.SelectedAfterText);
    }

    private static SimulationWorkspaceViewModel CreateWorkspace()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11], icIndexSelector: static col => col);
        var table = new NotchTable(
        [
            new NotchTableRow(0, 10, 0, 313, new NotchV22Node(10, 50, 65535, 0, 65535, 0, 0), "half gain"),
        ]);
        var session = new SimulationWorkspaceSession(
            grid, table, 65535, SourceRevision: 1, ActiveRegularPadIds: new HashSet<int> { 0 });
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);
    }
}
