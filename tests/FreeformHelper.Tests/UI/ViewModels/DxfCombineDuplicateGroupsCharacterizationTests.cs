// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfCombineDuplicateGroupsCharacterizationTests
{
    [AvaloniaFact]
    public async Task CombineDisconnectedDuplicateDiffPads_RefreshesGroupsOnRematchAndUndoRestoresSourcesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("alpha", 0, 0, 5, 10), ("alpha", 10, 0, 15, 10), ("beta", 30, 0, 40, 20));
        await vm.MatchCommand.ExecuteAsync(null);
        var before = Assert.Single(vm.Step4DuplicateDiffGroups);
        var sourceIds = vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id).Order().ToArray();
        Assert.Equal(2, sourceIds.Length);
        Assert.Equal(sourceIds, before.CadPadIds.Order());
        Assert.True(vm.HasStep4DuplicateDiffGroups);
        await SelectCadPadsAsync(vm, sourceIds);

        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        var outputIds = vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id).Order().ToArray();
        Assert.Equal(2, outputIds.Length);
        Assert.DoesNotContain(outputIds, sourceIds.Contains);
        Assert.Equal(2, vm.CombinedCadPadCount);
        Assert.Empty(vm.Step4DuplicateDiffGroups);
        Assert.False(vm.HasStep4DuplicateDiffGroups);
        await vm.MatchCommand.ExecuteAsync(null);
        var combined = Assert.Single(vm.Step4DuplicateDiffGroups);
        Assert.Equal((before.IcIndex, before.DiffIndex), (combined.IcIndex, combined.DiffIndex));
        Assert.Equal(outputIds, combined.CadPadIds.Order());
        Assert.True(vm.HasStep4DuplicateDiffGroups);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Equal(sourceIds, vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id).Order());
        Assert.Empty(vm.Step4DuplicateDiffGroups);
        Assert.False(vm.HasStep4DuplicateDiffGroups);
        await vm.MatchCommand.ExecuteAsync(null);
        var restored = Assert.Single(vm.Step4DuplicateDiffGroups);
        Assert.Equal((before.IcIndex, before.DiffIndex), (restored.IcIndex, restored.DiffIndex));
        Assert.Equal(sourceIds, restored.CadPadIds.Order());
        Assert.True(vm.HasStep4DuplicateDiffGroups);
    }
}
