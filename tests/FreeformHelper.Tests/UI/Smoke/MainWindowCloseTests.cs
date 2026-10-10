// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowCloseTests
{
    [AvaloniaFact]
    public async Task OnClosing_PromptPending_CancelsSynchronously()
    {
        var prompt = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new ClosingWindowFixture(_ => prompt.Task);
        var cancelledWhenHandlerReturned = false;
        fixture.Window.Closing += (_, e) => cancelledWhenHandlerReturned = e.Cancel;

        try
        {
            fixture.Window.Close();

            Assert.True(cancelledWhenHandlerReturned);
            Assert.True(fixture.Window.IsVisible);
        }
        finally
        {
            prompt.TrySetResult(null);
            await Dispatcher.UIThread.InvokeAsync(static () => { });
        }
    }

    [AvaloniaFact]
    public async Task OnClosing_SavePickerFails_KeepsWindowOpenAndProjectDirty()
    {
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new ClosingWindowFixture(_ => Task.FromResult<bool?>(true));
        fixture.Helper.PickSaveProjectPathAsync = () => picker.Task;
        fixture.Window.Close();
        var save = Assert.IsAssignableFrom<Task<bool>>(fixture.Helper.SaveProjectCommand.ExecutionTask);

        picker.SetException(new InvalidOperationException("save picker unavailable"));
        Assert.False(await save);
        await Dispatcher.UIThread.InvokeAsync(static () => { });

        Assert.True(fixture.Window.IsVisible);
        Assert.True(fixture.Helper.HasUnsavedChanges);
        Assert.Equal(0, fixture.ClosedCount);
        Assert.False(fixture.Helper.SaveProjectCommand.IsRunning);
        Assert.Contains("save picker unavailable", fixture.Helper.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task OnClosing_SaveWriteFails_KeepsWindowOpenAndReleasesSaveCommand()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            using var fixture = new ClosingWindowFixture(_ => Task.FromResult<bool?>(true));
            fixture.Helper.PickSaveProjectPathAsync = () => Task.FromResult<string?>(directory.FullName);

            fixture.Window.Close();
            var save = Assert.IsAssignableFrom<Task<bool>>(fixture.Helper.SaveProjectCommand.ExecutionTask);
            Assert.False(await save);
            await Dispatcher.UIThread.InvokeAsync(static () => { });

            Assert.True(fixture.Window.IsVisible);
            Assert.True(fixture.Helper.HasUnsavedChanges);
            Assert.Equal(0, fixture.ClosedCount);
            Assert.False(fixture.Helper.SaveProjectCommand.IsRunning);
            Assert.True(fixture.Helper.SaveProjectCommand.CanExecute(null));
            Assert.Contains("Save project failed", fixture.Helper.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task OnClosing_SaveSucceeds_ClosesExactlyOnce()
    {
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            using var fixture = new ClosingWindowFixture(_ => Task.FromResult<bool?>(true));
            var path = Path.Combine(directory.FullName, "saved.json");
            fixture.Helper.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);

            fixture.Window.Close();
            await fixture.Closed.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(1, fixture.ClosedCount);
            Assert.False(fixture.Helper.HasUnsavedChanges);
            Assert.False(fixture.Window.IsVisible);
            Assert.True(File.Exists(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task OnClosing_SecondCloseWhileSaving_DoesNotStartSecondSave()
    {
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saves = 0;
        var prompts = 0;
        using var fixture = new ClosingWindowFixture(_ =>
        {
            prompts++;
            return Task.FromResult<bool?>(true);
        });
        fixture.Helper.PickSaveProjectPathAsync = () =>
        {
            saves++;
            return picker.Task;
        };
        fixture.Window.Close();
        var save = Assert.IsAssignableFrom<Task<bool>>(fixture.Helper.SaveProjectCommand.ExecutionTask);
        var cancelled = false;
        fixture.Window.Closing += (_, e) => cancelled = e.Cancel;

        try
        {
            fixture.Window.Close();

            Assert.True(cancelled);
            Assert.Equal(1, saves);
            Assert.Equal(1, prompts);
        }
        finally
        {
            picker.TrySetResult(null);
            await save;
            await Dispatcher.UIThread.InvokeAsync(static () => { });
        }
    }

    [AvaloniaFact]
    public async Task OnClosing_MenuSaveRunning_DoesNotStartSecondSave()
    {
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saves = 0;
        using var fixture = new ClosingWindowFixture(_ => Task.FromResult<bool?>(true));
        fixture.Helper.PickSaveProjectPathAsync = () =>
        {
            saves++;
            return picker.Task;
        };
        var menuSave = fixture.Helper.SaveProjectCommand.ExecuteAsync(null);

        try
        {
            fixture.Window.Close();

            Assert.Equal(1, saves);
            Assert.True(fixture.Helper.SaveProjectCommand.IsRunning);
            Assert.True(fixture.Window.IsVisible);
        }
        finally
        {
            picker.TrySetResult(null);
            await menuSave;
            await Dispatcher.UIThread.InvokeAsync(static () => { });
        }
    }

    [AvaloniaFact]
    public void OnClosing_PromptFails_ReportsOnceAndKeepsProjectDirty()
    {
        var probe = new UiEventProbe();
        var failure = new InvalidOperationException("prompt unavailable");
        using var fixture = new ClosingWindowFixture(_ => Task.FromException<bool?>(failure), probe.Runner);

        fixture.Window.Close();

        var report = Assert.Single(probe.Reports);
        Assert.Equal("Window.ClosingSave", report.Operation);
        Assert.Same(failure, report.Exception);
        Assert.True(fixture.Helper.HasUnsavedChanges);
        Assert.True(fixture.Window.IsVisible);
        Assert.Equal(0, fixture.ClosedCount);
    }

    [AvaloniaFact]
    public void OnClosing_ExitWithoutSaving_ClosesOnce()
    {
        using var fixture = new ClosingWindowFixture(_ => Task.FromResult<bool?>(false));

        fixture.Window.Close();

        Assert.Equal(1, fixture.ClosedCount);
    }
}
