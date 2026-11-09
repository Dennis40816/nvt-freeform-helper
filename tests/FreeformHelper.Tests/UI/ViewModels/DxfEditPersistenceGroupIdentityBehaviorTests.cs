// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditPersistenceGroupIdentityBehaviorTests
{
    [AvaloniaFact]
    public async Task LoadProject_UnnumberedGroup_AcquiresPositiveIdAndSurvivesReloadAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        var source = ReadSource(project);
        project.DxfCombinedCadGroups = [CreateGroup(source, 0, source.Id + 100, "combined")];
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();

        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.Equal(source.Id + 100, Assert.Single(vm.CadPads).Id);
        Assert.Equal("combined", vm.CadPads[0].Name);
        Assert.DoesNotContain("could not be restored", vm.StatusText, StringComparison.Ordinal);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var saved = Assert.Single(JsonProjectStore.Load(path).DxfCombinedCadGroups);
        Assert.True(saved.GroupId > 0);
        Assert.Equal(source.Id, Assert.Single(saved.SourceCadIds));
        Assert.Equal(source.Id + 100, Assert.Single(saved.OutputPads).Id);
        var reloaded = files.CreateViewModel();
        Assert.True((await reloaded.LoadProjectFromPathAsync(path)).IsLoaded);
        await WaitForGridRebuildAsync(reloaded);
        Assert.Equal(1, reloaded.CombinedCadPadCount);
        Assert.Equal("combined", Assert.Single(reloaded.CadPads).Name);
    }

    [AvaloniaFact]
    public async Task LoadProject_NestedGroupsStoredOutOfOrder_ReplaysSourcesBeforeDependentsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        var source = ReadSource(project);
        var inner = CreateGroup(source, 10, source.Id + 100, "inner");
        var outer = CreateGroup(source, 20, source.Id + 200, "outer");
        outer.SourceCadIds = [source.Id + 100];
        project.DxfCombinedCadGroups = [outer, inner];
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();

        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        await WaitForGridRebuildAsync(vm);

        AssertNestedOutput(vm, source);
        Assert.DoesNotContain("could not be restored", vm.StatusText, StringComparison.Ordinal);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var saved = JsonProjectStore.Load(path).DxfCombinedCadGroups;
        Assert.Equal(2, saved.Count);
        var savedInner = Assert.Single(saved, static group => group.GroupId == 10);
        var savedOuter = Assert.Single(saved, static group => group.GroupId == 20);
        Assert.Equal(source.Id, Assert.Single(savedInner.SourceCadIds));
        Assert.Equal(Assert.Single(savedInner.OutputPads).Id, Assert.Single(savedOuter.SourceCadIds));
        Assert.Equal(source.Id + 200, Assert.Single(savedOuter.OutputPads).Id);
        var reloaded = files.CreateViewModel();
        Assert.True((await reloaded.LoadProjectFromPathAsync(path)).IsLoaded);
        await WaitForGridRebuildAsync(reloaded);
        AssertNestedOutput(reloaded, source);
    }

    [AvaloniaFact]
    public async Task LoadProject_BlankLayerOverride_KeepsBaselineLayerAndIndependentGeometryEditAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        var source = ReadSource(project);
        var triangle = source.Polygon.Vertices.Take(3).ToList();
        project.DxfCadLayerOverrides = new() { [source.Id] = " \t " };
        project.DxfCadGeometryOverrides = new()
        {
            [source.Id] = new ProjectCadGeometrySnapshot { Vertices = triangle },
        };
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();

        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        await WaitForGridRebuildAsync(vm);

        var edited = Assert.Single(vm.CadPads);
        Assert.Equal(source.Layer, edited.Layer);
        Assert.Equal(triangle, edited.Polygon.Vertices);
        Assert.Equal(0, vm.RelayeredCadPadCount);
        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var saved = JsonProjectStore.Load(path);
        Assert.Empty(saved.DxfCadLayerOverrides);
        Assert.Equal(source.Id, Assert.Single(saved.DxfCadGeometryOverrides).Key);
        Assert.Equal(triangle, saved.DxfCadGeometryOverrides[source.Id].Vertices);
    }

    private static CadPad ReadSource(ProjectFile project) =>
        Assert.Single(new DxfImportService().ImportFromPath(project.LastDxfPath!, new DxfImportOptions()).Pads);

    private static ProjectDxfCombinedCadGroup CreateGroup(CadPad source, int groupId, int outputId, string name) => new()
    {
        GroupId = groupId,
        SourceCadIds = [source.Id],
        OutputPads =
        [
            new ProjectCadPadSnapshot
            {
                Id = outputId, Name = name, Layer = source.Layer, Vertices = [.. source.Polygon.Vertices],
            },
        ],
    };

    private static void AssertNestedOutput(FreeformHelperViewModel vm, CadPad source)
    {
        Assert.Equal(2, vm.CombinedCadPadCount);
        var visible = Assert.Single(vm.CadPads);
        Assert.Equal(source.Id + 200, visible.Id);
        Assert.Equal("outer", visible.Name);
        Assert.Equal(source.Polygon.Vertices, visible.Polygon.Vertices);
    }
}
