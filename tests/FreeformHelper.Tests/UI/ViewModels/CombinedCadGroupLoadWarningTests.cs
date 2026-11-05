// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CombinedCadGroupLoadWarningTests
{
    [AvaloniaTheory]
    [InlineData("invalid-output")]
    [InlineData("missing-source")]
    [InlineData("empty-sources")]
    [InlineData("empty-outputs")]
    [InlineData("conflicting-output")]
    public async Task SkippedGroupWarnsAndTheNextSaveKeepsIt(string damage)
    {
        using var files = new LoadingModeTestScope();
        await SaveGroupsAsync(files, damage);
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Contains("Warning:", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("101", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("stay in the project file", vm.StatusText, StringComparison.Ordinal);
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(await vm.SaveProjectAsync());
        Assert.Equal(101, Assert.Single(JsonProjectStore.Load(files.PathFor("project.json")).DxfCombinedCadGroups).GroupId);

        var reloaded = files.CreateViewModel();
        Assert.True((await reloaded.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        Assert.Contains("101", reloaded.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task MissingDxfWarnsAndTheNextSaveKeepsTheGroups()
    {
        using var files = new LoadingModeTestScope();
        await SaveGroupsAsync(files, "none", groupCount: 2);
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        project.LastDxfPath = files.PathFor("moved-away.dxf");
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();

        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);

        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Contains("101, 102", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("stay in the project file", vm.StatusText, StringComparison.Ordinal);
        Assert.True(await vm.SaveProjectAsync());
        var saved = JsonProjectStore.Load(path).DxfCombinedCadGroups;
        Assert.Equal([101, 102], saved.Select(static group => group.GroupId).Order().ToArray());
        Assert.All(saved, static group => Assert.Single(group.OutputPads));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CleanProjectLoadKeepsUnsavedChangesFalse(int groupCount)
    {
        using var files = new LoadingModeTestScope();
        await SaveGroupsAsync(files, "none", groupCount);
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.Equal(groupCount, vm.CombinedCadPadCount);
        Assert.DoesNotContain("could not be restored", vm.StatusText, StringComparison.Ordinal);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task SkippedGroupWarningListsFiveIdsAndTheRemainingCountOnce()
    {
        using var files = new LoadingModeTestScope();
        await SaveGroupsAsync(files, "invalid-output", groupCount: 7);
        var vm = files.CreateViewModel();
        var messages = new List<string>();
        vm.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(vm.StatusText)) messages.Add(vm.StatusText);
        };

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.Single(messages, static message => message.Contains("could not be restored", StringComparison.Ordinal));
        Assert.Contains("101, 102, 103, 104, 105", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("and 2 more", vm.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("106", vm.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("107", vm.StatusText, StringComparison.Ordinal);
    }

    private static async Task SaveGroupsAsync(LoadingModeTestScope files, string damage, int groupCount = 1)
    {
        await files.SaveWithCadAsync();
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        var source = new DxfImportService().ImportFromPath(project.LastDxfPath!, new DxfImportOptions()).Pads.Single();
        project.DxfCombinedCadGroups = Enumerable.Range(101, groupCount)
            .Select(id => CreateGroup(source, id, damage)).ToList();
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
    }

    private static ProjectDxfCombinedCadGroup CreateGroup(CadPad source, int groupId, string damage)
    {
        var outputId = source.Id + groupId;
        var output = new ProjectCadPadSnapshot
        {
            Id = damage == "conflicting-output" ? source.Id : outputId,
            Name = outputId.ToString(CultureInfo.InvariantCulture),
            Layer = source.Layer,
            Vertices = damage == "invalid-output" ? [new Point2(0, 0), new Point2(10, 0)] : [.. source.Polygon.Vertices]
        };
        return new ProjectDxfCombinedCadGroup
        {
            GroupId = groupId,
            SourceCadIds = damage switch
            {
                "missing-source" => [outputId + 1],
                "empty-sources" => [],
                _ => [source.Id]
            },
            OutputPads = damage == "empty-outputs" ? [] : [output]
        };
    }
}
