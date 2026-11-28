// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchValueTextTests
{
    [Theory]
    [InlineData(12.5, "12.5")]
    [InlineData(12.345, "12.35")]
    [InlineData(0, "0")]
    [InlineData(100, "100")]
    public void PercentUsesAtMostTwoDecimals(double value, string expected)
    {
        Assert.Equal(expected, NotchValueText.FormatPercent(value));
    }

    [Theory]
    [InlineData(1.5, "1.5")]
    [InlineData(1.23456, "1.235")]
    [InlineData(0, "0")]
    public void AreaUsesAtMostThreeDecimals(double value, string expected)
    {
        Assert.Equal(expected, NotchValueText.FormatAreaText(value));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AreaShowsDashForNonFiniteValues(double value)
    {
        Assert.Equal("-", NotchValueText.FormatAreaText(value));
    }

    [Fact]
    public void FormattingIgnoresCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Assert.Equal("1.5", NotchValueText.FormatAreaText(1.5));
            Assert.Equal("2.25", NotchValueText.FormatPercent(2.25));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
