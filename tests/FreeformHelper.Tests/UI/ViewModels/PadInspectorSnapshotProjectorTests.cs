// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInspectorSnapshotProjectorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CadMatchDisplaySelectsCachedDetailsIndependentlyOfDetailsText(bool includeMatchDetails, bool includeRuleTrace)
    {
        PadInspectorMatchedRegularSnapshot[] details = [new(101, 0, 7, 0.6, 0.8), new(102, 1, 8, 0.4, 0.9)];
        var cache = new CadInspectorMatchCacheEntry(3, [], [101, 102], details, "IC1/diff7 (+1)", "Cached details", 0.6);

        var result = PadInspectorSnapshotProjector.BuildCadMatchDisplay(cache, includeMatchDetails, includeRuleTrace);

        Assert.Equal("IC1/diff7 (+1)", result.MatchText);
        Assert.Equal(includeMatchDetails ? "Cached details" : "Matched regular pad(s): 2.", result.MatchDetailsText);
        Assert.Equal<PadInspectorMatchedRegularSnapshot>(includeMatchDetails || includeRuleTrace
            ? [new(101, 0, 7, 0.6, 0.8), new(102, 1, 8, 0.4, 0.9)] : [], result.Details);
        Assert.Same(includeMatchDetails || includeRuleTrace
            ? details : Array.Empty<PadInspectorMatchedRegularSnapshot>(), result.Details);
    }

    [Fact]
    public void CadMatchDisplayWithoutMatchesUsesUnmatchedText()
    {
        var cache = new CadInspectorMatchCacheEntry(3, [], [], [], "Unmatched", "Unmatched", 0);

        var result = PadInspectorSnapshotProjector.BuildCadMatchDisplay(cache, false, false);

        Assert.Equal("Unmatched", result.MatchText);
        Assert.Equal("Unmatched", result.MatchDetailsText);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void NotchRowSummaryPreservesVersionOrderAndLabels()
    {
        var eligibility = new NotchCadRowEligibility(11, [NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21], 2, 101, "Eligible");

        var result = PadInspectorSnapshotProjector.FormatNotchRowSummary(eligibility);

        Assert.Equal("Notch rows: 2 (v2.2, v2.1)", result);
    }

    [Theory]
    [InlineData(0, "No regular overlap.", "Notch rows: 0 (No regular overlap.)")]
    [InlineData(1, "Eligible", "Notch rows: 1 (-)")]
    public void NotchRowSummaryPreservesTheReasonOrMissingVersionLabel(int rowCount, string reason, string expected)
    {
        var eligibility = new NotchCadRowEligibility(11, [], rowCount, null, reason);

        var result = PadInspectorSnapshotProjector.FormatNotchRowSummary(eligibility);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RegularFlagsPreserveCapturedValuesSourcesAndLists(bool includeMatchDetails, bool includeRuleTrace)
    {
        var regular = TestGeometryFactory.CreateRegularPad(2, 3, 101, 0, 0, 10, 20, diffIndex: 7, icIndex: 1);
        regular.Freeform = FreeformType.YWay;
        PadInspectorMatchedCadSnapshot[] details = [new(11, 7, 0.6, 0.8), new(12, 8, 0.4, 0.9)];
        PadInspectorRuleTraceEntry[] trace = [new("Freeform", "YWay", "Source: Override")];
        var cache = new RegularInspectorMatchCacheEntry(
            3, [], [11, 12], details, "CAD idx 7, 8 (CAD id 11, 12)", "Cached details",
            "Strict", "Override", "Saved/manual override keeps this regular pad as YWay.", 0.6, trace);

        var result = PadInspectorSnapshotProjector.BuildRegular(regular, cache, includeMatchDetails, includeRuleTrace, 4);

        var snapshot = Assert.IsType<RegularPadInspectorSnapshot>(result.Regular);
        Assert.Equal("regular", result.Kind);
        Assert.Null(result.Cad);
        Assert.Equal(101, snapshot.RegularPadId);
        Assert.Equal(101, snapshot.Index);
        Assert.Equal(2, snapshot.Row);
        Assert.Equal(3, snapshot.Col);
        Assert.Equal(4, snapshot.DisplayRow);
        Assert.Equal(1, snapshot.IcIndex);
        Assert.Equal(7, snapshot.DiffIndex);
        Assert.Equal(200, snapshot.Area);
        Assert.Equal(regular.Bounds, snapshot.Bounds);
        Assert.Equal(regular.Centroid, snapshot.Centroid);
        Assert.Equal(FreeformType.YWay, snapshot.Freeform);
        Assert.Equal("CAD idx 7, 8 (CAD id 11, 12)", snapshot.MatchText);
        Assert.Equal(includeMatchDetails ? "Cached details" : "Matched CAD pad(s): 2.", snapshot.MatchDetailsText);
        Assert.Equal(includeMatchDetails || includeRuleTrace ? "Strict" : "Computing...", snapshot.DiffSource);
        Assert.Equal(includeMatchDetails || includeRuleTrace ? "Override" : "Computing...", snapshot.FreeformSource);
        Assert.Equal(includeMatchDetails || includeRuleTrace
            ? "Saved/manual override keeps this regular pad as YWay." : "Computing...", snapshot.FreeformSourceDetail);
        Assert.Equal<int>([11, 12], snapshot.MatchedCadPadIds);
        Assert.Same(cache.MatchedCadIds, snapshot.MatchedCadPadIds);
        Assert.Equal<PadInspectorMatchedCadSnapshot>(includeMatchDetails || includeRuleTrace
            ? [new(11, 7, 0.6, 0.8), new(12, 8, 0.4, 0.9)] : [], snapshot.MatchedCadDetails);
        Assert.Same(includeMatchDetails || includeRuleTrace
            ? details : Array.Empty<PadInspectorMatchedCadSnapshot>(), snapshot.MatchedCadDetails);
        Assert.Equal<PadInspectorRuleTraceEntry>(includeRuleTrace
            ? [new("Freeform", "YWay", "Source: Override")]
            : [new("Computing", "Queued", "Detailed trace is loading in background.")], snapshot.RuleTrace);
        Assert.Equal(0.6, snapshot.MatchConfidence);
    }

    [Theory]
    [InlineData(false, null, null, 0)]
    [InlineData(false, "Precomputed", null, 0)]
    [InlineData(true, "Precomputed", "Precomputed", 0)]
    [InlineData(true, "", "", 0)]
    [InlineData(true, null, "Generated", 1)]
    [InlineData(true, null, null, 1)]
    public void NotchDiagnosticsInvokeTheDelegateOnlyForExpensiveMissingText(
        bool includeExpensiveDetails, string? precomputed, string? expected, int expectedCalls)
    {
        var cad = TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, 10, 10);
        var compensation = new NotchV22CompensationResult(0.75, 1, 0.75, false, 100, 75, 100, 1, [], [], [], []);
        var resolved = NotchV22ResolvedResultService.Build(cad, compensation, 0);
        var diagnosticsCalls = 0;
        string? BuildDiagnostics(NotchV22CompensationResult value)
        {
            Assert.Same(compensation, value);
            diagnosticsCalls++;
            return expected;
        }

        var result = PadInspectorSnapshotProjector.BuildNotch(
            resolved, includeExpensiveDetails, precomputed, BuildDiagnostics, NotchComputationMode.LegacyRegularAnchor);

        Assert.Equal(expected, result.Diagnostics);
        Assert.Equal(expectedCalls, diagnosticsCalls);
        Assert.Equal(NotchComputationMode.LegacyRegularAnchor, result.ComputationMode);
        Assert.Same(resolved.TargetAllocation.TargetCoverageProjection, result.TargetCoverageProjection);
    }
}
