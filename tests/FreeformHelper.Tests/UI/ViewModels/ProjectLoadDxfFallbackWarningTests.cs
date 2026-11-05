// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadDxfFallbackWarningTests
{
    [AvaloniaFact]
    public async Task LoadProject_DxfPathMissing_UsesTheEmbeddedCopyAndWarnsInTheStatusBar()
    {
        using var files = new LoadingModeTestScope();
        var saver = files.CreateViewModel();
        saver.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        await saver.OpenDxfCommand.ExecuteAsync(null);
        Assert.True(await saver.SaveProjectAsync());
        var path = files.PathFor("project.json");
        var dxfPath = JsonProjectStore.Load(path).LastDxfPath;
        Assert.False(string.IsNullOrWhiteSpace(dxfPath));
        File.Delete(dxfPath);
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.NotEmpty(vm.CadPads);
        Assert.StartsWith("Warning: The DXF file was not found:", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains(dxfPath, vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("Using the DXF copy stored in the project.", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task LoadProject_FallbackAndSkippedGroupTogether_ShowsBothWarningsInOneStatusMessage()
    {
        using var files = new LoadingModeTestScope();
        var saver = files.CreateViewModel();
        saver.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        await saver.OpenDxfCommand.ExecuteAsync(null);
        Assert.True(await saver.SaveProjectAsync());
        var path = files.PathFor("project.json");
        var project = JsonProjectStore.Load(path);
        var dxfPath = project.LastDxfPath!;
        var source = new DxfImportService().ImportFromPath(dxfPath, new DxfImportOptions()).Pads[0];
        project.DxfCombinedCadGroups =
        [
            new ProjectDxfCombinedCadGroup
            {
                GroupId = 101,
                SourceCadIds = [source.Id],
                OutputPads =
                [
                    new ProjectCadPadSnapshot
                    {
                        Id = source.Id + 101,
                        Name = "101",
                        Layer = source.Layer,
                        Vertices = [new Point2(0, 0), new Point2(10, 0)]
                    }
                ]
            }
        ];
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        File.Delete(dxfPath);
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.StartsWith("Warning: The DXF file was not found:", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("Using the DXF copy stored in the project.", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("Combined DXF groups could not be restored: 101", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("They stay in the project file.", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task LoadProject_FallbackThenReadablePathOnTheSameViewModel_ClearsTheFallbackWarning()
    {
        using var files = new LoadingModeTestScope();
        var saver = files.CreateViewModel();
        saver.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        await saver.OpenDxfCommand.ExecuteAsync(null);
        Assert.True(await saver.SaveProjectAsync());
        var path = files.PathFor("project.json");
        var dxfPath = JsonProjectStore.Load(path).LastDxfPath!;
        var dxfBytes = TestFiles.ReadBytes(dxfPath);
        File.Delete(dxfPath);
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        Assert.Contains("Using the DXF copy stored in the project.", vm.StatusText, StringComparison.Ordinal);
        TestFiles.WriteBytes(dxfPath, dxfBytes);

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.DoesNotContain("DXF copy stored in the project", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task LoadProject_DxfPathReadable_ShowsNoFallbackWarning()
    {
        using var files = new LoadingModeTestScope();
        var saver = files.CreateViewModel();
        saver.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        await saver.OpenDxfCommand.ExecuteAsync(null);
        Assert.True(await saver.SaveProjectAsync());
        var vm = files.CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));

        Assert.True(result.IsLoaded);
        Assert.NotEmpty(vm.CadPads);
        Assert.DoesNotContain("DXF copy stored in the project", vm.StatusText, StringComparison.Ordinal);
    }
}
