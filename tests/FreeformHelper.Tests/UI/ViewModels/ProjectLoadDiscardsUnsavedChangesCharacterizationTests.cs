// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what loading another project does with the unsaved changes of the current project
/// (issue #64, inventory finding SHELL-D02). The load only asks the pad editor whether pending
/// pad edits can proceed. It does not ask what to do with the unsaved settings of the current
/// project, so the load replaces them and the unsaved flag reads clean. Both entry points are
/// pinned: the menu command (<c>LoadProjectCommand</c>, after the file picker) and the load by
/// path (the runtime query entry). The assertions describe the end state: the load went through,
/// the value of the file replaced the unsaved value and the flag reads clean. These tests
/// describe the current result, not the wanted one. A fix that adds a project-departure decision
/// before the load changes the expected values here and must say so in its pull request.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadDiscardsUnsavedChangesCharacterizationTests
{
    private const decimal SavedThreshold = 0.27m;
    private const decimal UnsavedThreshold = 0.61m;

    [AvaloniaFact]
    public async Task LoadFromPath_CurrentProjectHasUnsavedChanges_ReplacesThemWithoutAsking()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.MatchThreshold = SavedThreshold);
        var path = files.PathFor("project.json");
        var vm = files.CreateViewModel();
        vm.MatchThreshold = UnsavedThreshold;
        Assert.True(vm.HasUnsavedChanges);

        var result = await vm.LoadProjectFromPathAsync(path);

        // The load goes through and nothing asked what to do with the unsaved value.
        Assert.True(result.IsLoaded);
        Assert.Equal(SavedThreshold, vm.MatchThreshold);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_CurrentProjectHasUnsavedChanges_ReplacesThemWithoutAsking()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.MatchThreshold = SavedThreshold);
        var vm = files.CreateViewModel();
        vm.MatchThreshold = UnsavedThreshold;
        Assert.True(vm.HasUnsavedChanges);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        // The command picked the file and loaded it. Nothing asked about the unsaved value.
        Assert.Equal(SavedThreshold, vm.MatchThreshold);
        Assert.False(vm.HasUnsavedChanges);
    }
}
