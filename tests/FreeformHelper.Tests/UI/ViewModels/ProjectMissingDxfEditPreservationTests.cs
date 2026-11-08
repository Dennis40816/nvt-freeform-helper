// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Infrastructure.Project;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectMissingDxfEditPreservationTests
{
    [AvaloniaFact]
    public async Task MissingDxfKeepsEverySavedEditUntilACadSourceIsOpenedAsync()
    {
        using var files = new LoadingModeTestScope();
        var project = CreateProjectWithDxfEdits(files.PathFor("missing.dxf"));
        var path = files.PathFor("project.json");
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();

        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(vm.CadPads);
        Assert.StartsWith("Warning: The DXF file was not found:", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains(project.LastDxfPath!, vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("The saved DXF edits are kept", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("Combined DXF groups could not be restored: 101", vm.StatusText, StringComparison.Ordinal);
        vm.ApplySizingToCells([0], [0], 6m, 7m);
        await WaitForGridRebuildAsync(vm);
        vm.FilterCadPadsByLayer();
        await vm.SaveProjectCommand.ExecuteAsync(null);
        var saved = JsonProjectStore.Load(path);
        AssertDxfEditsEqual(project, saved);
        var resized = Assert.Single(vm.RegularPads, static pad => pad.Row == 0 && pad.Col == 0);
        Assert.Equal(6, resized.Bounds.Width, 6);
        Assert.Equal(7, resized.Bounds.Height, 6);
        var reloaded = files.CreateViewModel();
        Assert.True((await reloaded.LoadProjectFromPathAsync(path)).IsLoaded);
        await WaitForGridRebuildAsync(reloaded);
        var restoredSize = Assert.Single(reloaded.RegularPads, static pad => pad.Row == 0 && pad.Col == 0);
        Assert.Equal(resized.Bounds, restoredSize.Bounds);

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        var cadId = Assert.Single(vm.CadPads).Id;
        await SelectCadPadsAsync(vm, [cadId]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var withCad = JsonProjectStore.Load(path);
        Assert.Equal(cadId, Assert.Single(withCad.HiddenCadPadIds));
        Assert.Empty(withCad.VisibleDuplicateCadPadIds);
        Assert.Empty(withCad.DxfCadLayerOverrides);
        Assert.Empty(withCad.DxfCadGeometryOverrides);
    }

    private static ProjectFile CreateProjectWithDxfEdits(string missingPath)
    {
        var project = new ProjectFile
        {
            LastDxfPath = missingPath,
            HiddenCadPadIds = [17],
            VisibleDuplicateCadPadIds = [29],
            DxfCadLayerOverrides = new() { [23] = "manual" },
            DxfCadGeometryOverrides = new()
            {
                [23] = new ProjectCadGeometrySnapshot { Vertices = [new(0, 0), new(2, 1), new(1, 3)] }
            },
            DxfCombinedCadGroups =
            [
                new ProjectDxfCombinedCadGroup
                {
                    GroupId = 101,
                    SourceCadIds = [31, 32],
                    OutputPads =
                    [
                        new ProjectCadPadSnapshot
                        {
                            Id = 77, Name = "combined", Layer = "signal",
                            Vertices = [new Point2(0, 0), new Point2(3, 0), new Point2(0, 3)]
                        }
                    ]
                }
            ],
            CadPadCustomValues = new() { [23] = 2.5 },
            DxfRegularMappingOverrides = new() { [23] = 0 },
            CadOutputFwDiffIndexOverrides = new() { [23] = 37 },
            CadOutputFwDiffIndexAnchorCadPadId = 23,
            CadOutputFwDiffIndexAnchorCadPadByIc = new() { [0] = 23 }
        };
        project.Settings.Grid.XChannels = 2;
        project.Settings.Grid.YChannels = 2;
        project.Settings.Grid.ActiveAreaWidth = 40;
        project.Settings.Grid.ActiveAreaHeight = 20;
        project.Settings.Grid.AlignmentMode = GridAlignmentMode.FromPanelAa;
        project.Settings.Grid.BoundsPaddingRatio = 0;
        project.Settings.View.AutoReplayStep2AfterProjectLoad = false;
        return project;
    }

    private static void AssertDxfEditsEqual(ProjectFile expected, ProjectFile actual)
    {
        Assert.Equal(expected.LastDxfPath, actual.LastDxfPath);
        Assert.True(expected.HiddenCadPadIds.SetEquals(actual.HiddenCadPadIds));
        Assert.True(expected.VisibleDuplicateCadPadIds.SetEquals(actual.VisibleDuplicateCadPadIds));
        Assert.Equal(expected.DxfCadLayerOverrides, actual.DxfCadLayerOverrides);
        var geometry = Assert.Single(actual.DxfCadGeometryOverrides);
        Assert.Equal(23, geometry.Key);
        Assert.Equal(expected.DxfCadGeometryOverrides[23].Vertices, geometry.Value.Vertices);
        var expectedGroup = Assert.Single(expected.DxfCombinedCadGroups);
        var actualGroup = Assert.Single(actual.DxfCombinedCadGroups);
        Assert.Equal(expectedGroup.GroupId, actualGroup.GroupId);
        Assert.Equal(expectedGroup.SourceCadIds, actualGroup.SourceCadIds);
        var expectedOutput = Assert.Single(expectedGroup.OutputPads);
        var actualOutput = Assert.Single(actualGroup.OutputPads);
        Assert.Equal(expectedOutput.Id, actualOutput.Id);
        Assert.Equal(expectedOutput.Name, actualOutput.Name);
        Assert.Equal(expectedOutput.Layer, actualOutput.Layer);
        Assert.Equal(expectedOutput.Vertices, actualOutput.Vertices);
        Assert.Equal(expected.CadPadCustomValues, actual.CadPadCustomValues);
        Assert.Equal(expected.DxfRegularMappingOverrides, actual.DxfRegularMappingOverrides);
        Assert.Equal(expected.CadOutputFwDiffIndexOverrides, actual.CadOutputFwDiffIndexOverrides);
        Assert.Equal(expected.CadOutputFwDiffIndexAnchorCadPadId, actual.CadOutputFwDiffIndexAnchorCadPadId);
        Assert.Equal(expected.CadOutputFwDiffIndexAnchorCadPadByIc, actual.CadOutputFwDiffIndexAnchorCadPadByIc);
    }
}
