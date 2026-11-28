// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Verifies close confirmation outcomes, save completion and exception propagation.
/// </summary>
public sealed class ProjectCloseWorkflowTests
{
    [Fact]
    public async Task CanCloseAsyncDismissedChoiceKeepsWindowOpenWithoutStartingSave()
    {
        var saveStarts = 0;

        var canClose = await ProjectCloseWorkflow.CanCloseAsync(null, () =>
        {
            saveStarts++;
            return Task.FromResult(true);
        });

        Assert.False(canClose);
        Assert.Equal(0, saveStarts);
    }

    [Fact]
    public async Task CanCloseAsyncDiscardChoiceClosesWithoutStartingSave()
    {
        var saveStarts = 0;

        var canClose = await ProjectCloseWorkflow.CanCloseAsync(false, () =>
        {
            saveStarts++;
            return Task.FromResult(true);
        });

        Assert.True(canClose);
        Assert.Equal(0, saveStarts);
    }

    [Fact]
    public async Task CanCloseAsyncSaveNotStartableKeepsWindowOpen()
    {
        var saveStarts = 0;

        Task<bool>? NotStartableAsync()
        {
            saveStarts++;
            return null;
        }

        var canClose = await ProjectCloseWorkflow.CanCloseAsync(true, NotStartableAsync);

        Assert.False(canClose);
        Assert.Equal(1, saveStarts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanCloseAsyncSaveResultDeterminesWhetherWindowCloses(bool saveSucceeded)
    {
        var saveStarts = 0;

        var canClose = await ProjectCloseWorkflow.CanCloseAsync(true, () =>
        {
            saveStarts++;
            return Task.FromResult(saveSucceeded);
        });

        Assert.Equal(saveSucceeded, canClose);
        Assert.Equal(1, saveStarts);
    }

    [Fact]
    public async Task CanCloseAsyncSaveTaskFailurePropagates()
    {
        var failure = new InvalidOperationException("save task failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProjectCloseWorkflow.CanCloseAsync(true, () => Task.FromException<bool>(failure)));

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task CanCloseAsyncStartsSaveExactlyOnceAndWaitsForCompletion()
    {
        var save = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveStarts = 0;
        var closing = ProjectCloseWorkflow.CanCloseAsync(true, () =>
        {
            saveStarts++;
            return save.Task.WaitAsync(TestContext.Current.CancellationToken);
        });

        try
        {
            Assert.Equal(1, saveStarts);
            Assert.False(closing.IsCompleted);
        }
        finally
        {
            save.TrySetResult(true);
        }

        Assert.True(await closing.WaitAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, saveStarts);
    }
}
