// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class QuickFocusQueryParserBehaviorTests
{
    [Theory]
    [InlineData(null, "Quick focus: input a CAD id, REG id, or FW diff.")]
    [InlineData(" \t", "Quick focus: input a CAD id, REG id, or FW diff.")]
    [InlineData("#,:", "Quick focus: input a CAD id, REG id, or FW diff.")]
    [InlineData("CAD", "Quick focus: missing numeric id. Use CAD 4809, REG 4616, or DIFF 72.")]
    [InlineData("CAD 2147483648", "Quick focus: missing numeric id. Use CAD 4809, REG 4616, or DIFF 72.")]
    public void TryParse_InvalidQuery_ReturnsUsefulErrorAndNoTarget(string? input, string expectedError)
    {
        var parsed = QuickFocusQueryParser.TryParse(input, out var query, out var error);

        Assert.False(parsed);
        Assert.Equal(default, query);
        Assert.Equal(expectedError, error);
    }

    [Theory]
    [InlineData("C", "Cad")]
    [InlineData("REGULAR", "Regular")]
    [InlineData("R", "Regular")]
    [InlineData("D", "Diff")]
    [InlineData("FW", "Diff")]
    [InlineData("FWIDX", "Diff")]
    [InlineData("FWOUTPUT", "Diff")]
    public void TryParse_TargetAlias_SelectsRequestedKind(string alias, string expectedKind)
    {
        var parsed = QuickFocusQueryParser.TryParse(alias + " 72", out var query, out var error);

        Assert.True(parsed);
        Assert.Equal(expectedKind, query.TargetKind.ToString());
        Assert.Equal(72, query.Value);
        Assert.Null(query.IcIndex);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("REG4616 IC", "Regular", 4616, null)]
    [InlineData("IC0 C4809", "Cad", 4809, 0)]
    [InlineData("IC2 REG4616", "Regular", 4616, 1)]
    [InlineData("CAD2147483647", "Cad", 2_000_000_000, null)]
    [InlineData("unknown 72", "Auto", 72, null)]
    public void TryParse_QualifierAndNumericBoundaries_ReturnsUsableTarget(
        string input, string expectedKind, int expectedValue, int? expectedIc)
    {
        var parsed = QuickFocusQueryParser.TryParse(input, out var query, out var error);

        Assert.True(parsed);
        Assert.Equal(expectedKind, query.TargetKind.ToString());
        Assert.Equal(expectedValue, query.Value);
        Assert.Equal(expectedIc, query.IcIndex);
        Assert.Empty(error);
    }

    [Fact]
    public void TryParse_Characterization_IcFirstWithoutTargetUsesQualifierAsSearchValue()
    {
        var parsed = QuickFocusQueryParser.TryParse("IC2 72", out var query, out var error);

        Assert.True(parsed);
        Assert.Equal(QuickFocusTargetKind.Auto, query.TargetKind);
        Assert.Equal(2, query.Value);
        Assert.Equal(1, query.IcIndex);
        Assert.Empty(error);
    }
}
