// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{
    [Fact]
    public void ClearOverrideCommand_RejectedThenRetried_PreservesOverrideUntilClearSucceeds()
    {
        using var culture = TestCulture.UseEnUs();
        var accepted = false;
        var calls = new List<int>();
        var vm = new IndexMappingReportViewModel("mapping", CreateClearOutcomeReport(),
            clearOverride: cadId =>
            {
                calls.Add(cadId);
                return accepted;
            },
            getOverrideRegularIndex: _ => 66);
        var row = Assert.Single(vm.DecisionRows);
        var issue = Assert.Single(vm.Issues);

        vm.ClearOverrideCommand.Execute(null);

        Assert.Equal(777, Assert.Single(calls));
        Assert.Equal("Failed to clear mapping override.", vm.ActionStatus);
        Assert.Equal(66, row.OverrideRegularPadIndex);
        Assert.Equal(66, issue.OverrideRegularPadIndex);

        accepted = true;
        vm.ClearOverrideCommand.Execute(null);

        Assert.Equal(2, calls.Count);
        Assert.All(calls, cadId => Assert.Equal(777, cadId));
        Assert.Equal("Mapping override cleared.", vm.ActionStatus);
        Assert.Null(row.OverrideRegularPadIndex);
        Assert.Null(issue.OverrideRegularPadIndex);
        Assert.Equal("Override: none.", row.OverrideText);
    }

    [Fact]
    public void ApplyCadOutputFwDiffOverridesCommand_WithPassiveSuggestionAndRejection_CountsOnlySuccessfulRepairRows()
    {
        using var culture = TestCulture.UseEnUs();
        var report = CreateVisibleRowsReport();
        var rows = Assert.IsAssignableFrom<IReadOnlyList<DxfRegularMaskAuditRow>>(report.MaskAuditRows);
        var issue = new DxfRegularMappingIssue(DxfRegularMappingIssueKind.LowConfidence,
            "Review CAD 9300", CadPadId: 9300, RegularPadIndex: 700, DiffIndex: 300);
        report = report with
        {
            CadCount = 3,
            RegularCount = 3,
            MappedCount = 3,
            LowConfidenceCount = 1,
            HasIssues = true,
            TotalIssueCount = 1,
            Issues = new[] { issue },
            SampleIssues = new[] { issue },
            MaskAuditRows = new[]
            {
                rows[0],
                rows[1] with
                {
                    RepairSuggestionDiffIndex = null,
                    SuggestedDiffIndex = null,
                    PassiveCompensationDiffIndex = 201,
                },
            },
        };
        var calls = new List<(int CadId, int DiffIndex)>();
        var vm = new IndexMappingReportViewModel("mapping", report,
            applyDiffOverride: (cadId, diffIndex) =>
            {
                calls.Add((cadId, diffIndex));
                return cadId == 9202;
            });
        Assert.Equal(3, vm.DecisionRows.Count);

        vm.ApplyCadOutputFwDiffOverridesCommand.Execute(null);

        Assert.Equal(new[] { (9201, 200), (9202, 201) }, calls);
        Assert.Equal("Visible diff repairs applied for 1/2 rows.", vm.ActionStatus);

        vm.SearchKeyword = "9300";
        Assert.Single(vm.DecisionRows);
        vm.ApplyCadOutputFwDiffOverridesCommand.Execute(null);

        Assert.Equal(2, calls.Count);
        Assert.Equal("No visible rows have diff repair suggestions.", vm.ActionStatus);
    }

    private static DxfRegularMappingReport CreateClearOutcomeReport()
    {
        var issue = new DxfRegularMappingIssue(DxfRegularMappingIssueKind.LowConfidence,
            "Review CAD 777", DxfIndex: 77, CadPadId: 777, RegularPadIndex: 88, DiffIndex: 88);
        return new DxfRegularMappingReport(
            CadCount: 1,
            RegularCount: 1,
            MappedCount: 1,
            UnmappedCadCount: 0,
            UnmappedRegularCount: 0,
            LowConfidenceCount: 1,
            AmbiguousCount: 0,
            HasIssues: true,
            Summary: "One mapping needs review",
            TotalIssueCount: 1,
            IssuesTruncated: false,
            Issues: new[] { issue },
            SampleIssues: new[] { issue });
    }
}
