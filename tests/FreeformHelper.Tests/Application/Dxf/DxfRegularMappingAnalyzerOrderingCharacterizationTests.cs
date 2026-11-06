using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Characterizes the order of reported issues and the exact progress callback sequence of
/// <see cref="DxfRegularMappingAnalyzer"/>, including the paths that skip progress reports.
/// </summary>
public sealed class DxfRegularMappingAnalyzerOrderingCharacterizationTests
{
    private const int Precision = 9;

    private static readonly int[] _expectedMappedCadIds = [21, 22, 23];

    [Fact]
    public void Analyze_FullMapping_ReportsProgressOnlyAtStageEnds()
    {
        var grid = BuildRowGrid(2);
        var cad = new CadPadSet(new[]
        {
            BuildCad(1, grid.GetPad(0, 0).Bounds),
            BuildCad(2, grid.GetPad(0, 1).Bounds),
        });
        var progress = new List<double>();

        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, PaddingZero(), reportProgress: progress.Add);

        Assert.Equal(2, result.Report.MappedCount);
        Assert.False(result.Report.HasIssues);
        AssertProgress([0.0, 0.60, 0.60, 0.80, 0.95, 1.0], progress);
    }

    [Fact]
    public void Analyze_UnmappedRegularWithEvidence_ReportsRegularStageProgress()
    {
        var grid = BuildRowGrid(2);
        var cad = new CadPadSet(new[] { BuildCad(52, new Rect2(0.4, 0.0, 1.4, 1.0)) });
        var progress = new List<double>();

        var settings = new IndexMappingSettings
        {
            CandidatePaddingCells = 0,
            LowConfidenceThreshold = 0.0,
            AmbiguousMargin = 0.0,
        };

        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, reportProgress: progress.Add);

        Assert.Equal(1, result.Report.MappedCount);
        Assert.Equal(
            new[] { DxfRegularMappingIssueKind.CountMismatch, DxfRegularMappingIssueKind.UnmappedRegular },
            result.Report.Issues.Select(static issue => issue.Kind).ToArray());
        AssertProgress([0.0, 0.60, 0.60, 0.80, 0.95, 0.99, 1.0], progress);
    }

    [Fact]
    public void Analyze_UnmappedCadWithEvidence_SkipsPerCadProgressReport()
    {
        var grid = BuildRowGrid(1);
        var pad = grid.GetPad(0, 0).Bounds;
        var cad = new CadPadSet(new[]
        {
            BuildCad(1, pad),
            BuildCad(2, new Rect2(pad.MinX + 0.1, pad.MinY, pad.MaxX - 0.1, pad.MaxY)),
        });
        var progress = new List<double>();

        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, PaddingZero(), reportProgress: progress.Add);

        Assert.Equal(1, result.Report.MappedCount);
        Assert.Equal(1, result.Report.UnmappedCadCount);
        Assert.Equal(
            new[] { DxfRegularMappingIssueKind.CountMismatch, DxfRegularMappingIssueKind.UnmappedCad },
            result.Report.Issues.Select(static issue => issue.Kind).ToArray());
        Assert.Equal("visible DXF pads=2, regular pads=1.", result.Report.Issues[0].Message);
        Assert.Equal("Expected diff idx 1 (CAD id 2).", result.Report.Issues[1].Message);
        Assert.Equal(1, result.Report.Issues[1].DxfIndex);
        Assert.Equal(2, result.Report.Issues[1].CadPadId);
        Assert.NotNull(result.Report.Issues[1].TopCandidates);

        // The unmapped CAD takes the `continue` path in the per-CAD loop, so 0.95 is never reported.
        AssertProgress([0.0, 0.60, 0.60, 0.80, 1.0], progress);
    }

    [Fact]
    public void Analyze_NoCadPads_ReportsEmptyStageProgressAndCountMismatchOnly()
    {
        var grid = BuildRowGrid(2);
        var progress = new List<double>();

        var result = DxfRegularMappingAnalyzer.Analyze(
            new CadPadSet(Array.Empty<CadPad>()),
            grid,
            PaddingZero(),
            reportProgress: progress.Add);

        Assert.Empty(result.Pairs);
        var issue = Assert.Single(result.Report.Issues);
        Assert.Equal(DxfRegularMappingIssueKind.CountMismatch, issue.Kind);
        Assert.Equal("visible DXF pads=0, regular pads=2.", issue.Message);
        Assert.Equal(
            "mapped=0, unmappedCad=0, unmappedRegular=2, lowConf=0, ambiguous=0, duplicateDiff=0",
            result.Report.Summary);
        AssertProgress([0.0, 0.60, 0.80, 0.95, 1.0], progress);
    }

    [Fact]
    public void Analyze_MixedIssues_AreReportedInStageOrder()
    {
        var grid = BuildRowGrid(4);
        var cad = new CadPadSet(new[]
        {
            BuildCad(21, grid.GetPad(0, 0).Bounds),
            BuildCad(22, new Rect2(1.3, 0.0, 2.3, 1.0)),
            BuildCad(23, new Rect2(2.45, 0.0, 3.45, 1.0)),
            BuildCad(24, new Rect2(0.1, 0.0, 0.9, 1.0)),
        });
        var settings = new IndexMappingSettings
        {
            CandidatePaddingCells = 0,
            LowConfidenceThreshold = 0.8,
            AmbiguousMargin = 0.2,
        };

        var result = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            settings,
            expectedDiffIndexByCadId: new Dictionary<int, int> { [21] = 7, [22] = 7 });

        var issues = result.Report.Issues;
        var expectedOrder = new (DxfRegularMappingIssueKind Kind, int? CadPadId)[]
        {
            (DxfRegularMappingIssueKind.LowConfidence, 22),
            (DxfRegularMappingIssueKind.LowConfidence, 23),
            (DxfRegularMappingIssueKind.Ambiguous, 23),
            (DxfRegularMappingIssueKind.UnmappedCad, 24),
            (DxfRegularMappingIssueKind.DuplicateDiff, 21),
            (DxfRegularMappingIssueKind.DuplicateDiff, 22),
            (DxfRegularMappingIssueKind.UnmappedRegular, null),
        };
        Assert.Equal(expectedOrder, issues.Select(static issue => (issue.Kind, issue.CadPadId)).ToArray());

        var duplicate = issues.First(static issue => issue.Kind == DxfRegularMappingIssueKind.DuplicateDiff);
        Assert.Equal("Current diff idx 7 in IC1 is shared by CAD 21, 22.", duplicate.Message);
        Assert.Equal(7, duplicate.DxfIndex);
        Assert.Equal("Expected diff idx 3 (CAD id 24).", issues[3].Message);
        Assert.Equal(grid.GetPad(0, 3).RegularPadId, issues[^1].RegularPadIndex);

        var report = result.Report;
        Assert.Equal(
            (4, 4, 3, 1, 1, 2, 1, 2, 7, false),
            (report.CadCount, report.RegularCount, report.MappedCount, report.UnmappedCadCount, report.UnmappedRegularCount,
                report.LowConfidenceCount, report.AmbiguousCount, report.DuplicateDiffCount, report.TotalIssueCount, report.IssuesTruncated));
        Assert.Equal(
            "mapped=3, unmappedCad=1, unmappedRegular=1, lowConf=2, ambiguous=1, duplicateDiff=2",
            report.Summary);
        Assert.Equal(
            _expectedMappedCadIds,
            result.Pairs.Select(static pair => pair.CadPadId).OrderBy(static id => id).ToArray());
    }

    [Fact]
    public void Analyze_MixedIssues_TruncationKeepsFirstIssuesAndPreCountedTotals()
    {
        var grid = BuildRowGrid(4);
        var cad = new CadPadSet(new[]
        {
            BuildCad(21, grid.GetPad(0, 0).Bounds),
            BuildCad(22, new Rect2(1.3, 0.0, 2.3, 1.0)),
            BuildCad(23, new Rect2(2.45, 0.0, 3.45, 1.0)),
            BuildCad(24, new Rect2(0.1, 0.0, 0.9, 1.0)),
        });
        var settings = new IndexMappingSettings
        {
            CandidatePaddingCells = 0,
            LowConfidenceThreshold = 0.8,
            AmbiguousMargin = 0.2,
            MaxReportIssues = 3,
            MaxSampleIssues = 2,
        };

        var report = DxfRegularMappingAnalyzer.Analyze(
            cad,
            grid,
            settings,
            expectedDiffIndexByCadId: new Dictionary<int, int> { [21] = 7, [22] = 7 }).Report;

        Assert.True(report.IssuesTruncated);
        Assert.Equal(7, report.TotalIssueCount);
        Assert.Equal(
            new[] { DxfRegularMappingIssueKind.LowConfidence, DxfRegularMappingIssueKind.LowConfidence, DxfRegularMappingIssueKind.Ambiguous },
            report.Issues.Select(static issue => issue.Kind).ToArray());
        Assert.Equal(2, report.SampleIssues.Count);
        Assert.Same(report.Issues[0], report.SampleIssues[0]);
        Assert.Same(report.Issues[1], report.SampleIssues[1]);
        Assert.Equal(2, report.LowConfidenceCount);
        Assert.Equal(1, report.AmbiguousCount);
        Assert.Equal(2, report.DuplicateDiffCount);
        Assert.True(report.HasIssues);
    }

    [Fact]
    public void AnalyzeWithTypedOverrides_ManualOverride_IsAppliedBeforeScoredPairs()
    {
        var grid = BuildRowGrid(2);
        var cad = new CadPadSet(new[]
        {
            BuildCad(1, grid.GetPad(0, 0).Bounds),
            BuildCad(2, grid.GetPad(0, 1).Bounds),
        });
        var overrides = new Dictionary<CadPadId, RegularPadId>
        {
            [new CadPadId(1)] = new RegularPadId(1),
        };

        var result = DxfRegularMappingAnalyzer.AnalyzeWithTypedOverrides(cad, grid, PaddingZero(), overrides);

        // Overrides are applied first, so CAD 2 loses its only retained candidate (regular 1).
        var pair = Assert.Single(result.Pairs);
        Assert.Equal((1, 1), (pair.CadPadId, pair.RegularPadIndex));
        Assert.Equal(1, result.Report.UnmappedCadCount);
    }

    private static IndexMappingSettings PaddingZero()
    {
        return new IndexMappingSettings { CandidatePaddingCells = 0 };
    }

    private static void AssertProgress(double[] expected, List<double> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], Precision);
        }
    }

    private static RegularGrid BuildRowGrid(int cols)
    {
        var gridSettings = new GridSettings
        {
            XChannels = cols,
            YChannels = 1,
            ActiveAreaWidth = cols,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };

        var grid = RegularGridBuilder.BuildFromSettings(gridSettings);
        DiffIndexMapper.ApplyMapping(grid, gridSettings);
        return grid;
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
