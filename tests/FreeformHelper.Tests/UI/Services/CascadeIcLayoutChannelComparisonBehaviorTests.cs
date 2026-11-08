// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CascadeIcLayoutChannelComparisonBehaviorTests
{
    [Theory]
    [InlineData(4, 7, true)]
    [InlineData(5, 7, false)]
    [InlineData(4, 8, false)]
    public void HasSameChannelsDetectsAnIcChangeOnEitherAxis(int rightX, int rightY, bool expected)
    {
        int[] leftXChannels = [2, 4];
        int[] leftYChannels = [6, 7];
        int[] rightXChannels = [2, rightX];
        int[] rightYChannels = [6, rightY];

        var result = CascadeIcLayoutService.HasSameChannels(
            leftXChannels, leftYChannels, rightXChannels, rightYChannels);

        Assert.Equal(expected, result);
    }
}
