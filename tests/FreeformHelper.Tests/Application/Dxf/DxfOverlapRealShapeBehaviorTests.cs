// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfOverlapRealShapeBehaviorTests
{
    [Fact]
    public void Analyze_StaggeredRectanglesOfTheSameHeight_ReportOverlapInEitherOrder()
    {
        // Every vertex of one pad lies on an edge of the other, and neither centroid is inside the other pad.
        // The pads still share the 4 x 4 area between x = 6 and x = 10.
        var left = TestGeometryFactory.CreateRect(0, 0, 10, 4);
        var right = TestGeometryFactory.CreateRect(6, 0, 16, 4);

        AssertSingleOverlap(AnalyzePair(left, right));
        AssertSingleOverlap(AnalyzePair(right, left));
    }

    [Fact]
    public void Analyze_PadInsideTheNotchOfAConcavePad_DoesNotReportOverlap()
    {
        // The U-shaped pad has its centroid at (5, 4.42), inside the notch, where the small pad sits.
        var uShape = new Polygon2(
        [
            new Point2(0, 0), new Point2(10, 0), new Point2(10, 10), new Point2(7, 10),
            new Point2(7, 3), new Point2(3, 3), new Point2(3, 10), new Point2(0, 10),
        ]);
        var padInNotch = TestGeometryFactory.CreateRect(4, 4, 6, 6);

        foreach (var report in new[] { AnalyzePair(uShape, padInNotch), AnalyzePair(padInNotch, uShape) })
        {
            Assert.Equal(2, report.PadCount);
            Assert.Equal(0, report.OverlapPairs);
            Assert.False(report.HasIssues);
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

    private static void AssertSingleOverlap(DxfOverlapReport report)
    {
        Assert.Equal(1, report.OverlapPairs);
        var issue = Assert.Single(report.SampleIssues);
        Assert.Equal(DxfOverlapKind.Overlap, issue.Kind);
    }
}
