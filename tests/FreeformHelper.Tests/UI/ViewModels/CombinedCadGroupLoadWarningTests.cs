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
    public async Task SkippedGroupWarnsAndLeavesProjectUnsavedButSavable(string damage)
    {
        using var files = new LoadingModeTestScope();
        await SaveGroupsAsync(files, damage);
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.Contains("Warning:", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("101", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("Saving drops these groups", vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(await vm.SaveProjectAsync());
        Assert.False(vm.HasUnsavedChanges);
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
        Assert.DoesNotContain("Saving drops", vm.StatusText, StringComparison.Ordinal);
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
        Assert.Single(messages, static message => message.Contains("Saving drops", StringComparison.Ordinal));
        Assert.Contains("101, 102, 103, 104, 105", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("and 2 more", vm.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("106", vm.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("107", vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.HasUnsavedChanges);
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
