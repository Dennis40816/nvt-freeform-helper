// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerSelectionBatchEffectsBehaviorTests
{
    [AvaloniaFact]
    public async Task DeselectAllLayers_DefersFilteringUntilTheBatchEndsAndOneUndoRestoresEveryLayerAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        Assert.True(vm.RecalcBoundsOnLayerFilter);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.False(vm.CanUndo);
        var originalIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        var originalGrid = vm.RegularPads;
        var padCountsDuringChanges = new List<int>();
        foreach (var toggle in vm.LayerToggles)
        {
            toggle.PropertyChanged += (_, _) => padCountsDuringChanges.Add(vm.CadPads.Count);
        }

        vm.DeselectAllLayers();
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(vm.LayerToggles.Count, padCountsDuringChanges.Count);
        Assert.All(padCountsDuringChanges, count => Assert.Equal(originalIds.Length, count));
        Assert.All(vm.LayerToggles, static toggle => Assert.False(toggle.IsSelected));
        Assert.Empty(vm.CadPads);
        Assert.NotSame(originalGrid, vm.RegularPads);
        Assert.Equal(9, vm.RegularPads.Count);
        Assert.Empty(vm.MatchLinksForCanvas);
        Assert.All(vm.RegularPads, static pad => Assert.Null(pad.MatchedCadPadId));
        Assert.False(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.True(vm.CanUndo);

        vm.Undo();
        await vm.WaitForGridRebuildIdleAsync();

        Assert.All(vm.LayerToggles, static toggle => Assert.True(toggle.IsSelected));
        Assert.Equal(originalIds, vm.CadPads.Select(static pad => pad.Id).Order());
        Assert.Empty(vm.MatchLinksForCanvas);
        Assert.False(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task ApplyLayerSelection_SingleToggleInvalidatesMatchesAndOneUndoRestoresVisibilityAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        Assert.True(vm.RecalcBoundsOnLayerFilter);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.False(vm.CanUndo);
        var originalIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        var expectedIds = vm.CadPads.Where(static pad => pad.Layer != "signal-b")
            .Select(static pad => pad.Id).Order().ToArray();
        var originalGrid = vm.RegularPads;
        var target = vm.LayerToggles.Single(static toggle => toggle.Name == "signal-b");
        var padCountsDuringChanges = new List<int>();
        target.PropertyChanged += (_, _) => padCountsDuringChanges.Add(vm.CadPads.Count);

        vm.ApplyLayerSelection(target, soloSelect: false);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(originalIds.Length, Assert.Single(padCountsDuringChanges));
        Assert.False(target.IsSelected);
        Assert.All(vm.LayerToggles.Where(toggle => !ReferenceEquals(toggle, target)),
            static toggle => Assert.True(toggle.IsSelected));
        Assert.Equal(expectedIds, vm.CadPads.Select(static pad => pad.Id).Order());
        Assert.NotSame(originalGrid, vm.RegularPads);
        Assert.Equal(9, vm.RegularPads.Count);
        Assert.Empty(vm.MatchLinksForCanvas);
        Assert.All(vm.RegularPads, static pad => Assert.Null(pad.MatchedCadPadId));
        Assert.False(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.True(vm.CanUndo);

        vm.Undo();
        await vm.WaitForGridRebuildIdleAsync();

        Assert.All(vm.LayerToggles, static toggle => Assert.True(toggle.IsSelected));
        Assert.Equal(originalIds, vm.CadPads.Select(static pad => pad.Id).Order());
        Assert.Empty(vm.MatchLinksForCanvas);
        Assert.False(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.False(vm.CanUndo);
    }
}
