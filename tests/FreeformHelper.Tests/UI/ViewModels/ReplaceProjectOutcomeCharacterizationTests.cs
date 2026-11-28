// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Characterizes replacement and recovery through the existing project commands.</summary>
[Collection("HeadlessUiSerial")]
public sealed class ReplaceProjectOutcomeCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CancelledReplacementBackupContainsUnsavedSettingsAndPadEditsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        vm.MatchThreshold = 0.61m;
        var backupPath = BackupPath(files);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = path =>
        {
            Assert.Equal(backupPath, path);
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            Assert.False(vm.SaveProjectCommand.CanExecute(null));
            var backup = JsonProjectStore.Load(path!);
            Assert.Equal(0.61, backup.Settings.Matching.MatchThreshold);
            Assert.Equal(0.61, backup.UiSnapshot.Matching.MatchThreshold);
            return Task.FromResult<bool?>(null);
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);

        var reopened = files.CreateViewModel();
        Assert.True((await reopened.LoadProjectFromPathAsync(backupPath)).IsLoaded);
        Assert.Equal(0.61m, reopened.MatchThreshold);
        Assert.Empty(reopened.CadPads);
        Assert.Equal(1, reopened.DeletedCadPadCount);
        Assert.Equal(0.27, JsonProjectStore.Load(files.PathFor("current.json")).Settings.Matching.MatchThreshold);
        Assert.True(vm.HasUnsavedChanges);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RepeatedCancelledReplacementOverwritesTheSingleBackupWithLatestEditsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        var paths = new List<string?>();
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = path =>
        {
            paths.Add(path);
            return Task.FromResult<bool?>(null);
        };
        vm.MatchThreshold = 0.61m;
        await vm.LoadProjectCommand.ExecuteAsync(null);
        Assert.Equal(0.61, JsonProjectStore.Load(BackupPath(files)).Settings.Matching.MatchThreshold);
        vm.MatchThreshold = 0.74m;

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(new string?[] { BackupPath(files), BackupPath(files) }, paths);
        Assert.Equal(0.74, JsonProjectStore.Load(BackupPath(files)).Settings.Matching.MatchThreshold);
        Assert.Equal(BackupPath(files), Assert.Single(Directory.EnumerateFiles(
            Path.GetDirectoryName(BackupPath(files))!, "unsaved-project-backup*.json")));
        Assert.Equal(0.74m, vm.MatchThreshold);
        Assert.True(vm.HasUnsavedChanges);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task UnwiredPromptLoadsReplacementAfterBackupWithoutSavingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        vm.MatchThreshold = 0.61m;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = null;
        var savePickerCalls = 0;
        vm.PickSaveProjectPathAsync = () =>
        {
            savePickerCalls++;
            return Task.FromResult<string?>(null);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(0, savePickerCalls);
        Assert.Equal(0.27m, vm.MatchThreshold);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(0.61, JsonProjectStore.Load(BackupPath(files)).Settings.Matching.MatchThreshold);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task BackupSharingFailureStillPromptsWithoutPathAndRetainsPreviousBackupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        vm.MatchThreshold = 0.61m;
        var backupPath = BackupPath(files);
        TestFiles.CopyOverwriting(files.PathFor("current.json"), backupPath);
        using var lockedBackup = File.Open(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var prompts = 0;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = path =>
        {
            prompts++;
            Assert.Null(path);
            Assert.Contains("Could not back up the unsaved project:", vm.StatusText, StringComparison.Ordinal);
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            return Task.FromResult<bool?>(null);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, prompts);
        Assert.Equal(0.27, JsonProjectStore.Load(backupPath).Settings.Matching.MatchThreshold);
        Assert.Equal(0.61m, vm.MatchThreshold);
        Assert.True(vm.HasUnsavedChanges);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task BackupAccessFailureStillPromptsWithoutPathAndRetainsPreviousBackupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        vm.MatchThreshold = 0.61m;
        var backupPath = BackupPath(files);
        TestFiles.CopyOverwriting(files.PathFor("current.json"), backupPath);
        var attributes = File.GetAttributes(backupPath);
        File.SetAttributes(backupPath, attributes | FileAttributes.ReadOnly);
        try
        {
            var prompts = 0;
            vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = path =>
            {
                prompts++;
                Assert.Null(path);
                Assert.Contains("Could not back up the unsaved project:", vm.StatusText, StringComparison.Ordinal);
                Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
                return Task.FromResult<bool?>(null);
            };
            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

            await vm.LoadProjectCommand.ExecuteAsync(null);

            Assert.Equal(1, prompts);
            Assert.Equal(0.27, JsonProjectStore.Load(backupPath).Settings.Matching.MatchThreshold);
            Assert.Equal(0.61m, vm.MatchThreshold);
            Assert.True(vm.HasUnsavedChanges);
            AssertReady(vm);
        }
        finally
        {
            File.SetAttributes(backupPath, attributes);
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task FailedSaveKeepsCurrentProjectAndBackupAfterLeavingLoadSessionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        await files.CreateSavedCadAsync("replacement.json");
        var vm = await files.CreateSavedCadAsync("current.json");
        vm.MatchThreshold = 0.61m;
        var savePath = files.PathFor("save-destination.json");
        Directory.CreateDirectory(savePath);
        var savePickerCalls = 0;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ => Task.FromResult<bool?>(true);
        vm.PickSaveProjectPathAsync = () =>
        {
            savePickerCalls++;
            AssertEditingEnabled(vm);
            Assert.True(File.Exists(BackupPath(files)));
            return Task.FromResult<string?>(savePath);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(files.PathFor("replacement.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, savePickerCalls);
        Assert.Equal(0.61m, vm.MatchThreshold);
        Assert.Single(vm.CadPads);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal(0.61, JsonProjectStore.Load(BackupPath(files)).Settings.Matching.MatchThreshold);
        Assert.Equal(0.27, JsonProjectStore.Load(files.PathFor("current.json")).Settings.Matching.MatchThreshold);
        Assert.Equal("Load cancelled: the project was not saved, so unsaved changes were kept.", vm.StatusText);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task FailedReplacementAfterSuccessfulSaveKeepsSavedCurrentProjectButDeletesBackupAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var replacementPath = files.PathFor("invalid-replacement.json");
        TestFiles.WriteText(replacementPath, "{");
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        vm.MatchThreshold = 0.61m;
        var savePath = files.PathFor("kept-edits.json");
        var modes = new List<ProjectSessionMode>();
        vm.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(vm.SessionMode)) modes.Add(vm.SessionMode);
        };
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ => Task.FromResult<bool?>(true);
        vm.PickSaveProjectPathAsync = () =>
        {
            AssertEditingEnabled(vm);
            Assert.True(File.Exists(BackupPath(files)));
            return Task.FromResult<string?>(savePath);
        };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(replacementPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(new[] { ProjectSessionMode.LoadingProject, ProjectSessionMode.Ready,
            ProjectSessionMode.LoadingProject, ProjectSessionMode.Ready }, modes);
        Assert.False(File.Exists(BackupPath(files)));
        Assert.Equal(0.61, JsonProjectStore.Load(savePath).Settings.Matching.MatchThreshold);
        Assert.Equal(0.61m, vm.MatchThreshold);
        Assert.Empty(vm.CadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.False(vm.HasUnsavedChanges);
        Assert.StartsWith("Load project failed", vm.StatusText, StringComparison.Ordinal);
        AssertReady(vm);
    }

    private static string BackupPath(ProjectLoadCharacterizationScope files)
    {
        return FreeformHelper.UI.Services.UnsavedProjectBackup.PathFor(files.AppStore.SettingsPath);
    }

    private static void AssertReady(FreeformHelperViewModel vm)
    {
        AssertEditingEnabled(vm);
        Assert.True(vm.LoadProjectCommand.CanExecute(null));
    }

    private static void AssertEditingEnabled(FreeformHelperViewModel vm)
    {
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.True(vm.IsProjectEditingEnabled);
        Assert.True(vm.SaveProjectCommand.CanExecute(null));
    }
}
