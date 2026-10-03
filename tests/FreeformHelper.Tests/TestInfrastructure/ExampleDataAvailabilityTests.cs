using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ExampleDataAvailabilityTests
{
    [Fact]
    public void ExampleData_WhenRequired_IsCheckedOut()
    {
        if (!ExampleData.IsRequired)
        {
            return;
        }

        Assert.True(ExampleData.IsAvailable, ExampleData.MissingMessage);
    }
}
