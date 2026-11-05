// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Project;
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
