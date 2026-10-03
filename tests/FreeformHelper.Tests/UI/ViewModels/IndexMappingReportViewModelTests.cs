using System.Diagnostics;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
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
