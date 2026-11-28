// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePixelSizePolicyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(0.4, 1)]
    [InlineData(1.4, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.5, 3)]
    [InlineData(1000000, 1000000)]
    public void NormalizeRoundsAwayFromZeroAndKeepsAtLeastOne(double input, double expected)
    {
        Assert.Equal((decimal)expected, CoordinatePixelSizePolicy.Normalize((decimal)input));
    }
}
