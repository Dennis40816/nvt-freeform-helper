namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Waits until a condition holds, for state that has no event or task to await. It polls with a
/// <see cref="PeriodicTimer"/> and ends with a <see cref="TimeoutException"/> after the given time, so it needs
/// neither <c>Task.Delay</c>, nor <c>Environment.TickCount64</c>, nor a <c>Stopwatch</c>. Prefer awaiting a
/// <see cref="TaskCompletionSource"/> when the code under test can signal.
/// </summary>
internal static class ConditionWait
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(20);

    public static async Task UntilAsync(Func<bool> condition, TimeSpan timeout, string description, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrEmpty(description);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using var timer = new PeriodicTimer(_pollInterval);

        try
        {
            while (!condition())
            {
                await timer.WaitForNextTickAsync(timeoutSource.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!condition())
            {
                throw new TimeoutException($"Timed out after {timeout.TotalMilliseconds:0} ms waiting for: {description}.");
            }
        }
    }

    public static async Task<bool> TryUntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(condition);

        try
        {
            await UntilAsync(condition, timeout, "condition", cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
