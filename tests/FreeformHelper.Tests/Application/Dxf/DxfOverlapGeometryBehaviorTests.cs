// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfOverlapGeometryBehaviorTests
{
    [Fact]
    public void Analyze_ContainedAlignedAndCrossingPads_ReportOverlapInEitherOrder()
    {
        var pairs = new[]
        {
            // Strict containment must work whichever pad is visited first.
            (TestGeometryFactory.CreateRect(0, 0, 10, 10), TestGeometryFactory.CreateRect(2, 2, 4, 4)),
            // All inner vertices lie on outer edges; the interior still has positive area.
            (TestGeometryFactory.CreateRect(0, 0, 10, 10), TestGeometryFactory.CreateRect(0, 0, 10, 5)),
            // Neither vertices nor centroids are inside the other pad; only the edges cross.
            (TestGeometryFactory.CreateRect(0, 0, 12, 2), TestGeometryFactory.CreateRect(1, -8, 3, 6)),
        };

        foreach (var (first, second) in pairs)
        {
            AssertOverlap(AnalyzePair(first, second));
            AssertOverlap(AnalyzePair(second, first));
        }
    }

    [Fact]
    public void Analyze_TouchingOrSeparatedPads_DoNotReportOverlap()
    {
        var square = TestGeometryFactory.CreateRect(0, 0, 4, 4);
        var lowerTriangle = new Polygon2([new Point2(0, 0), new Point2(4, 0), new Point2(0, 4)]);
        var pairs = new[]
        {
            (square, TestGeometryFactory.CreateRect(4, 0, 8, 4)),
            (square, TestGeometryFactory.CreateRect(0, 4, 4, 8)),
            (square, TestGeometryFactory.CreateRect(4, 4, 8, 8)),
            // Shared diagonal, with identical bounding boxes but no common interior.
            (lowerTriangle, new Polygon2([new Point2(4, 4), new Point2(0, 4), new Point2(4, 0)])),
            // Positive bounding-box overlap also occurs when the triangles have a gap.
            (lowerTriangle, new Polygon2([new Point2(4, 4), new Point2(1, 4), new Point2(4, 1)])),
        };

        foreach (var (first, second) in pairs)
        {
            var report = AnalyzePair(first, second);

            Assert.Equal(2, report.PadCount);
            Assert.Equal(0, report.OverlapPairs);
            Assert.False(report.HasIssues);
            Assert.Empty(report.SampleIssues);
        }
    }

    private static DxfOverlapReport AnalyzePair(Polygon2 first, Polygon2 second)
    {
        return DxfOverlapAnalyzer.Analyze(new CadPadSet(new[]
        {
            new CadPad(1, "A", "Copper", first),
            new CadPad(2, "B", "Copper", second),
        }));
    }

    private static void AssertOverlap(DxfOverlapReport report)
    {
        Assert.Equal(2, report.PadCount);
        Assert.Equal(0, report.DuplicateSameLayerPairs);
        Assert.Equal(0, report.DuplicateCrossLayerPairs);
        Assert.Equal(1, report.OverlapPairs);
        Assert.True(report.HasIssues);
        var issue = Assert.Single(report.SampleIssues);
        Assert.Equal(DxfOverlapKind.Overlap, issue.Kind);
        Assert.Equal(1, issue.PadAId);
        Assert.Equal(2, issue.PadBId);
    }
}
