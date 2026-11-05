using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.LoadingModeTestScope;

namespace FreeformHelper.Tests;

/// <summary>
/// The settings window is not modal. These tests open it on one project, replace that project through the
/// real load path, and press Save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class SettingsWindowStaleDraftTests
{
    private const decimal LoadedMatchThreshold = 0.37m;
    private const decimal DraftMatchThreshold = 0.27m;

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Save_AfterAnotherProjectWasLoaded_LeavesTheLoadedProjectUnchangedAndSaysWhy(bool loadThroughMenu)
    {
        using var files = new LoadingModeTestScope();
        await SaveLoadedProjectAsync(files);
        var vm = files.CreateViewModel();
        var draft = vm.CreateSettingsWindowViewModel();
        draft.MatchThreshold = DraftMatchThreshold;
        var closeRequests = 0;
        draft.RequestClose += () => closeRequests++;

        await LoadAsync(vm, files, loadThroughMenu);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        var loadedMatchThreshold = vm.MatchThreshold;
        var loadedAutoDetect = vm.EnableAutoDetectXy;
        var canUndoAfterLoad = vm.CanUndo;
        Assert.Equal(LoadedMatchThreshold, loadedMatchThreshold);
        Assert.NotEqual(draft.EnableAutoDetectXy, loadedAutoDetect);
        Assert.False(vm.HasUnsavedChanges);

        draft.SaveCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Equal(loadedMatchThreshold, vm.MatchThreshold);
        Assert.Equal(loadedAutoDetect, vm.EnableAutoDetectXy);
        Assert.Equal(3240m, vm.ActiveAreaPixelsX);
        Assert.Equal(424m, vm.ActiveAreaPixelsY);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(canUndoAfterLoad, vm.CanUndo);
        Assert.Contains("Settings not saved", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(1, closeRequests);
    }

    [AvaloniaFact]
    public async Task Save_WithADraftOpenedAfterTheLoad_AppliesTheEdit()
    {
        using var files = new LoadingModeTestScope();
        await SaveLoadedProjectAsync(files);
        var vm = files.CreateViewModel();
        await LoadAsync(vm, files, loadThroughMenu: false);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        var freshDraft = vm.CreateSettingsWindowViewModel();
        freshDraft.MatchThreshold = DraftMatchThreshold;
        freshDraft.SaveCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Equal(DraftMatchThreshold, vm.MatchThreshold);
        Assert.DoesNotContain("Settings not saved", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Save_AfterTheSettingsWereResetToDefaults_IsRefused()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        vm.MatchThreshold = LoadedMatchThreshold;
        var draft = vm.CreateSettingsWindowViewModel();
        draft.MatchThreshold = DraftMatchThreshold;

        vm.CreateSettingsWindowViewModel().ResetAllSettingsToDefaults();
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        var defaultMatchThreshold = vm.MatchThreshold;
        Assert.NotEqual(DraftMatchThreshold, defaultMatchThreshold);

        draft.SaveCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Equal(defaultMatchThreshold, vm.MatchThreshold);
        Assert.Contains("Settings not saved", vm.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Save_WhenTheSameProjectFileIsLoadedAgain_IsRefused()
    {
        using var files = new LoadingModeTestScope();
        await SaveLoadedProjectAsync(files);
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        var draft = vm.CreateSettingsWindowViewModel();
        draft.MatchThreshold = DraftMatchThreshold;

        Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        draft.SaveCommand.Execute(null);

        Assert.Equal(LoadedMatchThreshold, vm.MatchThreshold);
        Assert.Contains("Settings not saved", vm.StatusText, StringComparison.Ordinal);
    }

    private static Task SaveLoadedProjectAsync(LoadingModeTestScope files)
    {
        return files.SaveAsync(saved =>
        {
            saved.MatchThreshold = LoadedMatchThreshold;
            saved.EnableAutoDetectXy = !saved.EnableAutoDetectXy;
            saved.ActiveAreaPixelsX = 3240m;
            saved.ActiveAreaPixelsY = 424m;
        });
    }

    private static async Task LoadAsync(FreeformHelperViewModel vm, LoadingModeTestScope files, bool loadThroughMenu)
    {
        if (loadThroughMenu)
        {
            await vm.LoadProjectCommand.ExecuteAsync(null);
        }
        else
        {
            Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        }
    }
}
