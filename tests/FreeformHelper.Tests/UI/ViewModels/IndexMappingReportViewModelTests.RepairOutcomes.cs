// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{
    [Fact]
    public void ApplyDiffOverrideCommand_WhenUnavailableOrRejected_ReportsReasonAndKeepsDecision()
    {
        using var culture = TestCulture.UseEnUs();
        var report = CreateRepairOutcomeReport(CreateRepairOutcomeRow(9011, 22));
        var unavailable = new IndexMappingReportViewModel("summary", report);

        unavailable.ApplyDiffOverrideCommand.Execute(null);

        Assert.Equal("Diff override handler not available.", unavailable.ActionStatus);
        var applyCalls = new List<(int CadId, int DiffIndex)>();
        var rejected = new IndexMappingReportViewModel(
            "summary", report,
            applyDiffOverride: (cadId, diffIndex) =>
            {
                applyCalls.Add((cadId, diffIndex));
                return false;
            });
        var selected = Assert.Single(rejected.DecisionRows);
        var originalDisplay = selected.CurrentDisplay;

        rejected.ApplyDiffOverrideCommand.Execute(null);

        Assert.Equal((9011, 22), Assert.Single(applyCalls));
        Assert.Equal("Failed to apply diff repair override.", rejected.ActionStatus);
        Assert.Same(selected, rejected.SelectedDecision);
        Assert.Equal(originalDisplay, selected.CurrentDisplay);
        Assert.Null(selected.OverrideRegularPadIndex);
    }

    [Fact]
    public void ApplySegmentDiffOverridesCommand_WhenFilteredAndPartiallyRejected_StillRepairsHiddenSegmentMember()
    {
        using var culture = TestCulture.UseEnUs();
        var report = CreateRepairOutcomeReport(
            CreateRepairOutcomeRow(9101, 100, memberCount: 2),
            CreateRepairOutcomeRow(9102, 101, memberCount: 2),
            CreateRepairOutcomeRow(9103, 110, icIndex: 0),
            CreateRepairOutcomeRow(9104, 120, rowIndex: 8),
            CreateRepairOutcomeRow(9105, 130, segmentIndex: 1));
        var applyCalls = new List<(int CadId, int DiffIndex)>();
        var viewModel = new IndexMappingReportViewModel(
            "summary", report,
            applyDiffOverride: (cadId, diffIndex) =>
            {
                applyCalls.Add((cadId, diffIndex));
                return cadId != 9101;
            })
        {
            SearchKeyword = "CAD 9101",
        };
        var selected = Assert.Single(viewModel.DecisionRows);
        Assert.Equal(9101, selected.CadPadId);
        Assert.True(viewModel.CanApplySelectedSegmentDiffOverrides);
        Assert.Equal("Segment preview: IC 1, row 7, segment 0, repair rows 2/2.", viewModel.SelectedSegmentPreviewText);

        viewModel.ApplySegmentDiffOverridesCommand.Execute(null);

        Assert.Equal(2, applyCalls.Count);
        Assert.Contains((9101, 100), applyCalls);
        Assert.Contains((9102, 101), applyCalls);
        Assert.Equal("Segment diff repair applied for 1/2 rows.", viewModel.ActionStatus);
        Assert.Same(selected, viewModel.SelectedDecision);
        Assert.Same(selected, Assert.Single(viewModel.DecisionRows));
    }

    [Fact]
    public void ApplyVisibleDiffOverridesCommand_WhenOneRepairFails_ContinuesAndCountsOnlySuccessfulSuggestions()
    {
        using var culture = TestCulture.UseEnUs();
        var report = CreateRepairOutcomeReport(
            CreateRepairOutcomeRow(9201, 200), CreateRepairOutcomeRow(9202, 201),
            CreateRepairOutcomeRow(9203, 202), CreateRepairOutcomeRow(9204, null));
        var applyCalls = new List<(int CadId, int DiffIndex)>();
        var viewModel = new IndexMappingReportViewModel(
            "summary", report,
            applyDiffOverride: (cadId, diffIndex) =>
            {
                applyCalls.Add((cadId, diffIndex));
                return cadId != 9201;
            });
        Assert.Equal(4, viewModel.DecisionRows.Count);

        viewModel.ApplyCadOutputFwDiffOverridesCommand.Execute(null);

        Assert.Equal(3, applyCalls.Count);
        Assert.Contains((9201, 200), applyCalls);
        Assert.Contains((9202, 201), applyCalls);
        Assert.Contains((9203, 202), applyCalls);
        Assert.Equal("Visible diff repairs applied for 2/3 rows.", viewModel.ActionStatus);

        viewModel.SearchKeyword = "no matching CAD";
        Assert.Empty(viewModel.DecisionRows);
        Assert.False(viewModel.CanApplyCadOutputFwDiffOverrides);
        viewModel.ApplyCadOutputFwDiffOverridesCommand.Execute(null);
        Assert.Equal("No visible rows have diff repair suggestions.", viewModel.ActionStatus);
        Assert.Equal(3, applyCalls.Count);
    }

    private static DxfRegularMappingReport CreateRepairOutcomeReport(params DxfRegularMaskAuditRow[] rows)
    {
        return new DxfRegularMappingReport(
            CadCount: rows.Length, RegularCount: rows.Length, MappedCount: rows.Length,
            UnmappedCadCount: 0, UnmappedRegularCount: 0,
            LowConfidenceCount: 0, AmbiguousCount: 0,
            HasIssues: true, Summary: "summary", TotalIssueCount: 0, IssuesTruncated: false,
            Issues: [], SampleIssues: [], MaskAuditRows: rows, MaskChangedCount: rows.Length);
    }

    private static DxfRegularMaskAuditRow CreateRepairOutcomeRow(
        int cadId, int? repairDiffIndex, int icIndex = 1, int rowIndex = 7, int segmentIndex = 0, int memberCount = 1)
    {
        var diffIndex = repairDiffIndex ?? 500;
        var seed = new CadBestMatchSeed(cadId + 1000, icIndex, diffIndex, 0.9, 0.9);
        return new DxfRegularMaskAuditRow(
            CadPadId: cadId, OrderedCadIndex: cadId,
            RawSeed: seed, MaskedSeed: seed, CsvConfirmedCandidates: [], CsvConfirmedConfidence: 0.9,
            SuggestedDiffIndex: repairDiffIndex, CurrentAssignedDiffIndex: diffIndex - 2,
            Status: DxfRegularMaskAuditStatus.ChangedByMask, Message: "CSV repair candidate",
            Mode: CadOutputFwDiffAssignmentMode.CsvConstrained,
            ReasonCode: CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected,
            DecisionSource: CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
            RepairSuggestionDiffIndex: repairDiffIndex, IcIndex: icIndex, RowIndex: rowIndex,
            SegmentIndex: segmentIndex, SegmentMemberCount: memberCount);
    }
}
