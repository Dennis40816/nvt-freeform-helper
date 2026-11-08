// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfOverlapCandidateBehaviorTests
{
    [Fact]
    public void Analyze_LargeOutlineAmongSmallPads_FindsEveryContainedPadAfterSpatialFallback()
    {
        using var culture = TestCulture.UseEnUs();
        var pads = Enumerable.Range(0, 64).Select(index =>
        {
            var x = 2 + index % 8 * 10;
            var y = 2 + index / 8 * 10;
            return TestGeometryFactory.CreateCadPad(index + 1, "Copper", x, y, x + 1, y + 1);
        }).ToList();
        pads.Add(TestGeometryFactory.CreateCadPad(1000, "Outline", 0, 0, 100, 100));

        var report = DxfOverlapAnalyzer.Analyze(new CadPadSet(pads), maxSamples: 3);

        Assert.Equal(65, report.PadCount);
        Assert.Equal(64, report.OverlapPairs);
        Assert.Equal(0, report.DuplicateSameLayerPairs);
        Assert.Equal(0, report.DuplicateCrossLayerPairs);
        Assert.True(report.HasIssues);
        Assert.Equal(new[] { (1, 1000), (2, 1000), (3, 1000) },
            report.SampleIssues.Select(issue => (issue.PadAId, issue.PadBId)));
        Assert.All(report.SampleIssues, issue => Assert.Equal(DxfOverlapKind.Overlap, issue.Kind));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    public void Analyze_IsolatedPadsAcrossTheOrigin_ReportNoIssues(int padCount)
    {
        using var culture = TestCulture.UseEnUs();
        var pads = Enumerable.Range(0, padCount)
            .Select(index => TestGeometryFactory.CreateCadPad(
                index + 1, "Copper", index * 3 - 96, -1, index * 3 - 95, 0))
            .ToList();

        var report = DxfOverlapAnalyzer.Analyze(new CadPadSet(pads));

        Assert.Equal(padCount, report.PadCount);
        Assert.Equal(0, report.OverlapPairs);
        Assert.Equal(0, report.DuplicateSameLayerPairs);
        Assert.Equal(0, report.DuplicateCrossLayerPairs);
        Assert.False(report.HasIssues);
        Assert.Empty(report.SampleIssues);
    }
}
