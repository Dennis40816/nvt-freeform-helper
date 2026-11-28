// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectPathResolutionCharacterizationTests
{
    private const decimal SavedThreshold = 0.27m;

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task LoadFromPathBlankPathReturnsInvalidArgumentsAndShowsTheRequiredArgumentAsync(string? path)
    {
        using var files = new ProjectLoadCharacterizationScope();
        var vm = files.CreateViewModel();
        vm.StatusText = "before load";

        var result = await vm.LoadProjectFromPathAsync(path!);

        AssertRejected(vm, result, "INVALID_ARGUMENTS", "Argument '--path' is required.", null);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task LoadFromPathInvalidCharacterReturnsInvalidArgumentsWithTheOriginalPathAndShowsItAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        var vm = files.CreateViewModel();
        vm.StatusText = "before load";
        const string path = "  bad\0path.json  ";
        var error = Assert.Throws<ArgumentException>(() => Path.GetFullPath(path.Trim()));

        var result = await vm.LoadProjectFromPathAsync(path);

        AssertRejected(vm, result, "INVALID_ARGUMENTS", $"Invalid project path '{path}': {error.Message}", null);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task LoadFromPathRelativePathUsesTheCurrentDirectoryAndReturnsTheLoadedFullPathAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        var fullPath = await SaveProjectAsync(files);
        using var directory = new WorkingDirectoryScope(Path.GetDirectoryName(fullPath)!);
        var vm = files.CreateViewModel();
        vm.StatusText = "before load";

        var result = await vm.LoadProjectFromPathAsync("project.json");

        AssertLoaded(vm, result, fullPath);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task LoadFromPathSurroundingSpacesAreTrimmedBeforeLoadingAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        var fullPath = await SaveProjectAsync(files);
        var vm = files.CreateViewModel();
        vm.StatusText = "before load";

        var result = await vm.LoadProjectFromPathAsync($"  {fullPath}  ");

        AssertLoaded(vm, result, fullPath);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task LoadFromPathMissingFileReturnsAndShowsTheNormalizedFullPathAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        var vm = files.CreateViewModel();
        vm.StatusText = "before load";
        var path = files.PathFor(Path.Combine(".", "missing.json"));
        var fullPath = Path.GetFullPath(path);

        var result = await vm.LoadProjectFromPathAsync(path);

        AssertRejected(vm, result, "PATH_NOT_FOUND", $"Project file not found: {fullPath}", fullPath);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task LoadFromPathExistingFileLoadsTheSavedProjectAndReportsTheFullPathAsync()
    {
        using var files = new ProjectLoadCharacterizationScope();
        var fullPath = await SaveProjectAsync(files);
        var vm = files.CreateViewModel();
        vm.StatusText = "before load";

        var result = await vm.LoadProjectFromPathAsync(fullPath);

        AssertLoaded(vm, result, fullPath);
    }

    private static async Task<string> SaveProjectAsync(ProjectLoadCharacterizationScope files)
    {
        var vm = files.CreateViewModel();
        vm.MatchThreshold = SavedThreshold;
        vm.AutoReplayStep2AfterProjectLoad = false;
        await vm.WaitForGridRebuildIdleAsync();
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        return Path.GetFullPath(files.PathFor("project.json"));
    }

    private static void AssertRejected(
        FreeformHelperViewModel vm, ProjectLoadCommandResult result, string code, string message, string? path)
    {
        Assert.False(result.IsLoaded);
        Assert.Equal(code, result.Code);
        Assert.Equal(message, result.Message);
        Assert.Equal(path, result.Path);
        Assert.Equal(message, vm.StatusText);
    }

    private static void AssertLoaded(FreeformHelperViewModel vm, ProjectLoadCommandResult result, string fullPath)
    {
        Assert.True(result.IsLoaded);
        Assert.Equal("OK", result.Code);
        Assert.Equal($"Project loaded: {fullPath}", result.Message);
        Assert.Equal(fullPath, result.Path);
        Assert.Equal("Project loaded: no CAD output pads; Step 1 auto replay skipped.", vm.StatusText);
        Assert.Equal(SavedThreshold, vm.MatchThreshold);
    }

    private sealed class WorkingDirectoryScope : IDisposable
    {
        private readonly string _previousDirectory = Directory.GetCurrentDirectory();

        public WorkingDirectoryScope(string directory)
        {
            Directory.SetCurrentDirectory(directory);
        }

        public void Dispose() => Directory.SetCurrentDirectory(_previousDirectory);
    }
}
