// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfRegularMappingCollapsedGeometryBehaviorTests
{
    [Fact]
    public void Analyze_CollapsedManualPair_ReportsFiniteDistanceScoreAndLowConfidence()
    {
        using var culture = TestCulture.UseEnUs();
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 17, 0, 0, 0, 0, diffIndex: 9, icIndex: 1);
        var grid = new RegularGrid(1, 1, [0, 0], [0, 0], [regular]);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(42, "Collapsed", 0.25, 0, 0.25, 0)]);
        var settings = new IndexMappingSettings
        {
            WeightIou = 1,
            WeightCentroidDistance = 1,
            WeightAreaRatio = 1,
            LowConfidenceThreshold = 0.5,
        };

        var result = DxfRegularMappingAnalyzer.Analyze(cad, grid, settings, new Dictionary<int, int> { [42] = 17 });

        var pair = Assert.Single(result.Pairs);
        Assert.Equal((42, 17, 1, 9), (pair.CadPadId, pair.RegularPadIndex, pair.IcIndex, pair.DiffIndex));
        Assert.Equal(0.25, pair.Score, 12);
        Assert.Equal(1, result.Report.MappedCount);
        Assert.Equal(0, result.Report.UnmappedCadCount);
        Assert.Equal(0, result.Report.UnmappedRegularCount);
        Assert.Equal(1, result.Report.LowConfidenceCount);
        var issue = Assert.Single(result.Report.Issues);
        Assert.Equal(DxfRegularMappingIssueKind.LowConfidence, issue.Kind);
        Assert.Equal(0.25, issue.Score);
        Assert.Equal("Expected diff idx 0 (CAD id 42) -> diff9 (IC1, regId 17), score=0.25.", issue.Message);
        var candidate = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<DxfRegularMappingCandidate>>(issue.TopCandidates));
        Assert.Equal(new DxfRegularMappingCandidate(17, 1, 9, 0.25), candidate);
    }
}
