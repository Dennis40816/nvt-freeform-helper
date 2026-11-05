// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadRejectedPathStatusTests
{
    private const string StatusBeforeLoad = "before load";

    [AvaloniaFact]
    public async Task LoadProjectCommand_PickedFileDoesNotExist_ShowsTheMissingPath()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel(files.PathFor("vanishing.json"));
        vm.StatusText = StatusBeforeLoad;

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.StartsWith("Project file not found:", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("vanishing.json", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
    }

    [AvaloniaFact]
    public async Task LoadProjectFromPath_MissingFile_ReturnsPathNotFoundAndShowsIt()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        vm.StatusText = StatusBeforeLoad;

        var result = await vm.LoadProjectFromPathAsync(files.UnreadableProjectPath(invalidContent: false));

        Assert.False(result.IsLoaded);
        Assert.Equal("PATH_NOT_FOUND", result.Code);
        Assert.Equal(result.Message, vm.StatusText);
        Assert.StartsWith("Project file not found:", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData("", "Argument '--path' is required.")]
    [InlineData("bad\0path.json", "Invalid project path")]
    public async Task LoadProjectFromPath_UnusablePath_ReturnsInvalidArgumentsAndShowsIt(string path, string expectedText)
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        vm.StatusText = StatusBeforeLoad;

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.False(result.IsLoaded);
        Assert.Equal("INVALID_ARGUMENTS", result.Code);
        Assert.Contains(expectedText, vm.StatusText, StringComparison.Ordinal);
    }
}
