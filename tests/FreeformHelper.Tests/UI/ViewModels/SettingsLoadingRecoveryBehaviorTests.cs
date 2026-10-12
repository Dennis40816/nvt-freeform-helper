// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using Xunit;
using static FreeformHelper.Tests.LoadingModeTestScope;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsLoadingRecoveryBehaviorTests
{
    [AvaloniaFact]
    public async Task LoadProjectCommand_SettingsNotificationThrows_RestoresDirtyTrackingAndUndo()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.MatchThreshold = 0.27m);
        var vm = files.CreateViewModel();
        using var failure = OnNextChangeThrowingExpectedException(vm, nameof(vm.MatchThreshold),
            new InvalidOperationException("Synthetic settings notification failure."));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(failure.WasInvoked);
        Assert.Contains("Load project failed", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(0.27m, vm.MatchThreshold);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        var original = vm.NotchThresholdQ7;
        vm.NotchThresholdQ7 = 16m;
        Assert.Equal(12.5m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(original, vm.NotchThresholdQ7);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_ViewNotificationThrows_RestoresDirtyTrackingAndUndo()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.ShowCad = false);
        var vm = files.CreateViewModel();
        using var failure = OnNextChangeThrowingExpectedException(vm, nameof(vm.ShowCad),
            new InvalidOperationException("Synthetic view notification failure."));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(failure.WasInvoked);
        Assert.Contains("Load project failed", vm.StatusText, StringComparison.Ordinal);
        Assert.False(vm.ShowCad);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        var original = vm.NotchThresholdQ7;
        vm.NotchThresholdQ7 = 16m;
        Assert.Equal(12.5m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(original, vm.NotchThresholdQ7);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_ImportNotificationThrows_RestoresDirtyTrackingAndUndo()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.ImportOnlyClosedPolylines = false);
        var vm = files.CreateViewModel();
        using var failure = OnNextChangeThrowingExpectedException(vm, nameof(vm.ImportOnlyClosedPolylines),
            new InvalidOperationException("Synthetic import notification failure."));

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(failure.WasInvoked);
        Assert.Contains("Load project failed", vm.StatusText, StringComparison.Ordinal);
        Assert.False(vm.ImportOnlyClosedPolylines);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        var original = vm.NotchThresholdQ7;
        vm.NotchThresholdQ7 = 16m;
        Assert.Equal(12.5m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(original, vm.NotchThresholdQ7);
    }
}
