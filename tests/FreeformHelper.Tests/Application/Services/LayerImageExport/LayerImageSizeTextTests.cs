// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageSizeTextTests
{
    [Fact]
    public void FormatUsesInvariantThousandsAndPaddedDimensions()
    {
        var size = LayerImageSize.CreateDefault(10, 2, 6480, 848).WithPaddingX(10).WithPaddingY(2);

        Assert.Equal("6,500 × 852 px", LayerImageSizeText.Format(size));
    }

    [Theory]
    [InlineData(0, "0 layers selected")]
    [InlineData(1, "1 layer selected")]
    [InlineData(5, "5 layers selected")]
    public void SelectionTextUsesCorrectPlural(int count, string expected)
    {
        Assert.Equal(expected, LayerImageSizeText.FormatSelection(count));
    }
}
