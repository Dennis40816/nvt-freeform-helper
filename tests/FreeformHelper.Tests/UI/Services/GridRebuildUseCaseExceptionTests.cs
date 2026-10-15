using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how <see cref="GridRebuildUseCase"/> behaves after the rebuild delegate throws.
/// </summary>
public sealed class GridRebuildUseCaseExceptionTests
{
    [Fact]
    public async Task RequestAsync_WhenRebuildThrowsBeforeFirstAwait_FaultsTheReturnedTaskAndRunsTheNextRequest()
    {
        var calls = 0;
        var useCase = new GridRebuildUseCase(static () => { }, () => ThrowOnFirstCallAsync(++calls));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.RequestAsync(requestFit: false));
        await useCase.RequestAsync(requestFit: false);

        Assert.Equal("first rebuild failed", thrown.Message);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RequestAsync_WhenRebuildThrowsAfterAwait_FaultsTheReturnedTaskAndRunsTheNextRequest()
    {
        var calls = 0;
        var useCase = new GridRebuildUseCase(static () => { }, () => ThrowOnFirstCallAfterYieldAsync(++calls));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.RequestAsync(requestFit: false));
        await useCase.RequestAsync(requestFit: false);

        Assert.Equal("first rebuild failed", thrown.Message);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task WaitForIdleAsync_AfterRebuildThrew_Completes()
    {
        var useCase = new GridRebuildUseCase(static () => { }, static () => ThrowAlwaysAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.RequestAsync(requestFit: false));

        await useCase.WaitForIdleAsync();
    }

    [Fact]
    public async Task RequestAsync_AfterRebuildThrew_StillRequestsFitOnlyForRequestsThatAskedForIt()
    {
        var fitRequests = 0;
        var calls = 0;
        var useCase = new GridRebuildUseCase(() => fitRequests++, () => ThrowOnFirstCallAsync(++calls));

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.RequestAsync(requestFit: false));
        await useCase.RequestAsync(requestFit: true);

        Assert.Equal(1, fitRequests);
    }

    private static Task ThrowOnFirstCallAsync(int call)
    {
        if (call == 1)
        {
            throw new InvalidOperationException("first rebuild failed");
        }

        return Task.CompletedTask;
    }

    private static async Task ThrowOnFirstCallAfterYieldAsync(int call)
    {
        await Task.Yield();
        if (call == 1)
        {
            throw new InvalidOperationException("first rebuild failed");
        }
    }

    private static async Task ThrowAlwaysAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("rebuild failed");
    }
}
