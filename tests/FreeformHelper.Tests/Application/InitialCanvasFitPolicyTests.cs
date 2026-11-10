// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Verifies the layout readiness and initial fit decisions without UI dependencies.</summary>
public sealed class InitialCanvasFitPolicyTests
{
    [Theory]
    [InlineData(false, false, 800, 600, true)]
    [InlineData(false, true, 800, 600, true)]
    [InlineData(true, false, 800, 600, false)]
    [InlineData(true, true, 800, 600, true)]
    [InlineData(false, false, 0, 600, false)]
    [InlineData(false, false, -1, 600, false)]
    [InlineData(false, false, 800, 0, false)]
    [InlineData(false, false, 800, -1, false)]
    [InlineData(true, true, 0, 600, false)]
    [InlineData(true, true, -1, 600, false)]
    [InlineData(true, true, 800, 0, false)]
    [InlineData(true, true, 800, -1, false)]
    public void IsLayoutReadyRequiresExpectedMaximizationAndUsableBounds(
        bool expectedMaximized,
        bool currentMaximized,
        double width,
        double height,
        bool expected)
    {
        var actual = InitialCanvasFitPolicy.IsLayoutReady(expectedMaximized, currentMaximized, width, height);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false, false, false, InitialFitAction.None)]
    [InlineData(false, true, false, InitialFitAction.None)]
    [InlineData(false, false, true, InitialFitAction.None)]
    [InlineData(false, true, true, InitialFitAction.None)]
    [InlineData(true, true, false, InitialFitAction.FitNow)]
    [InlineData(true, false, true, InitialFitAction.FitNow)]
    [InlineData(true, true, true, InitialFitAction.FitNow)]
    [InlineData(true, false, false, InitialFitAction.FitAfterNextBuild)]
    public void DecideUsesHostAvailabilityBeforePadPresence(
        bool hasCanvasHost,
        bool hasCadPads,
        bool hasRegularPads,
        InitialFitAction expected)
    {
        var actual = InitialCanvasFitPolicy.Decide(hasCanvasHost, hasCadPads, hasRegularPads);

        Assert.Equal(expected, actual);
    }
}
