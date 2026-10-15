using System.Runtime.CompilerServices;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the UI operation runners do when the operation or its start callback fails: the failure goes to the
/// error callback, and the busy flag, the progress value and the modal spinner are restored.
/// </summary>
public sealed class RunUiOperationFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProgressOperation_WhenOperationFails_ReportsFailureAndRestoresBusyProgressAndSpinner(bool showModalSpinner)
    {
        var vm = new FreeformHelperViewModel();
        var busy = new List<bool>();
        var progress = new List<double>();
        var failure = new InvalidOperationException("operation failed");
        Exception? reported = null;

        await vm.RunUiProgressOperationForTestsAsync(
            "Progress failure",
            busy.Add,
            progress.Add,
            _ => Task.FromException(failure),
            ex => reported = ex,
            onStart: null,
            showModalSpinner);

        Assert.Same(failure, reported);
        Assert.Equal([true, false], busy);
        Assert.Equal(1.0, progress[^1]);
        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    [Fact]
    public async Task ProgressOperation_WhenStartCallbackFails_ReportsFailureWithoutRunningTheOperation()
    {
        var vm = new FreeformHelperViewModel();
        var busy = new List<bool>();
        var operationRan = false;
        var failure = new InvalidOperationException("start failed");
        Exception? reported = null;

        await vm.RunUiProgressOperationForTestsAsync(
            "Progress start failure",
            busy.Add,
            static _ => { },
            _ =>
            {
                operationRan = true;
                return Task.CompletedTask;
            },
            ex => reported = ex,
            () => throw failure,
            showModalSpinner: false);

        Assert.Same(failure, reported);
        Assert.False(operationRan);
        Assert.Equal([true, false], busy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProgressOperation_WhenOperationRunsOutOfMemory_PropagatesAfterRestoringBusyProgressAndSpinner(bool showModalSpinner)
    {
        var vm = new FreeformHelperViewModel();
        var busy = new List<bool>();
        var progress = new List<double>();
        var errorCalled = false;
        var outOfMemory = CreateOutOfMemory();

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => vm.RunUiProgressOperationForTestsAsync(
            "Progress out of memory",
            busy.Add,
            progress.Add,
            _ => Task.FromException(outOfMemory),
            _ => errorCalled = true,
            onStart: null,
            showModalSpinner));

        Assert.Same(outOfMemory, thrown);
        Assert.False(errorCalled);
        Assert.Equal([true, false], busy);
        Assert.Equal(1.0, progress[^1]);
        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    [Fact]
    public async Task ProgressOperation_WhenStartCallbackRunsOutOfMemory_PropagatesWithoutRunningTheOperation()
    {
        var vm = new FreeformHelperViewModel();
        var busy = new List<bool>();
        var operationRan = false;
        var errorCalled = false;
        var outOfMemory = CreateOutOfMemory();

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => vm.RunUiProgressOperationForTestsAsync(
            "Progress start out of memory",
            busy.Add,
            static _ => { },
            _ =>
            {
                operationRan = true;
                return Task.CompletedTask;
            },
            _ => errorCalled = true,
            () => throw outOfMemory,
            showModalSpinner: false));

        Assert.Same(outOfMemory, thrown);
        Assert.False(operationRan);
        Assert.False(errorCalled);
        Assert.Equal([true, false], busy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResultOperation_WhenOperationRunsOutOfMemory_PropagatesAfterRestoringSpinner(bool showModalSpinner)
    {
        var vm = new FreeformHelperViewModel();
        var errorCalled = false;
        var outOfMemory = CreateOutOfMemory();

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(() => vm.RunUiOperationForTestsAsync<int>(
            "Result out of memory",
            () => Task.FromException<int>(outOfMemory),
            _ =>
            {
                errorCalled = true;
                return -1;
            },
            onStart: null,
            showModalSpinner));

        Assert.Same(outOfMemory, thrown);
        Assert.False(errorCalled);
        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    // CA2201 forbids creating the type directly, and no test reads its message.
    private static OutOfMemoryException CreateOutOfMemory() =>
        (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));

    [Fact]
    public async Task ProgressOperation_WhenOperationSucceeds_SkipsTheErrorCallbackAndRestoresBusy()
    {
        var vm = new FreeformHelperViewModel();
        var busy = new List<bool>();
        var progress = new List<double>();
        var errorCalled = false;

        await vm.RunUiProgressOperationForTestsAsync(
            "Progress success",
            busy.Add,
            progress.Add,
            static _ => Task.CompletedTask,
            _ => errorCalled = true,
            onStart: null,
            showModalSpinner: false);

        Assert.False(errorCalled);
        Assert.Equal([true, false], busy);
        Assert.Equal(1.0, progress[^1]);
    }
}
