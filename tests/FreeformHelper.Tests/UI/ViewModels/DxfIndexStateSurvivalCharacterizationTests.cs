// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfIndexStateSurvivalCharacterizationTests
{
    [AvaloniaFact]
    public async Task LayerChangeAndHideUndo_KeepOverrideAndAnchorThroughProjectRoundTripAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var overrideId = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha").Id;
        var anchorId = Assert.Single(vm.CadPads, static pad => pad.Layer == "beta").Id;
        vm.SetCadOutputFwDiffIndexOverride(overrideId, 37);
        vm.SetCadOutputFwDiffIndexAnchorCadPad(anchorId);
        AssertIndexChoices(vm, overrideId, anchorId);

        var anchorLayer = Assert.Single(vm.LayerToggles, static layer => layer.Name == "beta");
        anchorLayer.IsSelected = false;
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(37, vm.GetCadOutputFwDiffIndexOverride(overrideId));
        Assert.Empty(vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas);
        Assert.Equal((decimal)anchorId, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        AssertSavedChoices(files, overrideId, anchorId);
        anchorLayer.IsSelected = true;
        await WaitForGridRebuildAsync(vm);
        AssertIndexChoices(vm, overrideId, anchorId);

        await SelectCadPadsAsync(vm, [overrideId]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == overrideId);
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(0, vm.DeletedCadPadCount);
        AssertIndexChoices(vm, overrideId, anchorId);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        AssertSavedChoices(files, overrideId, anchorId);

        var loaded = files.CreateViewModel();
        await loaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(loaded);

        AssertIndexChoices(loaded, overrideId, anchorId);
        Assert.Equal(0, loaded.DeletedCadPadCount);
        Assert.Equal(2, loaded.CadPads.Count);
        Assert.False(loaded.CanUndo);
    }

    private static void AssertIndexChoices(FreeformHelperViewModel vm, int overrideId, int anchorId)
    {
        Assert.Equal(37, vm.GetCadOutputFwDiffIndexOverride(overrideId));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(overrideId, out var assigned));
        Assert.Equal(37, assigned);
        Assert.Equal(overrideId, Assert.Single(vm.CadOutputFwDiffIndexOverrideCadIds));
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(anchorId));
        Assert.Equal(anchorId, Assert.Single(vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas));
        Assert.Equal((decimal)anchorId, vm.CadOutputFwDiffIndexAnchorCadId);
    }

    private static void AssertSavedChoices(LoadingModeTestScope files, int overrideId, int anchorId)
    {
        var project = JsonProjectStore.Load(files.PathFor("project.json"));
        var manual = Assert.Single(project.CadOutputFwDiffIndexOverrides);
        Assert.Equal(overrideId, manual.Key);
        Assert.Equal(37, manual.Value);
        var anchor = Assert.Single(project.CadOutputFwDiffIndexAnchorCadPadByIc);
        Assert.Equal(0, anchor.Key);
        Assert.Equal(anchorId, anchor.Value);
    }
}
