// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadInfoSelectionAndDisplayBehaviorTests
{
    [Fact]
    public void SinglePadCommands_MultiSelection_LeaveMappingAndNavigationUntouched()
    {
        using var culture = TestCulture.UseEnUs();
        var invokedCadIds = new List<int>();
        var vm = new CadPadInfoViewModel(
            pads:
            [
                TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, 10, 8),
                TestGeometryFactory.CreateCadPad(275, "AA", 20, 0, 30, 8),
            ],
            getDxfIndex: static _ => 112,
            getCadOutputFwDiffOverride: static _ => 900,
            isDxfIndexAnchor: static _ => true,
            getCustomValue: static _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: (id, _) => invokedCadIds.Add(id),
            clearCadOutputFwDiffOverride: invokedCadIds.Add,
            setDxfIndexAnchor: invokedCadIds.Add,
            clearDxfIndexAnchor: invokedCadIds.Add,
            getMatchedRegularPadIds: static _ => [4808],
            focusCadMatches: invokedCadIds.Add,
            highlightCadMatches: invokedCadIds.Add);
        vm.CadOutputFwDiffOverride = 999;
        Assert.True(vm.IsMulti);
        Assert.False(vm.HasCadOutputFwDiffOverrideEditor);
        Assert.False(vm.HasDxfIndexAnchorEditor);
        Assert.False(vm.HasLocateMatchAction);

        vm.LocateMatchCommand.Execute(null);
        vm.ApplyCadOutputFwDiffOverrideCommand.Execute(null);
        vm.ClearCadOutputFwDiffOverrideCommand.Execute(null);
        vm.SetDxfIndexAnchorCommand.Execute(null);
        vm.ClearDxfIndexAnchorCommand.Execute(null);

        Assert.Empty(invokedCadIds);
        Assert.Equal("112", vm.DxfIndexText);
        Assert.Equal(999m, vm.CadOutputFwDiffOverride);
        Assert.False(vm.HasCadOutputFwDiffOverride);
        Assert.False(vm.IsDxfIndexAnchor);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void CompactSummary_WithoutNotchPreview_ShowsPlaceholdersAndGeometryOnlyStatus()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new CadPadInfoViewModel(
            pads: [TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, 10, 8)],
            getDxfIndex: static _ => 112,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: static _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null);

        Assert.Equal("-", vm.Stage3AreaCompactText);
        Assert.Equal("-", vm.TargetAllocationCompactText);
        Assert.Equal("-", vm.NotchRowCompactText);
        Assert.Equal("-", vm.OwnerSummaryCompactText);
        Assert.False(vm.HasToFullReason);
        Assert.False(vm.HasOwnerSummary);
        Assert.False(vm.HasTargetAllocationItems);
        Assert.False(vm.HasToFullDiagnosticsText);
        Assert.False(vm.HasRuleTrace);
        Assert.Equal("geometry", vm.DebugStatusText);
    }
}
