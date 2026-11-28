// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfLayerMoveSelectionBehaviorTests
{
    [AvaloniaFact]
    public async Task MoveToLayer_MixedSelection_RevealsTargetAndPreservesOtherLayerFiltersAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new DxfEditingBehaviorTestScope();
        var vm = files.CreateViewModel();
        await files.OpenAsync(vm,
            ("alpha", 0, 0, 10, 10), ("beta", 20, 0, 30, 10), ("gamma", 40, 0, 50, 10));
        var originals = vm.CadPads.OrderBy(static pad => pad.Bounds.MinX).ToArray();
        Assert.Equal("0 CAD pads selected", vm.DxfEditSelectionSummary);
        vm.LayerToggles.Single(static layer => layer.Name == "beta").IsSelected = false;
        vm.LayerToggles.Single(static layer => layer.Name == "gamma").IsSelected = false;
        await SelectCadPadsAsync(vm, [originals[0].Id]);
        Assert.Equal("1 CAD pad selected", vm.DxfEditSelectionSummary);
        await SelectCadPadsAsync(vm, [originals[0].Id, originals[1].Id]);
        Assert.Equal("2 CAD pads selected", vm.DxfEditSelectionSummary);
        vm.DxfEditTargetLayerName = " beta ";
        Assert.True(vm.CanMoveSelectedCadPadsToLayer);

        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);

        Assert.Equal("DXF edit: moved 1 CAD pad(s) to layer 'beta'.", vm.StatusText);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(2, vm.CadPads.Count);
        foreach (var original in originals.Take(2))
        {
            var current = Assert.Single(vm.CadPads, pad => pad.Id == original.Id);
            Assert.Equal("beta", current.Layer);
            Assert.Equal(original.Polygon.Vertices.ToArray(), current.Polygon.Vertices.ToArray());
        }
        Assert.True(vm.LayerToggles.Single(static layer => layer.Name == "beta").IsSelected);
        Assert.False(vm.LayerToggles.Single(static layer => layer.Name == "gamma").IsSelected);
        Assert.Equal("beta", vm.DxfEditTargetLayerName);
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal("0 CAD pads selected", vm.DxfEditSelectionSummary);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveToSameLayer_IgnoresCaseAndWhitespaceWithoutCreatingAnEditAsync(bool createLayer)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new DxfEditingBehaviorTestScope();
        var vm = files.CreateViewModel();
        await files.OpenAsync(vm, ("alpha", 0, 0, 10, 20));
        var original = Assert.Single(vm.CadPads);
        await SelectCadPadsAsync(vm, [original.Id]);
        vm.HasUnsavedChanges = false;
        vm.DxfEditTargetLayerName = " ALPHA ";
        vm.DxfEditNewLayerName = " ALPHA ";

        if (createLayer)
        {
            vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
            Assert.Empty(vm.DxfEditNewLayerName);
        }
        else
        {
            vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        }

        Assert.Equal("DXF edit: selected pads are already in layer 'ALPHA'.", vm.StatusText);
        var current = Assert.Single(vm.CadPads);
        Assert.Equal("alpha", current.Layer);
        Assert.Equal(original.Polygon.Vertices.ToArray(), current.Polygon.Vertices.ToArray());
        Assert.Equal("alpha", Assert.Single(vm.LayerToggles).Name);
        Assert.Equal(original.Id, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal("1 CAD pad selected", vm.DxfEditSelectionSummary);
        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }
}
