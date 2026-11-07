using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, int timeoutMs)
    {
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        watchdog.CancelAfter(timeoutMs);
        while (!watchdog.IsCancellationRequested)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Yield();
        }

        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        return predicate();
    }
}
