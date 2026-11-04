namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// The one place where a test blocks a thread on purpose: a callback that must stay busy while the test checks
/// that the caller does not wait for it, or a UI thread that is held to prove a method does not need it. Every
/// other wait in a test awaits (<c>Task.WaitAsync</c>). The wait goes through a <see cref="WaitHandle"/>, so no
/// <c>Task.Wait</c>, <c>Task.Result</c> or <c>ManualResetEventSlim.Wait</c> call is left in a test. The owner of
/// the blocking-wait rule must approve this file before it is relied on.
/// </summary>
internal sealed class BlockingSignal : IDisposable
{
    private readonly ManualResetEventSlim _released = new(initialState: false);

    public void Release()
    {
        _released.Set();
    }

    /// <summary>Blocks the calling thread until <see cref="Release"/> is called or the timeout ends; returns whether it was released.</summary>
    public bool BlockUntilReleased(TimeSpan timeout)
    {
        return _released.WaitHandle.WaitOne(timeout);
    }

    /// <summary>Blocks the calling thread until the task completes or the timeout ends; returns whether it completed.</summary>
    public static bool BlockUntilCompleted(Task task, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(task);
        return ((IAsyncResult)task).AsyncWaitHandle.WaitOne(timeout);
    }

    public void Dispose()
    {
        _released.Dispose();
    }
}
