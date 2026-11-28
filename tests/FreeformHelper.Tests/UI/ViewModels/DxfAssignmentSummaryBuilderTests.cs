// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfAssignmentSummaryBuilderTests
{
    [AvaloniaFact]
    public void GetSummaryWithEmptyDecisionsReturnsEmptyData()
    {
        var vm = new FreeformHelperViewModel();

        var result = vm.GetCadOutputFwDiffAssignmentDecisionSummarySnapshot();

        Assert.Equal(CadOutputFwDiffAssignmentDecisionSummarySnapshot.Empty.DecisionCount, result.DecisionCount);
        Assert.Equal(CadOutputFwDiffAssignmentDecisionSummarySnapshot.Empty.Mode, result.Mode);
        Assert.Empty(result.ReasonCounts);
        Assert.Empty(result.DecisionSourceCounts);
    }

    [Fact]
    public void BuildChoosesMostFrequentModeAndOrdersCountsByFrequencyThenOrdinalKey()
    {
        CadOutputFwDiffAssignmentDecision[] decisions =
        [
            Decision(5, CadOutputFwDiffAssignmentMode.GeometryOnly,
                CadOutputFwDiffAssignmentReasonCode.NeedsReview, CadOutputFwDiffAssignmentDecisionSource.Seed),
            Decision(6, CadOutputFwDiffAssignmentMode.CsvConstrained,
                CadOutputFwDiffAssignmentReasonCode.InactiveBlocked, CadOutputFwDiffAssignmentDecisionSource.AutoRepaired),
            Decision(7, CadOutputFwDiffAssignmentMode.CsvConstrained,
                CadOutputFwDiffAssignmentReasonCode.DuplicateConflict, CadOutputFwDiffAssignmentDecisionSource.ManualOverride),
            Decision(8, CadOutputFwDiffAssignmentMode.GeometryOnly,
                CadOutputFwDiffAssignmentReasonCode.GeometryOnlySuggestion, CadOutputFwDiffAssignmentDecisionSource.PropagatedOverride),
            Decision(9, CadOutputFwDiffAssignmentMode.CsvConstrained,
                CadOutputFwDiffAssignmentReasonCode.NeedsReview, CadOutputFwDiffAssignmentDecisionSource.Seed),
            Decision(10, CadOutputFwDiffAssignmentMode.CsvConstrained,
                CadOutputFwDiffAssignmentReasonCode.DuplicateConflict, CadOutputFwDiffAssignmentDecisionSource.ManualOverride),
        ];
        KeyValuePair<string, int>[] expectedReasons =
        [
            new("duplicate-conflict", 2), new("needs-review", 2),
            new("geometry-only-suggestion", 1), new("inactive-blocked", 1),
        ];
        KeyValuePair<string, int>[] expectedSources =
        [
            new("manual-override", 2), new("seed", 2), new("auto-repaired", 1), new("propagated-override", 1),
        ];

        var result = DxfAssignmentSummaryBuilder.Build(decisions);

        Assert.Equal(6, result.DecisionCount);
        Assert.Equal("csv-constrained", result.Mode);
        Assert.Equal(expectedReasons, result.ReasonCounts.ToArray());
        Assert.Equal(expectedSources, result.DecisionSourceCounts.ToArray());
    }

    [Fact]
    public void BuildBreaksModeCountTieByModeKeyRegardlessOfInputOrder()
    {
        CadOutputFwDiffAssignmentDecision[] decisions =
        [
            CadOutputFwDiffAssignmentDecision.Empty(5, 0, CadOutputFwDiffAssignmentMode.CsvConstrained),
            CadOutputFwDiffAssignmentDecision.Empty(6, 1, CadOutputFwDiffAssignmentMode.GeometryOnly),
        ];

        var result = DxfAssignmentSummaryBuilder.Build(decisions);

        Assert.Equal(2, result.DecisionCount);
        Assert.Equal("geometry-only", result.Mode);
        Assert.Equal(new KeyValuePair<string, int>("needs-review", 2), Assert.Single(result.ReasonCounts));
        Assert.Equal(new KeyValuePair<string, int>("seed", 2), Assert.Single(result.DecisionSourceCounts));
    }

    private static CadOutputFwDiffAssignmentDecision Decision(
        int cadPadId,
        CadOutputFwDiffAssignmentMode mode,
        CadOutputFwDiffAssignmentReasonCode reason,
        CadOutputFwDiffAssignmentDecisionSource source) =>
        CadOutputFwDiffAssignmentDecision.Empty(cadPadId, cadPadId - 5, mode) with
        {
            ReasonCode = reason,
            DecisionSource = source,
        };
}
