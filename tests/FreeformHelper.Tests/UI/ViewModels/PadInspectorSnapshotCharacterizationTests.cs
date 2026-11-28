// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInspectorSnapshotCharacterizationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CadMatchedFlagsPreserveTextDetailsAndTrace(bool includeMatchDetails, bool includeRuleTrace)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateViewModel();

        var result = Assert.IsType<PadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            11, includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: false,
            includeMatchDetails: includeMatchDetails, includeRuleTrace: includeRuleTrace));

        var cad = Assert.IsType<CadPadInspectorSnapshot>(result.Cad);
        Assert.Equal("cad", result.Kind);
        Assert.Null(result.Regular);
        Assert.Equal("IC1/diff7", cad.MatchText);
        Assert.Equal(includeMatchDetails ? "IC1/diff7 (regId 101): 60%" : "Matched regular pad(s): 1.", cad.MatchDetailsText);
        Assert.Equal<int>([101], cad.MatchedRegularPadIds);
        Assert.Equal<PadInspectorMatchedRegularSnapshot>(includeMatchDetails || includeRuleTrace
            ? [new(101, 0, 7, 0.6, 0.8)] : [], cad.MatchedRegularDetails);
        Assert.Equal<PadInspectorRuleTraceEntry>(includeRuleTrace
            ? [new("Diff idx", "Auto (geometry seed)", "Diff idx value: 7"),
               new("Match", "Matched regular pad count: 1", "Best target: IC1/diff7. CAD coverage: 60.0%. Regular coverage: 80.0%."),
               new("Notch export", "Computing...", "Deferred notch-row eligibility is running."),
               new("Notch 2.2", "Not available", "Grid is not ready.")]
            : [new("Computing", "Queued", "Detailed trace is loading in background.")], cad.RuleTrace);
        Assert.Equal(0.6, cad.MatchConfidence);
        Assert.Equal(7, cad.DxfIndex);
        Assert.Equal("7", cad.DxfIndexDisplayText);
        Assert.Null(cad.CadOutputFwDiffOverride);
        Assert.False(cad.IsDxfIndexAnchor);
        Assert.Equal(0, cad.IcIndex);
        Assert.Equal(vm.CadPads[0].Name, cad.Name);
        Assert.Equal(vm.CadPads[0].Layer, cad.Layer);
        Assert.Equal(vm.CadPads[0].Area, cad.Area);
        Assert.Equal(vm.CadPads[0].Bounds, cad.Bounds);
        Assert.Equal(vm.CadPads[0].Centroid, cad.Centroid);
        Assert.Equal(4, cad.Vertices);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CadUnmatchedFlagsPreserveEmptyDetailsAndTrace(bool includeMatchDetails, bool includeRuleTrace)
    {
        var vm = CreateViewModel();

        var cad = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            12, includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: false,
            includeMatchDetails: includeMatchDetails, includeRuleTrace: includeRuleTrace)?.Cad);

        Assert.Equal("Unmatched", cad.MatchText);
        Assert.Equal("Unmatched", cad.MatchDetailsText);
        Assert.Empty(cad.MatchedRegularPadIds);
        Assert.Empty(cad.MatchedRegularDetails);
        Assert.Equal(0.0, cad.MatchConfidence);
        Assert.Equal<PadInspectorRuleTraceEntry>(includeRuleTrace
            ? [new("Diff idx", "Auto (geometry seed)", "Diff idx unresolved (-)."),
               new("Match", "No matched regular pad", "Best overlap target: none."),
               new("Notch export", "Computing...", "Deferred notch-row eligibility is running."),
               new("Notch 2.2", "Not available", "Grid is not ready.")]
            : [new("Computing", "Queued", "Detailed trace is loading in background.")], cad.RuleTrace);
    }

    [Theory]
    [InlineData(false, "Notch rows: computing...", "Computing...", "Deferred notch-row eligibility is running.")]
    [InlineData(true, "Notch rows: 0 (Grid not built.)", "No row", "Grid not built.")]
    public void CadRowEligibilityPreservesDeferredAndGridlessSummaries(
        bool includeDetails, string expectedSummary, string expectedOutcome, string expectedDetail)
    {
        var vm = CreateViewModel();

        var cad = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            11, includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: includeDetails)?.Cad);

        Assert.False(cad.IsNotchRowEligible);
        Assert.Equal(expectedSummary, cad.NotchRowSummary);
        Assert.Equal(new PadInspectorRuleTraceEntry("Notch export", expectedOutcome, expectedDetail),
            Assert.Single(cad.RuleTrace, static entry => entry.Rule == "Notch export"));
    }

    [Fact]
    public void CadRowEligibilityUsesUiThresholdsAndVersionLabels()
    {
        var vm = CreateViewModel(withGrid: true, cadMaxX: 20);
        var settings = FreeformHelperViewModelPrivateAccess.ProjectFile(vm).Settings.Notch;
        settings.ThresholdQ7 = 128;
        settings.ThresholdPercentV22 = 100;

        var cad = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            11, includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: true)?.Cad);

        Assert.True(cad.IsNotchRowEligible);
        Assert.Equal("Notch rows: 2 (v2.1, v2.2)", cad.NotchRowSummary);
        Assert.Equal(new PadInspectorRuleTraceEntry("Notch export", "Eligible (v2.1, v2.2)", "Anchor REG101, freeform=XWay."),
            Assert.Single(cad.RuleTrace, static entry => entry.Rule == "Notch export"));
    }

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(false, "Precomputed diagnostics", null)]
    [InlineData(true, "Precomputed diagnostics", "Precomputed diagnostics")]
    public void CadNotchDiagnosticsPreserveTheExpensiveFlagAndPrecomputedText(
        bool includeExpensiveDetails, string? precomputedDiagnostics, string? expectedDiagnostics)
    {
        var vm = CreateViewModel(withGrid: true);
        var resolved = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(11));

        var cad = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            11, includeExpensiveNotchDetails: includeExpensiveDetails, includeNotchRowEligibilityDetails: false,
            precomputedDiagnostics: precomputedDiagnostics)?.Cad);

        var notch = Assert.IsType<PadInspectorNotchSnapshot>(cad.Notch);
        Assert.Equal(expectedDiagnostics, notch.Diagnostics);
        Assert.Equal(resolved.Compensation.ToRegularRatio, notch.ToRegularRatio);
        Assert.Equal(resolved.Compensation.ToFullRatio, notch.ToFullRatio);
        Assert.Equal(resolved.Compensation.CombinedRatio, notch.CombinedRatio);
        Assert.Equal(resolved.Compensation.IsToFullEnabled, notch.IsToFullEnabled);
        Assert.Equal(resolved.Compensation.Stage3Area, notch.Stage3Area);
        Assert.Equal(resolved.TargetAllocation.StrictAreaThreshold, notch.StrictAreaThreshold);
        Assert.Equal(NotchComputationMode.CadAllocation, notch.ComputationMode);
        Assert.Same(resolved.TargetAllocation.TargetCoverageProjection, notch.TargetCoverageProjection);
        var target = Assert.Single(notch.Targets);
        var expectedTarget = Assert.Single(resolved.TargetAllocation.Targets);
        Assert.Equal(expectedTarget.IcIndex, target.IcIndex);
        Assert.Equal(expectedTarget.DiffIndex, target.DiffIndex);
        Assert.Equal(expectedTarget.EffectiveArea, target.EffectiveArea);
        Assert.Equal(expectedTarget.Ratio, target.Ratio);
        Assert.Equal(expectedTarget.RatioPercentRounded, target.RatioPercentRounded);
        Assert.Equal(expectedTarget.PassesStrictThreshold, target.PassesStrictThreshold);
        Assert.Equal(expectedTarget.IsAnchorDiff, target.IsAnchorDiff);
        Assert.Equal(expectedTarget.ToFullAppliedRegularCount, target.ToFullAppliedRegularCount);
        Assert.Equal(expectedTarget.RegularCount, target.RegularCount);
        Assert.Equal<int>(expectedTarget.RegularPadIds, target.RegularPadIds);
        Assert.Equal<PadInspectorNotchTargetRegularAreaSnapshot>(expectedTarget.RegularAreas
            .Select(static area => new PadInspectorNotchTargetRegularAreaSnapshot(area.RegularPadId, area.EffectiveArea)),
            target.RegularAreas);
    }

    [Fact]
    public void CadNotchDiagnosticsAreGeneratedWhenExpensiveDetailsHaveNoPrecomputedText()
    {
        var vm = CreateViewModel(withGrid: true);

        var cad = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            11, includeExpensiveNotchDetails: true, includeNotchRowEligibilityDetails: false)?.Cad);

        Assert.NotEmpty(Assert.IsType<PadInspectorNotchSnapshot>(cad.Notch).Diagnostics!);
    }

    [Fact]
    public void CadWithoutRegularOverlapKeepsTheReasonAndNullDiagnostics()
    {
        var vm = CreateViewModel(withGrid: true);

        var cad = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(12)?.Cad);

        Assert.False(cad.IsNotchRowEligible);
        Assert.Equal("Notch rows: 0 (No regular overlap.)", cad.NotchRowSummary);
        var notch = Assert.IsType<PadInspectorNotchSnapshot>(cad.Notch);
        Assert.Null(notch.Diagnostics);
        Assert.Empty(notch.Targets);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RegularMatchedFlagsPreserveSourcesDetailsAndTrace(bool includeMatchDetails, bool includeRuleTrace)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateViewModel();

        var result = Assert.IsType<PadInspectorSnapshot>(vm.BuildRegularPadInspectorSnapshot(
            101, includeMatchDetails, includeRuleTrace));

        var regular = Assert.IsType<RegularPadInspectorSnapshot>(result.Regular);
        Assert.Equal("regular", result.Kind);
        Assert.Null(result.Cad);
        Assert.Equal("CAD idx 7 (CAD id 11)", regular.MatchText);
        Assert.Equal(includeMatchDetails ? "CAD idx 7: 60%" : "Matched CAD pad(s): 1.", regular.MatchDetailsText);
        Assert.Equal<int>([11], regular.MatchedCadPadIds);
        Assert.Equal<PadInspectorMatchedCadSnapshot>(includeMatchDetails || includeRuleTrace
            ? [new(11, 7, 0.6, 0.8)] : [], regular.MatchedCadDetails);
        Assert.Equal(includeMatchDetails || includeRuleTrace ? "Strict" : "Computing...", regular.DiffSource);
        Assert.Equal(includeMatchDetails || includeRuleTrace ? "Step 2 auto-detect" : "Computing...", regular.FreeformSource);
        Assert.Equal(includeMatchDetails || includeRuleTrace
            ? "Current value comes from Step 2 auto-detect (XWay)." : "Computing...", regular.FreeformSourceDetail);
        Assert.Equal<PadInspectorRuleTraceEntry>(includeRuleTrace
            ? [new("Identity", "IC1/diff7", "R0  C0  REG101"),
               new("Match", "Matched CAD pad count: 1", "Best target: D7. Regular coverage: 80.0%. CAD coverage: 60.0%."),
               new("Diff source", "Strict", "Exact CAD Output FW Diff match: Yes."),
               new("Freeform", "XWay",
                   $"Source: Step 2 auto-detect{Environment.NewLine}Current value comes from Step 2 auto-detect (XWay).")]
            : [new("Computing", "Queued", "Detailed trace is loading in background.")], regular.RuleTrace);
        Assert.Equal(0, regular.DisplayRow);
        Assert.Equal(0.6, regular.MatchConfidence);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RegularUnmatchedFlagsPreserveNoneSourcesAndTrace(bool includeMatchDetails, bool includeRuleTrace)
    {
        var vm = CreateViewModel();

        var regular = Assert.IsType<RegularPadInspectorSnapshot>(vm.BuildRegularPadInspectorSnapshot(
            102, includeMatchDetails, includeRuleTrace)?.Regular);

        Assert.Equal("Unmatched", regular.MatchText);
        Assert.Equal("Unmatched", regular.MatchDetailsText);
        Assert.Empty(regular.MatchedCadPadIds);
        Assert.Empty(regular.MatchedCadDetails);
        Assert.Equal("Unmatched", regular.DiffSource);
        Assert.Equal("None", regular.FreeformSource);
        Assert.Equal("No Step 2 freeform tag.", regular.FreeformSourceDetail);
        Assert.Equal<PadInspectorRuleTraceEntry>(includeRuleTrace
            ? [new("Identity", "IC1/diff8", "R0  C1  REG102"),
               new("Match", "No matched CAD pad", "Best overlap target: none."),
               new("Diff source", "Unmatched", "Exact CAD Output FW Diff match: No."),
               new("Freeform", "None", $"Source: None{Environment.NewLine}No Step 2 freeform tag.")]
            : [new("Computing", "Queued", "Detailed trace is loading in background.")], regular.RuleTrace);
        Assert.Equal(0.0, regular.MatchConfidence);
    }

    [Fact]
    public void UnknownPadIdsReturnNull()
    {
        var vm = CreateViewModel();

        Assert.Null(vm.BuildCadPadInspectorSnapshot(-1));
        Assert.Null(vm.BuildRegularPadInspectorSnapshot(-1));
    }

    [Fact]
    public void RegularSnapshotConvertsTheInternalRowUsingTheCurrentGrid()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(3, 1);
        var vm = new FreeformHelperViewModel
        {
            RegularPads = new ObservableCollection<RegularPad>(grid.Pads),
        };
        FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;

        var regular = Assert.IsType<RegularPadInspectorSnapshot>(vm.BuildRegularPadInspectorSnapshot(
            grid.Pads[0].RegularPadId, includeMatchDetails: false, includeRuleTrace: false)?.Regular);

        Assert.Equal(0, regular.Row);
        Assert.Equal(2, regular.DisplayRow);
    }

    private static FreeformHelperViewModel CreateViewModel(bool withGrid = false, double cadMaxX = 5)
    {
        var cad = TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, cadMaxX, 10);
        var unmatchedCad = TestGeometryFactory.CreateCadPad(12, "unmatched", 30, 0, 40, 10);
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 101, 0, 0, 10, 10, diffIndex: 7);
        regular.Freeform = FreeformType.XWay;
        regular.MatchedCadPadId = cad.Id;
        var unmatchedRegular = TestGeometryFactory.CreateRegularPad(0, 1, 102, 10, 0, 20, 10, diffIndex: 8);
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>([cad, unmatchedCad]),
            RegularPads = new ObservableCollection<RegularPad>([regular, unmatchedRegular]),
            CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
            EnableToRegular = true,
            EnableToFull = false,
            EnableV21 = true,
            EnableV22 = true,
            LinkNotchThresholds = false,
            NotchThresholdQ7 = 0,
            NotchThresholdPercent = 0,
        };
        var links = new[] { new PadMatchLink(cad.Id, regular.RegularPadId, 30, 0.8, 0.6) };
        vm.WorkflowDataForTests.LatestPadMatch = new PadMatchResult(
            new Dictionary<int, IReadOnlyList<PadMatchLink>> { [cad.Id] = links },
            new Dictionary<int, IReadOnlyList<PadMatchLink>> { [regular.RegularPadId] = links });
        vm.CadIndexMaps.OutputFwDiffIndexByCadId[cad.Id] = 7;
        vm.CadIndexMaps.IcIndexByCadId[cad.Id] = 0;
        vm.InvalidateWorkflowDataSnapshot();
        FreeformHelperViewModelPrivateAccess.ProjectFile(vm).Settings.Notch.ComputationMode = NotchComputationMode.CadAllocation;
        if (withGrid)
        {
            FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet([cad, unmatchedCad]);
            FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(1, 2, [0, 10, 20], [0, 10], [regular, unmatchedRegular]);
        }

        return vm;
    }
}
