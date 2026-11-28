// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadInfoIndexCommandBehaviorTests
{
    [Theory]
    [InlineData(37, "37")]
    [InlineData(null, "-")]
    public void ClearOverride_ReloadsAutomaticIndexAfterRemovingManualValue(int? automaticIndex, string expectedText)
    {
        using var culture = TestCulture.UseEnUs();
        int? manualValue = 900;
        var clearedCadIds = new List<int>();
        var vm = new CadPadInfoViewModel(
            pads: [TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, 10, 8)],
            getDxfIndex: _ => manualValue ?? automaticIndex,
            getCadOutputFwDiffOverride: _ => manualValue,
            isDxfIndexAnchor: null,
            getCustomValue: static _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: (_, value) => manualValue = value,
            clearCadOutputFwDiffOverride: id =>
            {
                clearedCadIds.Add(id);
                manualValue = null;
            },
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null);
        Assert.Equal("900", vm.DxfIndexText);
        Assert.True(vm.HasCadOutputFwDiffOverride);
        Assert.Equal("Manual override", vm.CadOutputFwDiffAssignmentModeText);

        vm.ClearCadOutputFwDiffOverrideCommand.Execute(null);

        Assert.Equal(274, Assert.Single(clearedCadIds));
        Assert.Null(manualValue);
        Assert.Equal(expectedText, vm.DxfIndexText);
        Assert.False(vm.HasCadOutputFwDiffOverride);
        Assert.False(vm.CanClearCadOutputFwDiffOverride);
        Assert.Equal("Auto (geometry seed)", vm.CadOutputFwDiffAssignmentModeText);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void AnchorCommands_WithoutStateReader_RefreshIndexAndUseCommandState()
    {
        using var culture = TestCulture.UseEnUs();
        int? visibleIndex = 112;
        var anchoredCadIds = new List<int>();
        var clearedCadIds = new List<int>();
        var vm = new CadPadInfoViewModel(
            pads: [TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, 10, 8)],
            getDxfIndex: _ => visibleIndex,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: static _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: id =>
            {
                anchoredCadIds.Add(id);
                visibleIndex = 37;
            },
            clearDxfIndexAnchor: id =>
            {
                clearedCadIds.Add(id);
                visibleIndex = null;
            });
        Assert.False(vm.IsDxfIndexAnchor);
        Assert.True(vm.HasDxfIndexAnchorEditor);

        vm.SetDxfIndexAnchorCommand.Execute(null);

        Assert.Equal(274, Assert.Single(anchoredCadIds));
        Assert.True(vm.IsDxfIndexAnchor);
        Assert.True(vm.CanClearDxfIndexAnchor);
        Assert.Equal("37", vm.DxfIndexText);
        Assert.Equal("Anchor saved (inactive in current mode)", vm.CadOutputFwDiffAssignmentModeText);

        vm.ClearDxfIndexAnchorCommand.Execute(null);

        Assert.Equal(274, Assert.Single(clearedCadIds));
        Assert.False(vm.IsDxfIndexAnchor);
        Assert.False(vm.CanClearDxfIndexAnchor);
        Assert.Equal("-", vm.DxfIndexText);
        Assert.Equal("Auto (geometry seed)", vm.CadOutputFwDiffAssignmentModeText);
        Assert.False(vm.HasPendingChanges);
    }
}
