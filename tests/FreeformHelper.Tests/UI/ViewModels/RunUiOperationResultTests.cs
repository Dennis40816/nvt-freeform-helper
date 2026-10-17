using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Characterizes the value-returning UI operation runner: when the start callback runs, which value comes back,
/// and what happens when the error callback itself throws.
/// </summary>
public sealed class RunUiOperationResultTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Success_ReturnsOperationValueAfterStartCallbackAndSkipsErrorCallback(bool showModalSpinner)
    {
        var vm = new FreeformHelperViewModel();
        var order = new List<string>();

        var result = await vm.RunUiOperationAsync(
            "Result success",
            () => ReturnAfterYieldAsync(order, 42),
            _ =>
            {
                order.Add("error");
                return -1;
            },
            () => order.Add("start"),
            showModalSpinner);

        Assert.Equal(42, result);
        Assert.Equal("start,operation", string.Join(',', order));
        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperationFailure_ReturnsErrorCallbackValueAndReportsTheSameException(bool showModalSpinner)
    {
        var vm = new FreeformHelperViewModel();
        var order = new List<string>();
        var failure = new InvalidOperationException("operation failed");
        Exception? reported = null;

        var result = await vm.RunUiOperationAsync(
            "Result failure",
            () => ThrowAfterYieldAsync(order, failure),
            ex =>
            {
                order.Add("error");
                reported = ex;
                return -1;
            },
            () => order.Add("start"),
            showModalSpinner);

        Assert.Equal(-1, result);
        Assert.Same(failure, reported);
        Assert.Equal("start,operation,error", string.Join(',', order));
        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    [Fact]
    public async Task StartCallbackFailure_ReturnsErrorCallbackValueWithoutRunningTheOperation()
    {
        var vm = new FreeformHelperViewModel();
        var operationRan = false;
        var failure = new InvalidOperationException("start failed");
        Exception? reported = null;

        var result = await vm.RunUiOperationAsync(
            "Result start failure",
            () =>
            {
                operationRan = true;
                return Task.FromResult(1);
            },
            ex =>
            {
                reported = ex;
                return -2;
            },
            () => throw failure,
            showModalSpinner: false);

        Assert.Equal(-2, result);
        Assert.Same(failure, reported);
        Assert.False(operationRan);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorCallbackFailure_PropagatesToTheCaller(bool showModalSpinner)
    {
        var vm = new FreeformHelperViewModel();
        var replacement = new ArgumentException("error callback failed");

        var thrown = await Assert.ThrowsAsync<ArgumentException>(() => vm.RunUiOperationAsync<int>(
            "Result error callback failure",
            () => Task.FromException<int>(new InvalidOperationException("operation failed")),
            _ => throw replacement,
            onStart: null,
            showModalSpinner));

        Assert.Same(replacement, thrown);
        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    [Fact]
    public async Task ModalSpinner_IsVisibleWhileTheOperationRunsAndHiddenAfterwards()
    {
        var vm = new FreeformHelperViewModel();
        var visibleInside = false;

        var result = await vm.RunUiOperationAsync(
            "Result spinner",
            () =>
            {
                visibleInside = vm.IsModalLoadingSpinnerVisible;
                return Task.FromResult("done");
            },
            _ => "failed",
            onStart: null,
            showModalSpinner: true);

        Assert.Equal("done", result);
        Assert.True(visibleInside);
        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    private static async Task<int> ReturnAfterYieldAsync(List<string> order, int value)
    {
        order.Add("operation");
        await Task.Yield();
        return value;
    }

    private static async Task<int> ThrowAfterYieldAsync(List<string> order, Exception failure)
    {
        order.Add("operation");
        await Task.Yield();
        throw failure;
    }
}
