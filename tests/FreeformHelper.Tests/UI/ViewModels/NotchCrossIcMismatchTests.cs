// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchCrossIcMismatchTests
{
    [Fact]
    public void SnapshotWithoutCrossIcMatchesHasZeroCountsAndEmptyText()
    {
        var grid = CreateGrid((101, 0), (101, 0), (102, 1), (null, 1));

        Assert.Equal(new CrossIcMismatchSnapshot(0, 0, string.Empty), NotchCrossIcMismatch.BuildCrossIcMismatchSnapshot(grid));
    }

    [Fact]
    public void SnapshotCountsTwoRegularPadsForOneCadAcrossTwoIcs()
    {
        var grid = CreateGrid((101, 1), (101, 0));

        Assert.Equal(new CrossIcMismatchSnapshot(1, 2, "CAD101(IC:1/2)"), NotchCrossIcMismatch.BuildCrossIcMismatchSnapshot(grid));
    }

    [Fact]
    public void SnapshotSortsCadSamplesAndLimitsThemToSixWithOverflowSuffix()
    {
        var grid = CreateGrid(
            (108, 1), (108, 0), (107, 1), (107, 0),
            (106, 1), (106, 0), (105, 1), (105, 0),
            (104, 1), (104, 0), (103, 1), (103, 0),
            (102, 1), (102, 0), (101, 1), (101, 0));

        Assert.Equal(
            new CrossIcMismatchSnapshot(8, 16,
                "CAD101(IC:1/2), CAD102(IC:1/2), CAD103(IC:1/2), CAD104(IC:1/2), CAD105(IC:1/2), CAD106(IC:1/2)+2"),
            NotchCrossIcMismatch.BuildCrossIcMismatchSnapshot(grid));
    }

    [Fact]
    public void SnapshotRejectsNullGrid()
    {
        Assert.Throws<ArgumentNullException>("grid", () => NotchCrossIcMismatch.BuildCrossIcMismatchSnapshot(null!));
    }

    private static RegularGrid CreateGrid(params (int? CadId, int IcIndex)[] matches)
    {
        var pads = new List<RegularPad>();
        for (var index = 0; index < matches.Length; index++)
        {
            var polygon = new Polygon2(
            [
                new Point2(index, 0), new Point2(index + 1, 0),
                new Point2(index + 1, 1), new Point2(index, 1),
            ]);
            var pad = new RegularPad(0, index, index, polygon)
            {
                MatchedCadPadId = matches[index].CadId,
            };
            pad.AssignMapping(matches[index].IcIndex, index);
            pads.Add(pad);
        }

        var xEdges = Enumerable.Range(0, matches.Length + 1).Select(index => (double)index).ToArray();
        return new RegularGrid(1, matches.Length, xEdges, [0, 1], pads);
    }
}
