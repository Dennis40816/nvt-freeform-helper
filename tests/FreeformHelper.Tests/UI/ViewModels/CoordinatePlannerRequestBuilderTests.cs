// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using GuideGenerationMode = FreeformHelper.UI.ViewModels.CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerRequestBuilderTests
{
    [Fact]
    public void BuildPreservesAllValuesAndAppliesOnlyTheExistingConversions()
    {
        var geometry = new CoordinatePlannerGeometryInput(-1.25m, 2.5m, -3m, 0m, 2.5m, -1.5m, -2m, false);
        var horizontal = new CoordinatePlannerGuideInput(2.5m, GuideGenerationMode.Pitch, -1m, 3.25m, "bad;5 -2 5");
        var vertical = new CoordinatePlannerGuideInput(-0.5m, GuideGenerationMode.ExplicitPositions, 7.5m, -2m, "8,4");
        var array = new CoordinatePlannerArrayInput(true, 2.5m, -0.5m, -1.25m, 2.5m, 30.5m, -4.5m, 50.5m, 60.5m, -7.5m, 80.5m);
        CoordinateCustomPointRequest[] points = [new("p", "Probe", -2d, 3d)];
        CoordinateCustomPathRequest[] paths = [new("s", "Sweep", 1d, 2d, 3d, 4d, 3)];

        var request = CoordinatePlannerRequestBuilder.Build(geometry, horizontal, vertical, array, points, paths);

        var expected = new CoordinatePlannerRequest(
            -1.25d, 2.5d, 0.001d, 0.001d, 3, 1, 3, 0, 0d, false, true, 3, 0,
            -1.25d, 2.5d, 30.5d, -4.5d, 50.5d, 60.5d, -7.5d, 80.5d,
            CoordinateGuideGenerationMode.Pitch, CoordinateGuideGenerationMode.ExplicitPositions, 0d, 7.5d, 3.25d, 0d);
        Assert.Equal(expected, request with
        {
            HorizontalGuidePositions = null,
            VerticalGuidePositions = null,
            CustomPoints = null,
            CustomPaths = null,
        });
        Assert.Equal([5d, 0d, 5d], request.HorizontalGuidePositions);
        Assert.Equal([8d, 4d], request.VerticalGuidePositions);
        Assert.Same(points, request.CustomPoints);
        Assert.Same(paths, request.CustomPaths);
    }

    [Fact]
    public void BuildKeepsPositiveGeometryAndZeroCounts()
    {
        var geometry = new CoordinatePlannerGeometryInput(1m, -2m, 100.5m, 50.25m, 720.5m, 1280.5m, 10.5m, true);
        var guide = new CoordinatePlannerGuideInput(0m, GuideGenerationMode.Count, 0m, 0m, string.Empty);

        var request = CoordinatePlannerRequestBuilder.Build(geometry, guide, guide, CreateArray(), [], []);

        Assert.Equal(new CoordinatePlannerRequest(
            1d, -2d, 100.5d, 50.25d, 721, 1281, 0, 0, 10.5d, true, false, 0, 0,
            0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d), request with
            {
                HorizontalGuidePositions = null,
                VerticalGuidePositions = null,
                CustomPoints = null,
                CustomPaths = null,
            });
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<double>>(request.HorizontalGuidePositions));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<double>>(request.VerticalGuidePositions));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<CoordinateCustomPointRequest>>(request.CustomPoints));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<CoordinateCustomPathRequest>>(request.CustomPaths));
    }

    [Fact]
    public void BuildKeepsCastOverflowObservableForPixelsAndCounts()
    {
        var geometry = new CoordinatePlannerGeometryInput(0m, 0m, 100m, 50m, 1000m, 500m, 0m, true);
        var guide = new CoordinatePlannerGuideInput(0m, GuideGenerationMode.Count, 0m, 0m, string.Empty);
        var array = CreateArray();

        Assert.Throws<OverflowException>(() => CoordinatePlannerRequestBuilder.Build(
            geometry with { PixelWidth = decimal.MaxValue }, guide, guide, array, [], []));
        Assert.Throws<OverflowException>(() => CoordinatePlannerRequestBuilder.Build(
            geometry, guide with { Count = decimal.MinValue }, guide, array, [], []));
    }

    [Theory]
    [InlineData(100d, 50d, 1000, 500, 100d, 50d, 1000, 500)]
    [InlineData(-1d, 0d, -2, 0, 0.001d, 0.001d, 1, 1)]
    [InlineData(0.0004d, 0.0002d, 0, -1, 0.001d, 0.001d, 1, 1)]
    public void FromDefaultsPreservesTheConstructorRequestAndOptionalDefaults(
        double width, double height, int pixelWidth, int pixelHeight,
        double expectedWidth, double expectedHeight, int expectedPixelWidth, int expectedPixelHeight)
    {
        var request = CoordinatePlannerRequestBuilder.FromDefaults(width, height, pixelWidth, pixelHeight);

        Assert.Equal(new CoordinatePlannerRequest(
            0d, 0d, expectedWidth, expectedHeight, expectedPixelWidth, expectedPixelHeight, 5, 5, 0d, true, false, 5, 5,
            0d, 0d, expectedWidth, 0d, expectedWidth, expectedHeight, 0d, expectedHeight), request);
    }

    [Theory]
    [InlineData(GuideGenerationMode.Count, CoordinateGuideGenerationMode.Count)]
    [InlineData(GuideGenerationMode.Pitch, CoordinateGuideGenerationMode.Pitch)]
    [InlineData(GuideGenerationMode.ExplicitPositions, CoordinateGuideGenerationMode.ExplicitPositions)]
    [InlineData((GuideGenerationMode)99, CoordinateGuideGenerationMode.Count)]
    public void GuideModeConversionPreservesTheCountFallback(GuideGenerationMode mode, CoordinateGuideGenerationMode expected)
    {
        Assert.Equal(expected, CoordinatePlannerRequestBuilder.ToApplicationGuideMode(mode));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    [InlineData("bad ; + . 1x")]
    public void ParsePositionsIgnoresEmptyAndMalformedTokens(string text)
    {
        Assert.Empty(CoordinatePlannerRequestBuilder.ParsePositions(text));
    }

    [Fact]
    public void ParsePositionsKeepsOrderDuplicatesAndOutOfRangeValuesWithInvariantNumbers()
    {
        using var culture = TestCulture.UseEnUs();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

        var positions = CoordinatePlannerRequestBuilder.ParsePositions("25;bad\t-5\r\n12.5,25 100 1e1 0");

        Assert.Equal([25d, 0d, 12.5d, 25d, 100d, 10d, 0d], positions);
    }

    [Fact]
    public void ParsePositionsPreservesNonFiniteDoubleParsing()
    {
        var positions = CoordinatePlannerRequestBuilder.ParsePositions("NaN Infinity -Infinity 1e999");

        Assert.Equal([double.NaN, double.PositiveInfinity, 0d, double.PositiveInfinity], positions);
    }

    [Theory]
    [InlineData(-2147483648d, 1, 0)]
    [InlineData(-1.5d, 1, 0)]
    [InlineData(-0.5d, 1, 0)]
    [InlineData(0d, 1, 0)]
    [InlineData(0.49d, 1, 0)]
    [InlineData(0.5d, 1, 1)]
    [InlineData(1.5d, 2, 2)]
    [InlineData(2.5d, 3, 3)]
    [InlineData(2147483647d, int.MaxValue, int.MaxValue)]
    public void IntegerHelpersRoundAwayFromZeroThenApplyTheirDifferentMinimums(
        double value, int positive, int nonNegative)
    {
        Assert.Equal(positive, CoordinatePlannerRequestBuilder.PositiveInt((decimal)value));
        Assert.Equal(nonNegative, CoordinatePlannerRequestBuilder.NonNegativeInt((decimal)value));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IntegerHelpersPreserveOverflowBeforeClamping(bool positive)
    {
        var value = positive ? decimal.MaxValue : decimal.MinValue;

        Assert.Throws<OverflowException>(() => CoordinatePlannerRequestBuilder.PositiveInt(value));
        Assert.Throws<OverflowException>(() => CoordinatePlannerRequestBuilder.NonNegativeInt(value));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IntegerHelpersPreserveOverflowWhenRoundingCrossesTheIntBoundary(bool positive)
    {
        var value = positive ? int.MaxValue + 0.5m : int.MinValue - 0.5m;

        Assert.Throws<OverflowException>(() => CoordinatePlannerRequestBuilder.PositiveInt(value));
        Assert.Throws<OverflowException>(() => CoordinatePlannerRequestBuilder.NonNegativeInt(value));
    }

    private static CoordinatePlannerArrayInput CreateArray() => new(false, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
}
