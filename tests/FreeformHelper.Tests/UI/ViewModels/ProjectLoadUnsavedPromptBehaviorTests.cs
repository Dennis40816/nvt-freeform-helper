// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Load Project backs up unsaved changes first, then asks to save, discard or cancel.</summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadUnsavedPromptBehaviorTests
{
    [AvaloniaFact]
    public async Task LoadCommand_UnsavedChanges_BacksUpBeforePromptAndCancelKeepsCurrentProjectAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        var expectedBackup = UnsavedProjectBackup.PathFor(files.AppStore.SettingsPath);
        var backupExistedAtPrompt = false;
        string? promptedBackup = null;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = backupPath =>
        {
            promptedBackup = backupPath;
            backupExistedAtPrompt = File.Exists(backupPath);
            return Task.FromResult<bool?>(null);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(expectedBackup, promptedBackup);
        Assert.True(backupExistedAtPrompt);
        Assert.True(File.Exists(expectedBackup));
        Assert.Empty(vm.CadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal("Load cancelled: unsaved changes were kept.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task LoadCommand_UnsavedChanges_DiscardLoadsTheOtherProjectAndKeepsTheBackupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var saved = await files.CreateSavedCadAsync("replacement.json");
        var expectedPad = Assert.Single(saved.CadPads);
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        var prompts = 0;
        var savePickerCalls = 0;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ =>
        {
            prompts++;
            return Task.FromResult<bool?>(false);
        };
        vm.PickSaveProjectPathAsync = () =>
        {
            savePickerCalls++;
            return Task.FromResult<string?>(null);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, prompts);
        Assert.Equal(0, savePickerCalls);
        Assert.Equal(expectedPad.Id, Assert.Single(vm.CadPads).Id);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(File.Exists(UnsavedProjectBackup.PathFor(files.AppStore.SettingsPath)));
    }

    [AvaloniaFact]
    public async Task LoadCommand_UnsavedChanges_SaveWritesTheProjectThenLoadsAndRemovesTheBackupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var saved = await files.CreateSavedCadAsync("replacement.json");
        var expectedPad = Assert.Single(saved.CadPads);
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        var chosenPath = files.PathFor("kept-edits.json");
        var savePickerCalls = 0;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ => Task.FromResult<bool?>(true);
        vm.PickSaveProjectPathAsync = () =>
        {
            savePickerCalls++;
            return Task.FromResult<string?>(chosenPath);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, savePickerCalls);
        Assert.True(File.Exists(chosenPath));
        Assert.Equal(expectedPad.Id, Assert.Single(vm.CadPads).Id);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(File.Exists(UnsavedProjectBackup.PathFor(files.AppStore.SettingsPath)));

        var reopened = files.CreateViewModel();
        Assert.True((await reopened.LoadProjectFromPathAsync(chosenPath)).IsLoaded);
        Assert.Empty(reopened.CadPads);
        Assert.Equal(1, reopened.DeletedCadPadCount);
    }

    [AvaloniaFact]
    public async Task LoadCommand_UnsavedChanges_SavePickerCancelledKeepsCurrentProjectAndBackupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ => Task.FromResult<bool?>(true);
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(null);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Empty(vm.CadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(File.Exists(UnsavedProjectBackup.PathFor(files.AppStore.SettingsPath)));
        Assert.Equal("Load cancelled: the project was not saved, so unsaved changes were kept.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task LoadCommand_SettingsChangedWithoutAnyProjectContent_LoadsWithoutPromptOrBackupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = files.CreateViewModel();
        vm.MatchThreshold = 0.61m;
        Assert.True(vm.HasUnsavedChanges);
        var prompts = 0;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ =>
        {
            prompts++;
            return Task.FromResult<bool?>(null);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(0, prompts);
        Assert.Single(vm.CadPads);
        Assert.False(File.Exists(UnsavedProjectBackup.PathFor(files.AppStore.SettingsPath)));
    }

    [AvaloniaFact]
    public async Task LoadCommand_NoUnsavedChanges_LoadsWithoutPromptOrBackupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        var prompts = 0;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ =>
        {
            prompts++;
            return Task.FromResult<bool?>(null);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(0, prompts);
        Assert.Single(vm.CadPads);
        Assert.False(File.Exists(UnsavedProjectBackup.PathFor(files.AppStore.SettingsPath)));
        Assert.Equal("Project loaded: Step 1 match restored.", vm.StatusText);
    }
}
