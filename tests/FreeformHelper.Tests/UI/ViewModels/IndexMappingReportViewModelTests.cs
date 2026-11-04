using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, int timeoutMs)
    {
        return await ConditionWait.TryUntilAsync(predicate, TimeSpan.FromMilliseconds(timeoutMs), TestContext.Current.CancellationToken);
    }
}
