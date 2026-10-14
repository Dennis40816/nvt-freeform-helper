// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what a successful project save does with a setting that changes while the save is still
/// waiting for its destination path (issue #64, inventory finding SETTINGS-D03). The save
/// captures the settings first and clears the unsaved flag last, so the later edit is not in the
/// file and the flag still reads clean. This test describes the current result, not the wanted
/// one. A fix that clears the flag only for the revision it saved changes the expected values
/// here and must say so in its pull request.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectSaveLaterEditCharacterizationTests
{
    private const decimal EditedThreshold = 0.43m;

    [AvaloniaFact]
    public async Task SaveProject_SettingChangesWhileSavePending_ClearsTheUnsavedFlag()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        using var gate = new SemaphoreSlim(0, 1);
        var path = files.PathFor("project.json");
        vm.PickSaveProjectPathAsync = () => WaitForGateAsync(gate, path);
        var defaultThreshold = vm.MatchThreshold;
        Assert.NotEqual(EditedThreshold, defaultThreshold);
        Assert.False(vm.HasUnsavedChanges);

        var save = vm.SaveProjectAsync();
        try
        {
            vm.MatchThreshold = EditedThreshold;
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            gate.Release();
        }

        var saved = await save;

        Assert.True(saved);
        Assert.Equal(EditedThreshold, vm.MatchThreshold);
        // The edit came after the settings were captured, but the save still reports a clean state.
        Assert.False(vm.HasUnsavedChanges);
        var reloaded = files.CreateViewModel();
        var result = await reloaded.LoadProjectFromPathAsync(path);
        Assert.True(result.IsLoaded);
        Assert.Equal(defaultThreshold, reloaded.MatchThreshold);
    }

    private static async Task<string?> WaitForGateAsync(SemaphoreSlim gate, string path)
    {
        await gate.WaitAsync();
        return path;
    }
}
