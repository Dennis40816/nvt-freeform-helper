using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfRegularMappingAnalyzerTests
{
    [Fact]
    public void Analyze_PerfectOverlap_MapsAllPads()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cadPads = new List<CadPad>();
        for (var row = 0; row < grid.Rows; row++)
        {
            for (var col = 0; col < grid.Cols; col++)
            {
                var reg = grid.GetPad(row, col);
                var b = reg.Bounds;
                var poly = new Polygon2(new[]
                {
                    new Point2(b.MinX, b.MinY),
                    new Point2(b.MaxX, b.MinY),
                    new Point2(b.MaxX, b.MaxY),
                    new Point2(b.MinX, b.MaxY),
                });

                var id = 100 + reg.Index;
                cadPads.Add(new CadPad(id, $"C{id}", "L1", poly));
            }
        }

        var cad = new CadPadSet(cadPads);

        var analyzer = new DxfRegularMappingAnalyzer();
        var res = DxfRegularMappingAnalyzer.Analyze(cad, grid, new IndexMappingSettings
        {
            CandidatePaddingCells = 0,
            LowConfidenceThreshold = 0.5,
            AmbiguousMargin = 0.05,
        });

        Assert.Equal(4, res.Report.CadCount);
        Assert.Equal(4, res.Report.RegularCount);
        Assert.Equal(4, res.Report.MappedCount);
        Assert.Equal(0, res.Report.UnmappedCadCount);
        Assert.Equal(0, res.Report.UnmappedRegularCount);
        Assert.False(res.Report.HasIssues);

        foreach (var p in res.Pairs)
        {
            Assert.Equal(100 + p.RegularPadIndex, p.CadPadId);
            Assert.Equal(p.DxfIndex, p.RegularPadIndex);
            Assert.Equal(grid.Pads[p.RegularPadIndex].DiffIndex, p.DiffIndex);
            Assert.Equal(grid.Pads[p.RegularPadIndex].IcIndex, p.IcIndex);
            Assert.True(p.Score > 0.99);
        }
    }

    [Fact]
    public void Analyze_WithManualOverride_AppliesOverridePair()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var left = grid.GetPad(0, 0).Bounds;
        var right = grid.GetPad(0, 1).Bounds;

        var cad = new CadPadSet(new[]
        {
            BuildCad(10, left),
            BuildCad(11, right),
        });

        var analyzer = new DxfRegularMappingAnalyzer();
        var res = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            new IndexMappingSettings
            {
                CandidatePaddingCells = 0,
            },
            new Dictionary<int, int> { [10] = 1 });

        Assert.Contains(res.Pairs, pair => pair.CadPadId == 10 && pair.RegularPadIndex == 1);
    }

    [Fact]
    public void Analyze_AmbiguousCad_ProvidesScoreDiagnosticsAndTopCandidates()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cad = new CadPadSet(new[]
        {
            BuildCad(20, new Rect2(0.5, 0, 1.5, 1)),
        });

        var analyzer = new DxfRegularMappingAnalyzer();
        var res = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            new IndexMappingSettings
            {
                CandidatePaddingCells = 1,
                LowConfidenceThreshold = 0.9,
                AmbiguousMargin = 0.2,
                DiagnosticTopK = 3,
            });

        var issue = Assert.Single(res.Report.Issues, i => i.Kind == DxfRegularMappingIssueKind.Ambiguous);
        Assert.Equal(20, issue.CadPadId);
        Assert.True(issue.BestScore.HasValue);
        Assert.True(issue.SecondScore.HasValue);
        Assert.True(issue.Margin.HasValue);
        Assert.NotNull(issue.TopCandidates);
        Assert.True(issue.TopCandidates!.Count >= 2);
        Assert.True(issue.TopCandidates.Count <= 3);
        Assert.True(issue.TopCandidates[0].Score >= issue.TopCandidates[1].Score);
    }

    [Fact]
    public void Analyze_ReportsProgress_ToCompletion()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 3,
            YChannels = 3,
            ActiveAreaWidth = 3,
            ActiveAreaHeight = 3,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cadPads = new List<CadPad>();
        for (var row = 0; row < grid.Rows; row++)
        {
            for (var col = 0; col < grid.Cols; col++)
            {
                var reg = grid.GetPad(row, col);
                cadPads.Add(BuildCad(1000 + reg.Index, reg.Bounds));
            }
        }

        var progress = new List<double>();
        var analyzer = new DxfRegularMappingAnalyzer();
        _ = DxfRegularMappingAnalyzer.Analyze(
            new CadPadSet(cadPads),
            grid,
            new IndexMappingSettings
            {
                CandidatePaddingCells = 0,
            },
            reportProgress: value => progress.Add(value));

        Assert.NotEmpty(progress);
        Assert.True(progress[0] >= 0 && progress[0] <= 1);
        Assert.True(progress[^1] >= 0.999 && progress[^1] <= 1.0);
        Assert.All(progress, value => Assert.InRange(value, 0.0, 1.0));
    }

    [Fact]
    public void Analyze_ReportsCountMismatchAndLowConfidence_WhenRemainingRegularHasNoOverlap()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cad = new CadPadSet(new[]
        {
            BuildCad(31, new Rect2(0.0, 0.0, 0.2, 1.0)),
        });

        var analyzer = new DxfRegularMappingAnalyzer();
        var res = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            new IndexMappingSettings
            {
                CandidatePaddingCells = 0,
                LowConfidenceThreshold = 0.8,
                AmbiguousMargin = 0.0,
            });

        Assert.Equal(1, res.Report.CadCount);
        Assert.Equal(2, res.Report.RegularCount);
        Assert.Equal(1, res.Report.MappedCount);
        Assert.Equal(0, res.Report.UnmappedCadCount);
        Assert.Equal(1, res.Report.UnmappedRegularCount);
        Assert.Equal(1, res.Report.LowConfidenceCount);

        Assert.Contains(res.Report.Issues, i => i.Kind == DxfRegularMappingIssueKind.CountMismatch);
        Assert.Contains(res.Report.Issues, i => i.Kind == DxfRegularMappingIssueKind.LowConfidence);
        Assert.DoesNotContain(res.Report.Issues, i => i.Kind == DxfRegularMappingIssueKind.UnmappedRegular);
    }

    [Fact]
    public void Analyze_DoesNotReportUnmappedCadIssue_WhenNoOverlapEvidenceExists()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 1,
            YChannels = 1,
            ActiveAreaWidth = 1,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cad = new CadPadSet(new[]
        {
            BuildCad(51, new Rect2(2.0, 2.0, 3.0, 3.0)),
        });

        var analyzer = new DxfRegularMappingAnalyzer();
        var res = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            new IndexMappingSettings
            {
                WeightIou = 1.0,
                WeightCentroidDistance = 0.0,
                WeightAreaRatio = 0.0,
                CandidatePaddingCells = 0,
            });

        Assert.Equal(1, res.Report.UnmappedCadCount);
        Assert.Equal(0, res.Report.MappedCount);

        Assert.DoesNotContain(res.Report.Issues, i => i.Kind == DxfRegularMappingIssueKind.UnmappedCad);
    }

    [Fact]
    public void Analyze_ReportsUnmappedRegularIssue_WhenOverlapEvidenceExists()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);

        var cad = new CadPadSet(new[]
        {
            BuildCad(52, new Rect2(0.4, 0.0, 1.4, 1.0)),
        });

        var res = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            new IndexMappingSettings
            {
                CandidatePaddingCells = 0,
            });

        Assert.Equal(1, res.Report.UnmappedRegularCount);
        Assert.Contains(res.Report.Issues, i => i.Kind == DxfRegularMappingIssueKind.UnmappedRegular);
    }

    [Fact]
    public void Analyze_ReportsDuplicateDiff_WhenCurrentAssignmentsShareSameDiff()
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);
        var left = grid.GetPad(0, 0).Bounds;
        var right = grid.GetPad(0, 1).Bounds;
        var cad = new CadPadSet(new[]
        {
            BuildCad(61, left),
            BuildCad(62, right),
        });

        var res = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            new IndexMappingSettings
            {
                CandidatePaddingCells = 0,
            },
            expectedDiffIndexByCadId: new Dictionary<int, int>
            {
                [61] = 324,
                [62] = 324,
            });

        Assert.Equal(2, res.Report.DuplicateDiffCount);
        var duplicateIssues = res.Report.Issues.Where(i => i.Kind == DxfRegularMappingIssueKind.DuplicateDiff).ToList();
        Assert.Equal(2, duplicateIssues.Count);
        Assert.All(duplicateIssues, issue => Assert.Equal(324, issue.DxfIndex));
        Assert.Contains("IC1", duplicateIssues[0].Message);
        Assert.Contains("shared by CAD 61, 62", duplicateIssues[0].Message);
    }

    [Fact]
    public void Analyze_DoesNotReportDuplicateDiffAcrossDifferentIcGroups()
    {
        var topPad = CreateRegularPad(0, 0, 0, icIndex: 0, diffIndex: 10);
        var bottomPad = CreateRegularPad(1, 0, 1, icIndex: 1, diffIndex: 11);
        var xEdges = new[] { 0d, 1d };
        var yEdges = new[] { 0d, 1d, 2d };
        var pads = new[] { topPad, bottomPad };
        var grid = new RegularGrid(2, 1, xEdges, yEdges, pads);
        var top = topPad.Bounds;
        var bottom = bottomPad.Bounds;
        var cad = new CadPadSet(new[]
        {
            BuildCad(71, top),
            BuildCad(72, bottom),
        });

        var res = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            new IndexMappingSettings
            {
                CandidatePaddingCells = 0,
            },
            expectedDiffIndexByCadId: new Dictionary<int, int>
            {
                [71] = 324,
                [72] = 324,
            });

        Assert.Equal(0, res.Report.DuplicateDiffCount);
        Assert.DoesNotContain(res.Report.Issues, issue => issue.Kind == DxfRegularMappingIssueKind.DuplicateDiff);
    }

    private static RegularPad CreateRegularPad(int row, int col, int regularPadId, int icIndex, int diffIndex)
    {
        return new RegularPad(
            row,
            col,
            regularPadId,
            new Polygon2(new[]
            {
                new Point2(col, row),
                new Point2(col + 1, row),
                new Point2(col + 1, row + 1),
                new Point2(col, row + 1),
            }))
        {
            IcIndex = icIndex,
            DiffIndex = diffIndex,
        };
    }

    private static CadPad BuildCad(int id, Rect2 bounds)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(bounds.MinX, bounds.MinY),
            new Point2(bounds.MaxX, bounds.MinY),
            new Point2(bounds.MaxX, bounds.MaxY),
            new Point2(bounds.MinX, bounds.MaxY),
        });

        return new CadPad(id, $"C{id}", "L1", polygon);
    }
}
