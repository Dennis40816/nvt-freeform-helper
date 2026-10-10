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
}
