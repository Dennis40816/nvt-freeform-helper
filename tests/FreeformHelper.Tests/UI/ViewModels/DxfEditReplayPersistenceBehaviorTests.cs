// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditReplayPersistenceBehaviorTests
{
    [AvaloniaFact]
    public async Task LoadProject_PartiallyConflictingGroup_ReleasesOutputIdsForTheNextGroupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        var source = ReadSource(project);
        var outputId = source.Id + 100;
        var rejected = CreateGroup(source, 101, outputId, "rejected");
        rejected.OutputPads.Add(CreateOutput(source, source.Id, "conflict"));
        project.DxfCombinedCadGroups = [rejected, CreateGroup(source, 102, outputId, "recovered")];
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        var recovered = Assert.Single(vm.CadPads);
        Assert.Equal(outputId, recovered.Id);
        Assert.Equal("recovered", recovered.Name);
        Assert.Equal(source.Polygon.Vertices, recovered.Polygon.Vertices);
        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.Contains("could not be restored: 101", vm.StatusText, StringComparison.Ordinal);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var saved = JsonProjectStore.Load(path).DxfCombinedCadGroups;
        Assert.Equal(2, saved.Count);
        Assert.Equal(2, Assert.Single(saved, group => group.GroupId == 101).OutputPads.Count);
        Assert.Equal("recovered", Assert.Single(Assert.Single(saved, group => group.GroupId == 102).OutputPads).Name);
        var reloaded = files.CreateViewModel();
        await reloaded.LoadProjectCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(reloaded);
        Assert.Equal(outputId, Assert.Single(reloaded.CadPads).Id);
        Assert.Equal(1, reloaded.CombinedCadPadCount);
    }

    [AvaloniaFact]
    public async Task SaveProject_ReplayedAndSkippedGroupsShareAnId_PreservesBothWithDistinctIdsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        var source = ReadSource(project);
        var kept = CreateGroup(source, 7, source.Id + 200, "unrestored");
        kept.SourceCadIds = [source.Id + 1000];
        project.DxfCombinedCadGroups = [CreateGroup(source, 7, source.Id + 100, "restored"), kept];
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();
        await vm.LoadProjectCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.Equal("restored", Assert.Single(vm.CadPads).Name);
        Assert.Contains("could not be restored: 7", vm.StatusText, StringComparison.Ordinal);

        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);

        var saved = JsonProjectStore.Load(path).DxfCombinedCadGroups;
        Assert.Equal(2, saved.Count);
        Assert.Equal(2, saved.Select(group => group.GroupId).Distinct().Count());
        Assert.Equal("restored", Assert.Single(Assert.Single(saved, group => group.GroupId == 7).OutputPads).Name);
        var renumbered = Assert.Single(saved, group => group.GroupId != 7);
        Assert.True(renumbered.GroupId > 0);
        Assert.Equal(kept.SourceCadIds, renumbered.SourceCadIds);
        var output = Assert.Single(renumbered.OutputPads);
        Assert.Equal(kept.OutputPads[0].Id, output.Id);
        Assert.Equal("unrestored", output.Name);
        Assert.Equal(kept.OutputPads[0].Vertices, output.Vertices);
        var reloaded = files.CreateViewModel();
        await reloaded.LoadProjectCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(reloaded);
        Assert.Equal("restored", Assert.Single(reloaded.CadPads).Name);
        Assert.Contains($"could not be restored: {renumbered.GroupId}", reloaded.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task LoadProject_CaseOnlyAndMissingLayerOverrides_AreNotSavedAsEditsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        var source = ReadSource(project);
        project.DxfCadLayerOverrides = new() { [source.Id] = "SIGNAL", [source.Id + 999] = "orphan" };
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Equal(source.Layer, Assert.Single(vm.CadPads).Layer);
        Assert.Equal(0, vm.RelayeredCadPadCount);
        Assert.False(vm.HasAnyCadEdits);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        Assert.Empty(JsonProjectStore.Load(path).DxfCadLayerOverrides);
    }

    private static CadPad ReadSource(ProjectFile project) =>
        Assert.Single(new DxfImportService().ImportFromPath(project.LastDxfPath!, new DxfImportOptions()).Pads);

    private static ProjectDxfCombinedCadGroup CreateGroup(CadPad source, int groupId, int outputId, string name) => new()
    {
        GroupId = groupId,
        SourceCadIds = [source.Id],
        OutputPads = [CreateOutput(source, outputId, name)],
    };

    private static ProjectCadPadSnapshot CreateOutput(CadPad source, int id, string name) => new()
    {
        Id = id,
        Name = name,
        Layer = source.Layer,
        Vertices = [.. source.Polygon.Vertices],
    };
}
