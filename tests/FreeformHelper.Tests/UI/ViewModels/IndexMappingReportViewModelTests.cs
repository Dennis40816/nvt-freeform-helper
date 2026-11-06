namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, int timeoutMs)
    {
        var start = TimeProvider.System.GetTimestamp();
        while (TimeProvider.System.GetElapsedTime(start).TotalMilliseconds < timeoutMs)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return predicate();
    }
}
