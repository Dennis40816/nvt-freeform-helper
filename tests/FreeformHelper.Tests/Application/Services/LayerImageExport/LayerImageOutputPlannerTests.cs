// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class LayerImageOutputPlannerTests
{
    [Fact]
    public void Plan_OneImageReturnsOneJobWithAllLayersInOrder()
    {
        var jobs = LayerImageOutputPlanner.Plan(new List<string> { "second", "first" }, LayerImageOutputMode.OneImage, "panel");

        var job = Assert.Single(jobs);
        Assert.Equal("panel", job.FileStem);
        Assert.Equal(new List<string> { "second", "first" }, job.Layers);
    }

    [Fact]
    public void Plan_OnePerLayerReturnsOneJobPerLayerInOrder()
    {
        var jobs = LayerImageOutputPlanner.Plan(new List<string> { "second", "first" }, LayerImageOutputMode.OnePerLayer, "panel");

        Assert.Equal(new List<string> { "panel_second", "panel_first" }, jobs.Select(job => job.FileStem));
        Assert.Equal(new List<string> { "second" }, jobs[0].Layers);
        Assert.Equal(new List<string> { "first" }, jobs[1].Layers);
    }

    [Theory]
    [InlineData("nc/drawing", "panel_nc_drawing")]
    [InlineData("nc\0drawing", "panel_nc_drawing")]
    [InlineData("nc \t\r\n\u2003drawing", "panel_nc_drawing")]
    [InlineData(" nc drawing ", "panel__nc_drawing_")]
    public void Plan_SanitizesInvalidCharactersAndWhitespaceRuns(string layer, string expected)
    {
        var jobs = LayerImageOutputPlanner.Plan(new[] { layer }, LayerImageOutputMode.OnePerLayer, "panel");

        var job = Assert.Single(jobs);
        Assert.Equal(expected, job.FileStem);
        Assert.Equal(layer, Assert.Single(job.Layers));
    }

    [Fact]
    public void Plan_ReplacesEveryPlatformInvalidFileNameCharacter()
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string layer = string.Join("x", invalidCharacters);
        string sanitized = string.Join("x", Enumerable.Repeat("_", invalidCharacters.Length));

        var jobs = LayerImageOutputPlanner.Plan(new[] { layer }, LayerImageOutputMode.OnePerLayer, "panel");

        Assert.Equal("panel_" + sanitized, Assert.Single(jobs).FileStem);
    }

    [Fact]
    public void Plan_ResolvesSanitizedCaseInsensitiveAndSuffixCollisions()
    {
        var jobs = LayerImageOutputPlanner.Plan(
            new List<string> { "a/b", "a\0b", "A_B", "a_b_2" }, LayerImageOutputMode.OnePerLayer, "panel");

        Assert.Equal(new List<string> { "panel_a_b", "panel_a_b_2", "panel_A_B_3", "panel_a_b_2_2" }, jobs.Select(job => job.FileStem));
    }

    [Theory]
    [InlineData(LayerImageOutputMode.OneImage)]
    [InlineData(LayerImageOutputMode.OnePerLayer)]
    public void Plan_EmptyLayersReturnNoJobs(LayerImageOutputMode mode)
    {
        Assert.Empty(LayerImageOutputPlanner.Plan(Array.Empty<string>(), mode, "panel"));
    }

    [Theory]
    [InlineData(LayerImageOutputMode.OneImage)]
    [InlineData(LayerImageOutputMode.OnePerLayer)]
    public void Plan_OwnsLayerSnapshots(LayerImageOutputMode mode)
    {
        var layers = new List<string> { "original" };
        var jobs = LayerImageOutputPlanner.Plan(layers, mode, "panel");

        layers[0] = "changed";

        Assert.Equal("original", Assert.Single(Assert.Single(jobs).Layers));
    }

    [Fact]
    public void Plan_NullLayersThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("checkedLayers", () =>
            LayerImageOutputPlanner.Plan(null!, LayerImageOutputMode.OneImage, "panel"));
    }

    [Fact]
    public void Plan_NullBaseNameThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("baseName", () =>
            LayerImageOutputPlanner.Plan(new List<string> { "first" }, LayerImageOutputMode.OneImage, null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Plan_BlankBaseNameThrowsArgumentException(string baseName)
    {
        Assert.Throws<ArgumentException>(nameof(baseName), () =>
            LayerImageOutputPlanner.Plan(new List<string> { "first" }, LayerImageOutputMode.OneImage, baseName));
    }

    [Fact]
    public void Plan_UnknownModeThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>("mode", () =>
            LayerImageOutputPlanner.Plan(new List<string> { "first" }, (LayerImageOutputMode)99, "panel"));
    }
}
