using System.Globalization;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RuntimeQueryCharacterizationArgumentTests
{
    [Theory]
    [InlineData("-10", true, -10, null)]
    [InlineData("10", true, 10, null)]
    [InlineData("0", true, 0, null)]
    [InlineData(" +5 ", true, 5, null)]
    [InlineData("-11", false, 0, "Argument '--value' must be in [-10, 10].")]
    [InlineData("11", false, 0, "Argument '--value' must be in [-10, 10].")]
    [InlineData("abc", false, 0, "Argument '--value' must be an integer.")]
    [InlineData("5.0", false, 0, "Argument '--value' must be an integer.")]
    [InlineData("1,000", false, 0, "Argument '--value' must be an integer.")]
    [InlineData("2147483648", false, 0, "Argument '--value' must be an integer.")]
    public void Integer_UsesInvariantIntegerSyntaxAndInclusiveRange(string text, bool found, int value, string? message)
    {
        var result = RuntimeQueryCharacterizationSubject.IntArg(Args(text), "value", -10, 10);

        Assert.Equal(found, result.Found);
        Assert.Equal(value, result.Value);
        AssertError(result.Error, message);
    }

    [Theory]
    [InlineData("0", true, 0.0, null)]
    [InlineData("2000", true, 2000.0, null)]
    [InlineData(" +1.25 ", true, 1.25, null)]
    [InlineData("1e3", true, 1000.0, null)]
    [InlineData("1,234.5", true, 1234.5, null)]
    [InlineData("1,5", true, 15.0, null)]
    [InlineData("-0.01", false, 0.0, "Argument '--value' must be in [0, 2000].")]
    [InlineData("2000.01", false, 0.0, "Argument '--value' must be in [0, 2000].")]
    [InlineData("NaN", false, 0.0, "Argument '--value' must be in [0, 2000].")]
    [InlineData("Infinity", false, 0.0, "Argument '--value' must be in [0, 2000].")]
    [InlineData("-Infinity", false, 0.0, "Argument '--value' must be in [0, 2000].")]
    [InlineData("1e999", false, 0.0, "Argument '--value' must be in [0, 2000].")]
    [InlineData("abc", false, 0.0, "Argument '--value' must be numeric.")]
    [InlineData("1 234.5", false, 0.0, "Argument '--value' must be numeric.")]
    public void Double_UsesFloatSyntaxThousandsSeparatorsAndFiniteInclusiveRange(
        string text, bool found, double value, string? message)
    {
        var result = RuntimeQueryCharacterizationSubject.DoubleArg(Args(text), "value", 0, 2000);

        Assert.Equal(found, result.Found);
        Assert.Equal(value, result.Value);
        AssertError(result.Error, message);
    }

    [Theory]
    [InlineData("en-US", "1,234.5", 1234.5)]
    [InlineData("fr-FR", "1,234.5", 1234.5)]
    [InlineData("de-DE", "1.5", 1.5)]
    [InlineData("fr-FR", "1,5", 15.0)]
    public void Double_ParsesInvariantlyRegardlessOfCurrentCulture(string culture, string text, double expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var result = RuntimeQueryCharacterizationSubject.DoubleArg(Args(text), "value", 0, 2000);

            Assert.True(result.Found);
            Assert.Equal(expected, result.Value);
            Assert.Null(result.Error);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Double_RangeErrorFormatsLimitsUsingCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var result = RuntimeQueryCharacterizationSubject.DoubleArg(Args("3.0"), "value", 1.5, 2.5);

            Assert.False(result.Found);
            Assert.Equal(0.0, result.Value);
            AssertError(result.Error, "Argument '--value' must be in [1,5, 2,5].");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    public static TheoryData<string, int[]> IntegerLists => new()
    {
        { "0,10", [0, 10] },
        { " 0 , 5 , 10 ", [0, 5, 10] },
        { ",,0,,5,,", [0, 5] },
        { "0, ,5,\t,10", [0, 5, 10] },
        { "5,5,0", [5, 5, 0] },
        { "+5", [5] }
    };

    [Theory]
    [MemberData(nameof(IntegerLists))]
    public void IntegerList_TrimsItemsDropsEmptyItemsAndPreservesOrderAndDuplicates(string text, int[] expected)
    {
        var result = RuntimeQueryCharacterizationSubject.IntListArg(Args(text), "value", 0, 10);

        Assert.True(result.Found);
        Assert.Equal(expected, result.Value);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(", , ,", "Argument '--value' cannot be empty.")]
    [InlineData("5,abc", "Argument '--value' must be a comma-separated integer list.")]
    [InlineData("5,1.5", "Argument '--value' must be a comma-separated integer list.")]
    [InlineData("5,2147483648", "Argument '--value' must be a comma-separated integer list.")]
    [InlineData("5;6", "Argument '--value' must be a comma-separated integer list.")]
    [InlineData("5,-1", "Argument '--value' values must be in [0, 10].")]
    [InlineData("5,11", "Argument '--value' values must be in [0, 10].")]
    public void IntegerList_ErrorDiscardsAnyAlreadyParsedItems(string text, string message)
    {
        var result = RuntimeQueryCharacterizationSubject.IntListArg(Args(text), "value", 0, 10);

        Assert.False(result.Found);
        Assert.Empty(result.Value);
        AssertError(result.Error, message);
    }

    [Theory]
    [InlineData("text", "text")]
    [InlineData(" \t text \r\n", "text")]
    [InlineData("  two  words  ", "two  words")]
    public void String_TrimsOnlyOuterWhitespace(string text, string expected)
    {
        var result = RuntimeQueryCharacterizationSubject.StringArg(Args(text), "value");

        Assert.True(result.Found);
        Assert.Equal(expected, result.Value);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("TrUe", true)]
    [InlineData("1", true)]
    [InlineData("on", true)]
    [InlineData("ON", true)]
    [InlineData("yes", true)]
    [InlineData("YES", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("FaLsE", false)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    [InlineData("OFF", false)]
    [InlineData("no", false)]
    [InlineData("NO", false)]
    [InlineData(" \t yes \r\n", true)]
    [InlineData(" no ", false)]
    public void Boolean_AcceptsAllCurrentSpellingsWithoutCaseOrOuterWhitespace(string text, bool expected)
    {
        var result = RuntimeQueryCharacterizationSubject.BoolArg(Args(text), "value");

        Assert.True(result.Found);
        Assert.Equal(expected, result.Value);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("enabled")]
    [InlineData("t")]
    [InlineData("y")]
    public void Boolean_RejectsOtherSpellingsWithExactMessage(string text)
    {
        var result = RuntimeQueryCharacterizationSubject.BoolArg(Args(text), "value");

        Assert.False(result.Found);
        Assert.False(result.Value);
        AssertError(result.Error, "Argument '--value' must be true/false (or 1/0, on/off).");
    }

    public static TheoryData<IReadOnlyDictionary<string, string>?> MissingArguments => new()
    {
        (IReadOnlyDictionary<string, string>?)null,
        new Dictionary<string, string>(),
        new Dictionary<string, string> { ["other"] = "5" },
        Args(""),
        Args(" \t\r\n")
    };

    [Theory]
    [MemberData(nameof(MissingArguments))]
    public void MissingOrBlankArgument_ReturnsFalseDefaultsAndNoErrorForEveryHelper(IReadOnlyDictionary<string, string>? args)
    {
        var integer = RuntimeQueryCharacterizationSubject.IntArg(args, "value", 1, 10);
        var list = RuntimeQueryCharacterizationSubject.IntListArg(args, "value", 1, 10);
        var number = RuntimeQueryCharacterizationSubject.DoubleArg(args, "value", 1, 10);
        var text = RuntimeQueryCharacterizationSubject.StringArg(args, "value");
        var boolean = RuntimeQueryCharacterizationSubject.BoolArg(args, "value");

        Assert.False(integer.Found);
        Assert.Equal(0, integer.Value);
        Assert.Null(integer.Error);
        Assert.False(list.Found);
        Assert.Empty(list.Value);
        Assert.Null(list.Error);
        Assert.False(number.Found);
        Assert.Equal(0.0, number.Value);
        Assert.Null(number.Error);
        Assert.False(text.Found);
        Assert.Equal(string.Empty, text.Value);
        Assert.Null(text.Error);
        Assert.False(boolean.Found);
        Assert.False(boolean.Value);
        Assert.Null(boolean.Error);
    }

    private static Dictionary<string, string> Args(string text) => new() { ["value"] = text };

    private static void AssertError(RuntimeQueryCharacterizationSubject.Response? error, string? message)
    {
        if (message is null)
        {
            Assert.Null(error);
            return;
        }

        Assert.NotNull(error);
        Assert.False(error.Ok);
        Assert.Equal("INVALID_ARGUMENTS", error.ErrorCode);
        Assert.Equal(message, error.ErrorMessage);
    }
}
