// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Current behaviour: a successful load replaces unsaved CAD edits and repeated loads replace their collections.</summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadReplacementCharacterizationTests
{
    [AvaloniaFact]
    public async Task LoadCommand_CurrentBehavior_AnotherProjectDiscardsUnsavedHiddenPadWithoutSavingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var saved = await files.CreateSavedCadAsync("replacement.json");
        var expectedPad = Assert.Single(saved.CadPads);
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        var savePickerCalls = 0;
        vm.PickSaveProjectPathAsync = () =>
        {
            savePickerCalls++;
            return Task.FromResult<string?>(null);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var loadedPad = Assert.Single(vm.CadPads);
        Assert.Equal(expectedPad.Id, loadedPad.Id);
        Assert.Equal(expectedPad.Polygon.Vertices, loadedPad.Polygon.Vertices);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(0, savePickerCalls);
        Assert.Equal("Project loaded: Step 1 match restored.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task LoadFromPath_CurrentBehavior_ImmediateSameProjectReloadDoesNotAccumulateStateAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("saved.json");
        var vm = files.CreateViewModel();
        var path = files.PathFor("saved.json");
        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
        var cadIds = vm.CadPads.Select(static pad => pad.Id).ToArray();
        var regularIds = vm.RegularPads.Select(static pad => pad.Index).ToArray();
        var layers = vm.LayerToggles.Select(static toggle => (toggle.Name, toggle.IsSelected)).ToArray();
        var links = vm.MatchLinksForCanvas.ToArray();
        Assert.Single(cadIds);
        Assert.Equal(2, regularIds.Length);
        Assert.Single(layers);
        Assert.NotEmpty(links);

        var result = await vm.LoadProjectFromPathAsync(path);

        Assert.True(result.IsLoaded);
        Assert.Equal(cadIds, vm.CadPads.Select(static pad => pad.Id));
        Assert.Equal(regularIds, vm.RegularPads.Select(static pad => pad.Index));
        Assert.Equal(layers, vm.LayerToggles.Select(static toggle => (toggle.Name, toggle.IsSelected)));
        Assert.Equal(links, vm.MatchLinksForCanvas);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        Assert.Equal("Project loaded: Step 1 match restored.", vm.StatusText);
    }
}
