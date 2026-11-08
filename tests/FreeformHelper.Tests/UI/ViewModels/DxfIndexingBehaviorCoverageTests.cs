// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfIndexingBehaviorCoverageTests
{
    [AvaloniaFact]
    public async Task SetOverride_ConflictReplacesOnlySameIcAndUndoRestoresBothPreviousOverridesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync();
        var first = vm.CadPads.Single(pad => pad.Layer == "left-a").Id;
        var second = vm.CadPads.Single(pad => pad.Layer == "left-b").Id;
        var otherIc = vm.CadPads.Single(pad => pad.Layer == "right-a").Id;
        var automaticSecond = GetAssignedDiff(vm, second);
        vm.SetCadOutputFwDiffIndexOverride(first, 20);
        vm.SetCadOutputFwDiffIndexOverride(second, 30);
        vm.SetCadOutputFwDiffIndexOverride(otherIc, 30);
        Assert.Equal(30, vm.GetCadOutputFwDiffIndexOverride(second));
        Assert.Equal(30, vm.GetCadOutputFwDiffIndexOverride(otherIc));

        vm.SetCadOutputFwDiffIndexOverride(first, 30);

        Assert.Equal(30, GetAssignedDiff(vm, first));
        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(second));
        Assert.Equal(automaticSecond, GetAssignedDiff(vm, second));
        Assert.Equal(30, GetAssignedDiff(vm, otherIc));
        Assert.Equal($"CAD Output FW Diff override saved: IC 1, CAD {first} -> 30. " +
            "Replaced 1 conflicting override(s) in same IC.", vm.StatusText);
        Assert.DoesNotContain(second, vm.CadOutputFwDiffIndexOverrideCadIds);

        vm.SetCadOutputFwDiffIndexOverride(first, 30);
        vm.UndoCommand.Execute(null);

        Assert.Equal(20, vm.GetCadOutputFwDiffIndexOverride(first));
        Assert.Equal(20, GetAssignedDiff(vm, first));
        Assert.Equal(30, vm.GetCadOutputFwDiffIndexOverride(second));
        Assert.Equal(30, GetAssignedDiff(vm, second));
        Assert.Equal(30, vm.GetCadOutputFwDiffIndexOverride(otherIc));
        Assert.Contains(second, vm.CadOutputFwDiffIndexOverrideCadIds);
    }

    [AvaloniaTheory]
    [InlineData(-1, 0)]
    [InlineData(1000001, 1000000)]
    public async Task ClearOverride_AfterClampedAssignment_ReturnsToGeometryAndUndoRestoresThenRemovesOverrideAsync(
        int requestedIndex, int clampedIndex)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync();
        var cadId = vm.CadPads.Single(pad => pad.Layer == "left-b").Id;
        var automaticIndex = GetAssignedDiff(vm, cadId);
        vm.ClearCadOutputFwDiffIndexOverride(cadId);
        Assert.Equal($"CAD Output FW Diff override clear: CAD {cadId} has no override.", vm.StatusText);
        vm.SetCadOutputFwDiffIndexOverride(cadId, requestedIndex);
        Assert.Equal(clampedIndex, vm.GetCadOutputFwDiffIndexOverride(cadId));
        Assert.Equal(clampedIndex, GetAssignedDiff(vm, cadId));

        vm.ClearCadOutputFwDiffIndexOverride(cadId);

        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(cadId));
        Assert.Equal(automaticIndex, GetAssignedDiff(vm, cadId));
        Assert.DoesNotContain(cadId, vm.CadOutputFwDiffIndexOverrideCadIds);
        Assert.Equal($"CAD Output FW Diff override cleared: CAD {cadId}.", vm.StatusText);

        vm.UndoCommand.Execute(null);

        Assert.Equal(clampedIndex, vm.GetCadOutputFwDiffIndexOverride(cadId));
        Assert.Equal(clampedIndex, GetAssignedDiff(vm, cadId));
        Assert.Contains(cadId, vm.CadOutputFwDiffIndexOverrideCadIds);

        vm.UndoCommand.Execute(null);

        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(cadId));
        Assert.Equal(automaticIndex, GetAssignedDiff(vm, cadId));
        Assert.DoesNotContain(cadId, vm.CadOutputFwDiffIndexOverrideCadIds);
    }

    private static async Task<FreeformHelperViewModel> OpenWorkspaceAsync()
    {
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("left-a", 0, 0, 10, 10), ("left-b", 10, 0, 20, 10),
            ("right-a", 20, 0, 30, 10), ("right-b", 30, 0, 40, 10));
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(TestFiles.NewTempFilePath("indexing-app-", ".json")))
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            ActiveAreaWidth = 40,
            ActiveAreaHeight = 10,
            PanelBiasX = 0,
            PanelBiasY = 0,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            GridPaddingPercent = 0,
            CascadeNum = 2,
            SelectedScanOrder = ScanOrder.LeftToRight_TopToBottom,
            RecalcBoundsOnLayerFilter = false,
            CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
        };
        Assert.Equal(2, vm.CascadeIcSettings.Count);
        foreach (var cascade in vm.CascadeIcSettings)
        {
            cascade.XChannels = 2;
            cascade.YChannels = 1;
        }

        await vm.WaitForGridRebuildIdleAsync();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.Equal(4, vm.CadPads.Count);
        Assert.Equal(4, vm.RegularPads.Count);
        Assert.Equal(2, vm.RegularPads.Count(pad => pad.IcIndex == 0));
        Assert.Equal(2, vm.RegularPads.Count(pad => pad.IcIndex == 1));
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        return vm;
    }

    private static int GetAssignedDiff(FreeformHelperViewModel vm, int cadId)
    {
        Assert.True(vm.TryGetCadOutputFwDiffIndex(cadId, out var index));
        return index;
    }
}
