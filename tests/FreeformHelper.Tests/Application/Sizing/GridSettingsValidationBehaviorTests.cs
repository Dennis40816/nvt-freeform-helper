// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Settings;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class GridSettingsValidationBehaviorTests
{
    [Fact]
    public void ValidateOrThrow_NonPositiveChannelCount_RejectsEitherAxis()
    {
        (int X, int Y)[] channelCounts = [(0, 2), (-1, 2), (2, 0), (2, -1)];

        foreach (var (x, y) in channelCounts)
        {
            var settings = new GridSettings { XChannels = x, YChannels = y };

            var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

            Assert.Equal("XChannels/YChannels must be positive.", error.Message);
        }
    }

    [Fact]
    public void ValidateOrThrow_ChannelCountAboveConfiguredLimit_RejectsEitherAxis()
    {
        (int X, int Y)[] channelCounts = [(3, 2), (2, 3)];

        foreach (var (x, y) in channelCounts)
        {
            var settings = new GridSettings
            {
                XChannels = x,
                YChannels = y,
                MaxChannelsX = 2,
                MaxChannelsY = 2,
            };

            var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

            Assert.Equal("Channels exceed configured max limit.", error.Message);
        }
    }

    [Fact]
    public void ValidateOrThrow_NonPositiveCascadeCount_RejectsMissingIcAllocation()
    {
        int[] cascadeCounts = [0, -1];

        foreach (var cascadeCount in cascadeCounts)
        {
            var settings = new GridSettings { CascadeNum = cascadeCount };

            var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

            Assert.Equal("CascadeNum must be positive.", error.Message);
        }
    }

    [Fact]
    public void ValidateOrThrow_NegativeActiveArea_RejectsEitherDimension()
    {
        (double Width, double Height)[] activeAreas = [(-0.5, 10), (10, -0.5)];

        foreach (var (width, height) in activeAreas)
        {
            var settings = new GridSettings
            {
                ActiveAreaWidth = width,
                ActiveAreaHeight = height,
            };

            var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

            Assert.Equal("Active area must be non-negative.", error.Message);
        }
    }

    [Fact]
    public void ValidateOrThrow_NegativeAfeOverlap_RejectsTheSetting()
    {
        var settings = new GridSettings { AfeOverlap = -1 };

        var error = Assert.Throws<InvalidOperationException>(settings.ValidateOrThrow);

        Assert.Equal("AfeOverlap must be non-negative.", error.Message);
    }

    [Fact]
    public void ValidateOrThrow_InclusiveBoundaries_AcceptsOneChannelPerAxisAndUnspecifiedActiveArea()
    {
        var settings = new GridSettings
        {
            XChannels = 1,
            YChannels = 1,
            MaxChannelsX = 1,
            MaxChannelsY = 1,
            CascadeNum = 1,
            ActiveAreaWidth = 0,
            ActiveAreaHeight = 0,
            AfeOverlap = 0,
        };

        var error = Record.Exception(settings.ValidateOrThrow);

        Assert.Null(error);
    }
}
