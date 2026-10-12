// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.LoadingModeTestScope;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadingModeBehaviorTests
{
    [AvaloniaFact]
    public async Task LoadProjectCommand_SavedSettings_RestoresValuesQuietlyInNotificationOrder()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm =>
        {
            vm.MatchThreshold = 0.27m;
            vm.FreeformAxisThreshold = 0.61m;
            vm.NotchThresholdQ7 = 32m;
        });
        var vm = files.CreateViewModel();
        var changes = ObserveChanges(vm);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(0.27m, vm.MatchThreshold);
        Assert.Equal(0.61m, vm.FreeformAxisThreshold);
        Assert.Equal(32m, vm.NotchThresholdQ7);
        Assert.Equal<string?>([nameof(vm.MatchThreshold), nameof(vm.FreeformAxisThreshold), nameof(vm.NotchThresholdQ7)],
            changes.Where(name => name is nameof(vm.MatchThreshold) or nameof(vm.FreeformAxisThreshold) or nameof(vm.NotchThresholdQ7)));
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(0.27m, vm.MatchThreshold);
        Assert.Equal(0.61m, vm.FreeformAxisThreshold);
        Assert.Equal(32m, vm.NotchThresholdQ7);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_EditsBeforeReload_PreservesExistingUndoEntries()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var original = vm.MatchThreshold;
        vm.MatchThreshold = 0.27m;
        Assert.True(await vm.SaveProjectAsync());
        vm.MatchThreshold = 0.44m;

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(0.27m, vm.MatchThreshold);
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo); // Current load does not clear history from the previous state.
        vm.UndoCommand.Execute(null);
        Assert.Equal(0.27m, vm.MatchThreshold);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(original, vm.MatchThreshold);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.CadPads))]
    [InlineData(nameof(FreeformHelperViewModel.RegularPads))]
    public async Task LoadProjectCommand_NotchEditBeforeOrAfterCadAwait_AppliesWithoutSyncDirtyOrUndo(string notification)
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.NotchThresholdQ7 = 32m);
        var vm = files.CreateViewModel();
        using var edit = OnNextChange(vm, notification, () =>
        {
            Assert.Equal(25m, vm.NotchThresholdPercent);
            vm.NotchThresholdQ7 = 64m;
            Assert.Equal(64m, vm.NotchThresholdQ7);
            Assert.Equal(25m, vm.NotchThresholdPercent);
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.CanUndo);
        });

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(edit.WasInvoked);
        Assert.Equal(64m, vm.NotchThresholdQ7);
        Assert.Equal(25m, vm.NotchThresholdPercent);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadProjectCommand_CompletedWithEitherVisualLayer_RestoresNotchSyncDirtyAndUndo(bool applyVisualLayer)
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.NotchThresholdQ7 = 32m);
        var vm = files.CreateViewModel();
        vm.ApplyAppVisualPreferencesOnProjectLoad = applyVisualLayer;
        await vm.LoadProjectCommand.ExecuteAsync(null);

        vm.NotchThresholdQ7 = 96m;

        Assert.Equal(75m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_ManualWidthEdit_SkipsMixedStateHandler()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        using var edit = OnNextChange(vm, nameof(vm.CadPads), () =>
        {
            vm.IsPendingColumnWidthMixed = true;
            vm.PendingColumnWidth = 4m;
            Assert.Equal(4m, vm.PendingColumnWidth);
            Assert.True(vm.IsPendingColumnWidthMixed);
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.CanUndo);
        });

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(edit.WasInvoked);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_Completed_ManualWidthHandlerClearsMixedStateAndRecordsUndo()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        await vm.LoadProjectCommand.ExecuteAsync(null);
        vm.IsPendingColumnWidthMixed = true;

        vm.PendingColumnWidth = 4m;

        Assert.False(vm.IsPendingColumnWidthMixed);
        Assert.True(vm.CanUndo);
        Assert.False(vm.HasUnsavedChanges); // A pending value without a manual range is not a persisted edit.
        vm.UndoCommand.Execute(null);
        Assert.Equal(0m, vm.PendingColumnWidth);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_LayerToggleDuringGridBuild_FiltersCadWithoutDirtyOrUndo()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var vm = files.CreateViewModel();
        using var edit = OnNextChange(vm, nameof(vm.RegularPads), () =>
        {
            Assert.Single(vm.CadPads);
            vm.LayerToggles.Single(toggle => toggle.Name == "signal").IsSelected = false;
            Assert.Empty(vm.CadPads); // Current handler still filters while loading.
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.CanUndo);
        });

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(edit.WasInvoked);
        Assert.False(vm.LayerToggles.Single(toggle => toggle.Name == "signal").IsSelected);
        Assert.Empty(vm.CadPads);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_Completed_LayerToggleFiltersCadAndRecordsDirtyUndo()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var vm = files.CreateViewModel();
        await vm.LoadProjectCommand.ExecuteAsync(null);

        vm.LayerToggles.Single(toggle => toggle.Name == "signal").IsSelected = false;

        Assert.Empty(vm.CadPads);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Single(vm.CadPads);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadProjectCommand_UnreadableFile_LeavesNotchSyncDirtyAndUndoEnabled(bool invalidContent)
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel(files.UnreadableProjectPath(invalidContent));

        await vm.LoadProjectCommand.ExecuteAsync(null);
        vm.NotchThresholdQ7 = 16m;

        Assert.Equal(12.5m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
        Assert.True(vm.IsProjectEditingEnabled);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_ExceptionInCadReset_RestoresNotchSyncDirtyAndUndo()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        using var failure = OnNextChangeThrowingExpectedException(vm, nameof(vm.CadPads),
            new InvalidOperationException("Synthetic CAD reset failure."));

        await vm.LoadProjectCommand.ExecuteAsync(null);
        vm.NotchThresholdQ7 = 16m;

        Assert.True(failure.WasInvoked);
        Assert.Contains("Load project failed", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(12.5m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task UndoCommand_DuringProjectLoad_KeepsExistingHistoryAndSetting()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.ShowCad = false);
        var vm = files.CreateViewModel();
        vm.ShowCad = false;
        using var undo = OnNextChange(vm, nameof(vm.CadPads), () =>
        {
            Assert.True(vm.CanUndo);
            Assert.False(vm.UndoCommand.CanExecute(null));
            vm.UndoCommand.Execute(null);
            Assert.False(vm.ShowCad);
            Assert.True(vm.CanUndo);
        });

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(undo.WasInvoked);
        vm.UndoCommand.Execute(null);
        Assert.True(vm.ShowCad);
        Assert.False(vm.CanUndo);
    }
}
