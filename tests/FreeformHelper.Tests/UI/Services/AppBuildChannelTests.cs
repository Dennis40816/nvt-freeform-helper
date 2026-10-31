// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AppBuildChannelTests
{
    [Theory]
    [InlineData("1.0.0-g.4", true)]
    [InlineData("1.0.0-g.4+abc123", true)]
    [InlineData("1.0.0", false)]
    [InlineData("1.0.0+abc-123", false)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData(null, true)]
    public void IsDevelopmentVersion_ReadsThePreReleaseSuffix(string? version, bool expected)
    {
        Assert.Equal(expected, AppBuildChannel.IsDevelopmentVersion(version));
    }
}
