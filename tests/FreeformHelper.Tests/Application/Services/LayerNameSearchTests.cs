using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerNameSearchTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Matches_BlankQuery_MatchesEveryName(string? query)
    {
        Assert.True(LayerNameSearch.Matches("PAD_TOP", query));
    }

    [Theory]
    [InlineData("pad", true)]
    [InlineData("TOP", true)]
    [InlineData("  top  ", true)]
    [InlineData("bottom", false)]
    public void Matches_SingleTerm_UsesCaseInsensitiveContains(string query, bool expected)
    {
        Assert.Equal(expected, LayerNameSearch.Matches("Pad_Top", query));
    }

    [Theory]
    [InlineData("pad top", true)]
    [InlineData("top pad", true)]
    [InlineData("pad bottom", false)]
    public void Matches_SeveralTerms_RequiresEveryTerm(string query, bool expected)
    {
        Assert.Equal(expected, LayerNameSearch.Matches("PAD_TOP", query));
    }
}
