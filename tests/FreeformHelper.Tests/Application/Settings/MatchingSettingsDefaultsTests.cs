using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MatchingSettingsDefaultsTests
{
    [Fact]
    public void MatchingSettings_DefaultFreeformAxisThreshold_IsPointOne()
    {
        var settings = new MatchingSettings();

        Assert.Equal(0.1, settings.FreeformAxisThreshold, 6);
    }

    [Fact]
    public void UiMatchingSnapshot_DefaultFreeformAxisThreshold_IsPointOne()
    {
        var snapshot = new UiMatchingSnapshot();

        Assert.Equal(0.1, snapshot.FreeformAxisThreshold, 6);
    }
}
