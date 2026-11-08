// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MappingScoreAndCandidateBehaviorTests
{
    private static readonly int[] _expected1 = [3, 2];
    private static readonly int[] _expected2 = [40, 30];

    [Fact]
    public void Analyze_AllScoreWeightsDisabled_LeavesAutomaticMappingEmptyButKeepsManualPairAtZeroScore()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(42, "PAD", 0, 0, 1, 1)]);
        var settings = new IndexMappingSettings
        {
            WeightIou = 0,
            WeightCentroidDistance = 0,
            WeightAreaRatio = 0,
        };

        var automatic = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings);
        var manual = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, new Dictionary<int, int> { [42] = 0 });

        Assert.Empty(automatic.Pairs);
        Assert.Equal(1, automatic.Report.UnmappedCadCount);
        Assert.Equal(1, automatic.Report.UnmappedRegularCount);
        var pair = Assert.Single(manual.Pairs);
        Assert.Equal(42, pair.CadPadId);
        Assert.Equal(0, pair.RegularPadIndex);
        Assert.Equal(0d, pair.Score);
        Assert.Equal(1, manual.Report.MappedCount);
        Assert.Equal(1, manual.Report.LowConfidenceCount);
        var issue = Assert.Single(manual.Report.Issues);
        Assert.Equal(DxfRegularMappingIssueKind.LowConfidence, issue.Kind);
        Assert.Equal(0d, issue.Score);
        Assert.NotNull(issue.TopCandidates);
        Assert.Empty(issue.TopCandidates);
    }

    [Fact]
    public void Analyze_BetterCandidatesVisitedLater_MapsBestAndReportsOnlyTopTwoInScoreOrder()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 20, 30, 40, 50]);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(42, "PAD", 3, 0, 4, 1)]);
        var settings = new IndexMappingSettings
        {
            CandidatePaddingCells = 3,
            CandidateNumber = 2,
            DiagnosticTopK = 2,
            AmbiguousMargin = 1,
        };

        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings);

        var pair = Assert.Single(result.Pairs);
        Assert.Equal(3, pair.RegularPadIndex);
        Assert.Equal(40, pair.DiffIndex);
        Assert.Equal(1d, pair.Score, 12);
        var issue = Assert.Single(result.Report.Issues, item => item.Kind == DxfRegularMappingIssueKind.Ambiguous);
        Assert.NotNull(issue.TopCandidates);
        Assert.Equal(_expected1, issue.TopCandidates.Select(candidate => candidate.RegularPadIndex));
        Assert.Equal(_expected2, issue.TopCandidates.Select(candidate => candidate.DiffIndex));
        Assert.Equal(1d, issue.TopCandidates[0].Score, 12);
        Assert.InRange(issue.TopCandidates[1].Score, 0d, 1d);
        Assert.True(issue.TopCandidates[0].Score > issue.TopCandidates[1].Score);
        Assert.Equal(issue.BestScore, issue.TopCandidates[0].Score);
        Assert.Equal(issue.SecondScore, issue.TopCandidates[1].Score);
    }
}
