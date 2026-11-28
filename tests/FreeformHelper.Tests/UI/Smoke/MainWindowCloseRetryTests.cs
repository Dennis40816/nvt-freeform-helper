// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins close retries and save availability through the existing main window adapters.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class MainWindowCloseRetryTests
{
    [AvaloniaFact]
    public void OnClosingDismissThenRetryPromptsAgainAndKeepsProjectDirty()
    {
        var prompts = 0;
        var saves = 0;
        using var fixture = new ClosingWindowFixture(_ =>
        {
            prompts++;
            return Task.FromResult<bool?>(null);
        });
        fixture.Helper.PickSaveProjectPathAsync = () =>
        {
            saves++;
            return Task.FromResult<string?>(null);
        };

        fixture.Window.Close();
        Assert.Equal(1, prompts);

        fixture.Window.Close();

        Assert.Equal(2, prompts);
        Assert.Equal(0, saves);
        Assert.True(fixture.Window.IsVisible);
        Assert.True(fixture.Helper.HasUnsavedChanges);
        Assert.Equal(0, fixture.ClosedCount);
    }

    [AvaloniaFact]
    public async Task OnClosingSaveCancelledThenRetryKeepsWindowOpenAndReleasesGuards()
    {
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompts = 0;
        var saves = 0;
        using var fixture = new ClosingWindowFixture(_ =>
        {
            prompts++;
            return Task.FromResult<bool?>(true);
        });
        fixture.Helper.PickSaveProjectPathAsync = () =>
        {
            saves++;
            return picker.Task.WaitAsync(TestContext.Current.CancellationToken);
        };

        fixture.Window.Close();
        picker.SetResult(null);
        var firstSave = Assert.IsAssignableFrom<Task<bool>>(fixture.Helper.SaveProjectCommand.ExecutionTask);
        Assert.False(await firstSave);
        await HeadlessUiPump.YieldAsync();
        Assert.True(fixture.Window.IsVisible);
        Assert.Equal(0, fixture.ClosedCount);

        fixture.Window.Close();
        var retrySave = Assert.IsAssignableFrom<Task<bool>>(fixture.Helper.SaveProjectCommand.ExecutionTask);
        Assert.False(await retrySave);
        await HeadlessUiPump.YieldAsync();

        Assert.Equal(2, prompts);
        Assert.Equal(2, saves);
        Assert.True(fixture.Window.IsVisible);
        Assert.True(fixture.Helper.HasUnsavedChanges);
        Assert.Equal(0, fixture.ClosedCount);
        Assert.False(fixture.Helper.SaveProjectCommand.IsRunning);
        Assert.True(fixture.Helper.SaveProjectCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void OnClosingCleanProjectClosesWithoutPrompting()
    {
        var prompts = 0;
        using var fixture = new ClosingWindowFixture(_ =>
        {
            prompts++;
            return Task.FromResult<bool?>(null);
        });
        fixture.Helper.HasUnsavedChanges = false;

        fixture.Window.Close();

        Assert.Equal(0, prompts);
        Assert.Equal(1, fixture.ClosedCount);
        Assert.False(fixture.Window.IsVisible);
    }

    [AvaloniaFact]
    public async Task OnClosingSaveDisabledDuringLoadKeepsWindowOpen()
    {
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompts = 0;
        var saves = 0;
        using var fixture = new ClosingWindowFixture(_ =>
        {
            prompts++;
            return Task.FromResult<bool?>(true);
        });
        fixture.Helper.PickLoadProjectPathAsync = () => picker.Task.WaitAsync(TestContext.Current.CancellationToken);
        fixture.Helper.PickSaveProjectPathAsync = () =>
        {
            saves++;
            return Task.FromResult<string?>(null);
        };
        var load = fixture.Helper.LoadProjectCommand.ExecuteAsync(null);

        try
        {
            Assert.False(fixture.Helper.SaveProjectCommand.CanExecute(null));

            fixture.Window.Close();
            await HeadlessUiPump.YieldAsync();

            Assert.Equal(1, prompts);
            Assert.Equal(0, saves);
            Assert.Null(fixture.Helper.SaveProjectCommand.ExecutionTask);
            Assert.True(fixture.Window.IsVisible);
            Assert.True(fixture.Helper.HasUnsavedChanges);
            Assert.Equal(0, fixture.ClosedCount);
        }
        finally
        {
            picker.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }
    }
}
