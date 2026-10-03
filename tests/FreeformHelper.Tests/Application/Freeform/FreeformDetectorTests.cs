using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class FreeformDetectorTests
{
    [Fact]
    public void AutoTagFreeforms_MarksXWay_WhenCadSpreadIsDominantOnX()
    {
        var grid = BuildGrid(rows: 1, cols: 3, width: 3, height: 1);
        var cad = BuildCadPadSet(id: 1001, minX: 0.0, minY: 0.0, maxX: 3.0, maxY: 1.0);
        var matchResult = PadMatcher.Match(cad, grid, new MatchingSettings());

        FreeformDetector.AutoTagFreeforms(
            cad,
            grid,
            new MatchingSettings { FreeformAxisThreshold = 0.5 },
            matchResult);

        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.XWay, pad.Freeform));
    }

    [Fact]
    public void AutoTagFreeforms_MarksYWay_WhenCadSpreadIsDominantOnY()
    {
        var grid = BuildGrid(rows: 3, cols: 1, width: 1, height: 3);
        var cad = BuildCadPadSet(id: 2001, minX: 0.0, minY: 0.0, maxX: 1.0, maxY: 3.0);
        var matchResult = PadMatcher.Match(cad, grid, new MatchingSettings());

        FreeformDetector.AutoTagFreeforms(
            cad,
            grid,
            new MatchingSettings { FreeformAxisThreshold = 0.5 },
            matchResult);

        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.YWay, pad.Freeform));
    }

    [Fact]
    public void AutoTagFreeforms_LeavesNone_WhenDominantSpreadBelowThreshold()
    {
        var grid = BuildGrid(rows: 1, cols: 2, width: 2, height: 1);
        var cad = BuildCadPadSet(id: 3001, minX: 0.0, minY: 0.0, maxX: 1.1, maxY: 1.0);
        var matchResult = PadMatcher.Match(cad, grid, new MatchingSettings());

        FreeformDetector.AutoTagFreeforms(
            cad,
            grid,
            new MatchingSettings { FreeformAxisThreshold = 0.2 },
            matchResult);

        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.None, pad.Freeform));
    }

    [Fact]
    public void AutoTagFreeforms_AllowsXY_WhenOptionEnabled()
    {
        var grid = BuildGrid(rows: 2, cols: 2, width: 2, height: 2);
        var cad = BuildCadPadSet(id: 4001, minX: 0.0, minY: 0.0, maxX: 2.0, maxY: 2.0);
        var matchResult = PadMatcher.Match(cad, grid, new MatchingSettings());

        FreeformDetector.AutoTagFreeforms(
            cad,
            grid,
            new MatchingSettings
            {
                FreeformAxisThreshold = 0.4,
                EnableAutoDetectXy = true,
            },
            matchResult);

        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.XYWay, pad.Freeform));
    }

    [Fact]
    public void DetectAssignments_MatchesAutoTagFreeformsResult()
    {
        var grid = BuildGrid(rows: 2, cols: 2, width: 2, height: 2);
        var cad = BuildCadPadSet(id: 4101, minX: 0.0, minY: 0.0, maxX: 2.0, maxY: 2.0);
        var settings = new MatchingSettings
        {
            FreeformAxisThreshold = 0.4,
            EnableAutoDetectXy = true,
        };
        var matchResult = PadMatcher.Match(cad, grid, new MatchingSettings());

        var assignments = FreeformDetector.DetectAssignments(cad, grid, settings, matchResult);

        Assert.Equal(grid.Pads.Count, assignments.Count);
        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.None, pad.Freeform));

        FreeformDetector.ApplyAssignments(grid, assignments);

        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.XYWay, pad.Freeform));
    }

    [Fact]
    public void AutoTagFreeforms_LeavesNoneForBoundaryTinySpill_WhenEdgeSpecializationDisabled()
    {
        var grid = BuildGrid(rows: 1, cols: 3, width: 3, height: 1);
        var cad = BuildCadPadSet(id: 5001, minX: 0.9, minY: 0.0, maxX: 1.3, maxY: 1.0);
        var matchResult = PadMatcher.Match(cad, grid, new MatchingSettings());

        FreeformDetector.AutoTagFreeforms(
            cad,
            grid,
            new MatchingSettings
            {
                FreeformAxisThreshold = 0.3,
                EnableFreeformEdgeSpecialization = false,
            },
            matchResult);

        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.None, pad.Freeform));
    }

    [Fact]
    public void AutoTagFreeforms_EnablesBoundaryTinySpill_WhenEdgeSpecializationEnabled()
    {
        var grid = BuildGrid(rows: 1, cols: 3, width: 3, height: 1);
        var cad = BuildCadPadSet(id: 5002, minX: 0.9, minY: 0.0, maxX: 1.3, maxY: 1.0);
        var matchResult = PadMatcher.Match(cad, grid, new MatchingSettings());

        FreeformDetector.AutoTagFreeforms(
            cad,
            grid,
            new MatchingSettings
            {
                FreeformAxisThreshold = 0.3,
                EnableFreeformEdgeSpecialization = true,
            },
            matchResult);

        var taggedPads = grid.Pads
            .Where(pad => pad.Freeform != FreeformType.None)
            .ToList();

        Assert.Equal(2, taggedPads.Count);
        Assert.All(taggedPads, pad => Assert.Equal(FreeformType.XWay, pad.Freeform));
        Assert.Contains(taggedPads, pad => pad.Col == 0);
        Assert.Contains(taggedPads, pad => pad.Col == 1);
        Assert.All(grid.Pads.Where(pad => pad.Col == 2), pad => Assert.Equal(FreeformType.None, pad.Freeform));
    }

    [Fact]
    public void AutoTagFreeforms_IgnoresNumericalNoiseSecondaryOverlap_WhenEdgeSpecializationEnabled()
    {
        var grid = BuildGrid(rows: 1, cols: 2, width: 2, height: 1);
        var cad = BuildCadPadSet(id: 5003, minX: 0.0, minY: 0.0, maxX: 1.0, maxY: 1.0);
        var orderedPads = grid.Pads
            .OrderBy(pad => pad.Col)
            .ToList();
        var anchorPad = orderedPads[0];
        var noisePad = orderedPads[1];
        const int cadId = 5003;
        var anchorLink = new PadMatchLink(
            CadPadId: cadId,
            RegularPadId: anchorPad.RegularPadId,
            OverlapArea: 1.0,
            RegularCoverage: 1.0,
            CadCoverage: 0.9999995);
        var noiseLink = new PadMatchLink(
            CadPadId: cadId,
            RegularPadId: noisePad.RegularPadId,
            OverlapArea: 1e-7,
            RegularCoverage: 1e-7,
            CadCoverage: 5e-7);

        var matchResult = new PadMatchResult(
            new Dictionary<int, IReadOnlyList<PadMatchLink>>
            {
                [cadId] = new[] { anchorLink, noiseLink },
            },
            new Dictionary<int, IReadOnlyList<PadMatchLink>>
            {
                [anchorPad.RegularPadId] = new[] { anchorLink },
                [noisePad.RegularPadId] = new[] { noiseLink },
            });

        FreeformDetector.AutoTagFreeforms(
            cad,
            grid,
            new MatchingSettings
            {
                FreeformAxisThreshold = 0.3,
                EnableFreeformEdgeSpecialization = true,
            },
            matchResult);

        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.None, pad.Freeform));
    }

    [Fact]
    public void DetectAssignments_AssignsFreeformOnlyToMajorLinks_WhenMinorTailFallsBelowThreshold()
    {
        var grid = BuildGrid(rows: 1, cols: 3, width: 3, height: 1);
        var cad = BuildCadPadSet(id: 6001, minX: 0.0, minY: 0.0, maxX: 3.0, maxY: 1.0);
        var orderedPads = grid.Pads
            .OrderBy(pad => pad.Col)
            .ToList();
        var first = orderedPads[0];
        var second = orderedPads[1];
        var tail = orderedPads[2];
        const int cadId = 6001;

        var links = new[]
        {
            new PadMatchLink(
                CadPadId: cadId,
                RegularPadId: first.RegularPadId,
                OverlapArea: 0.9090909091,
                RegularCoverage: 0.9090909091,
                CadCoverage: 0.9090909091),
            new PadMatchLink(
                CadPadId: cadId,
                RegularPadId: second.RegularPadId,
                OverlapArea: 0.0908948239,
                RegularCoverage: 0.0908948239,
                CadCoverage: 0.0908948239),
            new PadMatchLink(
                CadPadId: cadId,
                RegularPadId: tail.RegularPadId,
                OverlapArea: 0.000014267,
                RegularCoverage: 0.0000156937,
                CadCoverage: 0.000014267),
        };

        var matchResult = new PadMatchResult(
            new Dictionary<int, IReadOnlyList<PadMatchLink>>
            {
                [cadId] = links,
            },
            new Dictionary<int, IReadOnlyList<PadMatchLink>>
            {
                [first.RegularPadId] = new[] { links[0] },
                [second.RegularPadId] = new[] { links[1] },
                [tail.RegularPadId] = new[] { links[2] },
            });

        var assignments = FreeformDetector.DetectAssignments(
            cad,
            grid,
            new MatchingSettings
            {
                FreeformAxisThreshold = 0.01,
                EnableAutoDetectXy = false,
                EnableFreeformEdgeSpecialization = false,
            },
            matchResult);

        Assert.Equal(2, assignments.Count);
        Assert.Equal(FreeformType.XWay, assignments[first.RegularPadId]);
        Assert.Equal(FreeformType.XWay, assignments[second.RegularPadId]);
        Assert.False(assignments.ContainsKey(tail.RegularPadId));
    }

    private static RegularGrid BuildGrid(int rows, int cols, double width, double height)
    {
        var settings = new GridSettings
        {
            XChannels = cols,
            YChannels = rows,
            ActiveAreaWidth = width,
            ActiveAreaHeight = height,
            BoundsPaddingRatio = 0,
        };

        return new RegularGridBuilder().BuildFromSettings(settings);
    }

    private static CadPadSet BuildCadPadSet(int id, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
        var cadPad = new CadPad(id, $"CAD{id}", "L1", polygon);
        return new CadPadSet(new[] { cadPad });
    }
}
