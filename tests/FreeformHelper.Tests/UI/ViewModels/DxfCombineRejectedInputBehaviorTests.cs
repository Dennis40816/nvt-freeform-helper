// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfCombineRejectedInputBehaviorTests
{
    [AvaloniaFact]
    public async Task Combine_AfterReloadWithStaleCanvasSelection_RequiresTwoCurrentOutputPadsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new DxfEditingBehaviorTestScope();
        var vm = files.CreateViewModel();
        await files.OpenAsync(vm, ("signal", 0, 0, 10, 10), ("signal", 30, 0, 40, 10));
        var previousIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        Assert.Equal(2, previousIds.Length);
        await files.OpenAsync(vm, ("replacement", 100, 0, 110, 20));
        var replacement = Assert.Single(vm.CadPads);
        Assert.Contains(replacement.Id, previousIds);
        await SelectCadPadsAsync(vm, previousIds);
        vm.HasUnsavedChanges = false;

        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF combine: select at least 2 CAD output pads.", vm.StatusText);
        Assert.Equal(replacement, Assert.Single(vm.CadPads));
        Assert.Equal(previousIds, vm.SelectedCadPadIds.Order().ToArray());
        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task Combine_TinyImportedOutlinesWithNoUsableUnion_PreservesSourcesSelectionAndUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new DxfEditingBehaviorTestScope();
        var vm = files.CreateViewModel();
        await files.OpenAsync(vm,
            ("signal", 0, 0, 0.00002, 0.00002), ("signal", 1, 0, 1.00002, 0.00002));
        var originals = vm.CadPads.OrderBy(static pad => pad.Id).ToArray();
        Assert.Equal(2, originals.Length);
        Assert.All(originals, static pad => Assert.True(pad.Area > 0));
        var ids = originals.Select(static pad => pad.Id).ToArray();
        await SelectCadPadsAsync(vm, ids);
        vm.HasUnsavedChanges = false;

        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.Equal("DXF combine: failed to build combined shape.", vm.StatusText);
        Assert.Equal(originals, vm.CadPads.OrderBy(static pad => pad.Id).ToArray());
        Assert.Equal(ids, vm.SelectedCadPadIds.Order().ToArray());
        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }
}
