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
}
