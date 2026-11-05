// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerListSearchTests
{
    [Fact]
    public void ParseTerms_SplitsAnyWhitespaceAndDropsEmptyTerms()
    {
        var terms = LayerListSearch.ParseTerms(" \tNC\r\ndra\u2003pad  ");

        Assert.Equal(new List<string> { "NC", "dra", "pad" }, terms);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n\u2003 ")]
    public void Matches_EmptyOrWhitespaceQueryMatchesAll(string? query)
    {
        var terms = LayerListSearch.ParseTerms(query);

        Assert.Empty(terms);
        Assert.True(LayerListSearch.Matches("nc.drawing", terms));
        Assert.True(LayerListSearch.Matches(string.Empty, terms));
    }

    [Theory]
    [InlineData("NC DRAW", true)]
    [InlineData("dra nc", true)]
    [InlineData("NC pad", false)]
    [InlineData("pad dra", false)]
    public void Matches_RequiresEveryTermIgnoringCase(string query, bool expected)
    {
        Assert.Equal(expected, LayerListSearch.Matches("nc.drawing", LayerListSearch.ParseTerms(query)));
    }

    [Fact]
    public void FindRanges_ReturnsSortedRangesForNcDrawing()
    {
        var ranges = LayerListSearch.FindRanges("nc.drawing", LayerListSearch.ParseTerms("NC dra"));

        Assert.Equal(new[] { new LayerTextRange(0, 2), new LayerTextRange(3, 3) }, ranges);
    }

    [Fact]
    public void FindRanges_MergesOverlappingTerms()
    {
        var ranges = LayerListSearch.FindRanges("nc.drawing", LayerListSearch.ParseTerms("dra draw"));

        Assert.Equal(new[] { new LayerTextRange(3, 4) }, ranges);
    }

    [Fact]
    public void FindRanges_ReturnsAllOccurrencesSortedRegardlessOfTermOrder()
    {
        var ranges = LayerListSearch.FindRanges("NC.dra.nc", LayerListSearch.ParseTerms("dra nc"));

        Assert.Equal(new[] { new LayerTextRange(0, 2), new LayerTextRange(3, 3), new LayerTextRange(7, 2) }, ranges);
    }

    [Fact]
    public void FindRanges_MergesOverlappingOccurrencesOfTheSameTerm()
    {
        var ranges = LayerListSearch.FindRanges("banana", LayerListSearch.ParseTerms("ana"));

        Assert.Equal(new[] { new LayerTextRange(1, 5) }, ranges);
    }

    [Fact]
    public void FindRanges_MergesAdjacentAndDuplicateTerms()
    {
        var ranges = LayerListSearch.FindRanges("drawing", LayerListSearch.ParseTerms("dra wing dra"));

        Assert.Equal(new[] { new LayerTextRange(0, 7) }, ranges);
    }

    [Fact]
    public void FindRanges_ReturnsEmptyForNoMatch()
    {
        Assert.Empty(LayerListSearch.FindRanges("nc.drawing", LayerListSearch.ParseTerms("pad")));
    }

    [Fact]
    public void FindRanges_ReturnsEmptyForEmptyTerms()
    {
        Assert.Empty(LayerListSearch.FindRanges("nc.drawing", new[] { string.Empty }));
    }
}
