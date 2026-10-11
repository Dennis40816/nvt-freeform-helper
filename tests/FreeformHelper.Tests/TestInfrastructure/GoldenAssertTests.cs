// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using Xunit.Sdk;

namespace FreeformHelper.Tests;

// The messages of a failed golden comparison reach the log of a public CI run.
public sealed class GoldenAssertTests
{
    private const string ExpectedSecret = "PRIVATE-EXPECTED-7731";
    private const string ActualSecret = "PRIVATE-ACTUAL-4419";

    private static readonly int[] _oneTwoThree = [1, 2, 3];
    private static readonly int[] _oneTwoTwo = [1, 2, 2];
    private static readonly string[] _secretTexts = [ExpectedSecret, ActualSecret];

    [Fact]
    public void TextEqual_WhenTextsAreEqual_DoesNotFail()
    {
        GoldenAssert.TextEqual(ExpectedSecret, ExpectedSecret, "same text");
    }

    [Fact]
    public void TextEqual_WhenTextsDiffer_FailsWithTheLabel()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextEqual(ExpectedSecret, ActualSecret, "exported C V21"));

        Assert.Contains("exported C V21", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextEqual_WhenTextsDiffer_NamesLineAndColumnOfTheFirstDifference()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextEqual("a\nbcd\ne", "a\nbxd\ne", "text"));

        Assert.Contains("line 2, column 2", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextEqual_WhenOneTextIsAPrefixOfTheOther_NamesThePositionAfterTheShorterText()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextEqual("ab", "abcd", "text"));

        Assert.Contains("line 1, column 3", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextEqual_WhenTextsDiffer_FailureMessageHoldsNeitherText()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextEqual(ExpectedSecret, ActualSecret, "text"));

        Assert.DoesNotContain("PRIVATE", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextEqual_WhenTextsDiffer_FailureMessageHoldsNoPartOfEitherText()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextEqual("ALPHA-7731", "BRAVO-4419", "text"));

        Assert.DoesNotContain("ALPHA", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("BRAVO", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("7731", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("4419", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextEqual_WhenTextsDiffer_FailureMessageIsExactlyTheSafeSummary()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextEqual("ALPHA-7731", "BRAVO-4419", "text"));

        Assert.Equal(
            "Golden text differs: text. " +
            "Expected 10 characters, 1 lines, SHA-256 05B1F6E8D19D47047F767583C337BB6AC982CCEAF0C30E186E9487383D723072. " +
            "Actual 10 characters, 1 lines, SHA-256 87902D941C33C5D61E4A1213570977024AEE3A1D770D11949632BF146444F225. " +
            "First difference at line 1, column 1.",
            failure.Message);
    }

    [Fact]
    public void TextEqual_WhenOneTextIsEmpty_NamesTheFirstPosition()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextEqual(string.Empty, "a", "text"));

        Assert.Contains("line 1, column 1", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextEqual_WhenTextsDifferAfterACarriageReturnLineFeed_NamesTheNextLine()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextEqual("a\r\nb", "a\r\nx", "text"));

        Assert.Contains("line 2, column 1", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextEqual_WhenOnlyTheCaseDiffers_Fails()
    {
        Assert.ThrowsAny<XunitException>(() => GoldenAssert.TextEqual("abc", "ABC", "text"));
    }

    [Fact]
    public void SingleMatch_WhenExactlyOneItemMatches_ReturnsIt()
    {
        var item = GoldenAssert.SingleMatch(_oneTwoThree, static value => value == 2, "values");

        Assert.Equal(2, item);
    }

    [Fact]
    public void SingleMatch_WhenNoItemMatches_FailsWithTheLabelAndTheCounts()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.SingleMatch(_oneTwoThree, static value => value == 9, "values"));

        Assert.Contains("values", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Items 3, matches 0", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleMatch_WhenTwoItemsMatch_FailsWithTheLabelAndTheCounts()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.SingleMatch(_oneTwoTwo, static value => value == 2, "values"));

        Assert.Contains("Items 3, matches 2", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleMatch_WhenItMatchesNothing_FailureMessageHoldsNoItemText()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.SingleMatch(_secretTexts, static value => value.Length == 1, "texts"));

        Assert.DoesNotContain("PRIVATE", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleMatch_WhenItMatchesNothing_FailureMessageIsExactlyTheSafeSummary()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.SingleMatch(_secretTexts, static value => value.Length == 1, "texts"));

        Assert.Equal("Golden collection does not hold exactly one match: texts. Items 2, matches 0.", failure.Message);
    }

    [Fact]
    public void TextContains_WhenTheFragmentIsPresent_DoesNotFail()
    {
        GoldenAssert.TextContains("one two three", "two", "text");
    }

    [Fact]
    public void TextContains_WhenTheFragmentIsMissing_FailureMessageHoldsNeitherTextNorFragment()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextContains(ExpectedSecret, ActualSecret, "text"));

        Assert.DoesNotContain("PRIVATE", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextContains_WhenTheFragmentIsMissing_FailureMessageIsExactlyTheSafeSummary()
    {
        var failure = Assert.ThrowsAny<XunitException>(
            () => GoldenAssert.TextContains("one two three", "ZZZ-FRAGMENT", "text"));

        Assert.Equal(
            "Golden text lacks an expected fragment: text. " +
            "Text 13 characters, 1 lines, SHA-256 6899EE404683A14E8C2A03149860DF25D67D34D9CD4DAE7350CBE91E4B3976BE.",
            failure.Message);
    }
}
