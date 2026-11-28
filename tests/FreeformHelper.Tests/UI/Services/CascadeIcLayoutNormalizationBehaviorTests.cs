// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CascadeIcLayoutNormalizationBehaviorTests
{
    private static readonly int[] _expectedXChannels = [16, 16];
    private static readonly int[] _expectedYChannels = [10, 10];

    [Fact]
    public void Build_MissingIcDetails_UsesFallbackChannelsForEachIc()
    {
        var layout = CascadeIcLayoutService.Build(2, 16, 10, null, Array.Empty<int>());

        Assert.Equal(
            new[] { new CascadeIcLayoutRow(1, 16, 10), new CascadeIcLayoutRow(2, 16, 10) },
            layout.Rows);
        Assert.Equal(_expectedXChannels, layout.PerIcXChannels);
        Assert.Equal(_expectedYChannels, layout.PerIcYChannels);
        Assert.Equal(32, layout.TotalXChannels);
        Assert.Equal(10, layout.TotalYChannels);
    }

    [Fact]
    public void HasSameChannels_ComparesMissingAndNonPositiveDetailsByTheirNormalizedValues()
    {
        Assert.True(CascadeIcLayoutService.HasSameChannels(null, [], [], null));
        Assert.False(CascadeIcLayoutService.HasSameChannels(null, [8], [1], [8]));
        Assert.False(CascadeIcLayoutService.HasSameChannels([16], [8], [16], null));
        Assert.True(CascadeIcLayoutService.HasSameChannels([0, 16], [10, -2], [1, 16], [10, 1]));
    }
}
