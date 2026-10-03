using FreeformHelper.Application.Settings;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingSettingsTests
{
    [Fact]
    public void GetEffectiveCandidateNumber_UsesCandidateNumber_WhenSpecified()
    {
        var settings = new IndexMappingSettings
        {
            CandidatePaddingCells = 3,
            CandidateNumber = 17,
        };

        Assert.Equal(17, settings.GetEffectiveCandidateNumber());
    }

    [Fact]
    public void GetEffectiveCandidateNumber_FallsBackToPadding_WhenCandidateNumberIsZero()
    {
        var settings = new IndexMappingSettings
        {
            CandidatePaddingCells = 2,
            CandidateNumber = 0,
        };

        Assert.Equal(25, settings.GetEffectiveCandidateNumber());
    }

    [Fact]
    public void PaddingFromCandidateNumber_AndCandidateNumberFromPadding_AreCompatible()
    {
        var candidateNumber = IndexMappingSettings.CandidateNumberFromPadding(4);
        var padding = IndexMappingSettings.PaddingFromCandidateNumber(candidateNumber);

        Assert.Equal(81, candidateNumber);
        Assert.Equal(4, padding);
    }

    [Fact]
    public void ValidateOrThrow_Throws_WhenCandidateNumberOutOfRange()
    {
        var settings = new IndexMappingSettings
        {
            CandidateNumber = 401,
        };

        Assert.Throws<InvalidOperationException>(() => settings.ValidateOrThrow());
    }
}
