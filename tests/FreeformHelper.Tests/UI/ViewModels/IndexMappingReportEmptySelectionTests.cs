// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class IndexMappingReportEmptySelectionTests
{
    [Fact]
    public void SelectDecisionCommand_EmptyParameter_PreservesFocusedEntityAndStatus()
    {
        using var culture = TestCulture.UseEnUs();
        DxfRegularMappingIssue[] issues =
        [
            new(DxfRegularMappingIssueKind.Ambiguous, "CAD 701 has competing candidates.",
                CadPadId: 701, RegularPadIndex: 81, DiffIndex: 11),
            new(DxfRegularMappingIssueKind.LowConfidence, "CAD 777 needs mapping review.",
                CadPadId: 777, RegularPadIndex: 88, DiffIndex: 18),
        ];
        var report = new DxfRegularMappingReport(
            CadCount: 2, RegularCount: 2, MappedCount: 2, UnmappedCadCount: 0, UnmappedRegularCount: 0,
            LowConfidenceCount: 1, AmbiguousCount: 1, HasIssues: true, Summary: "Mapping audit",
            TotalIssueCount: issues.Length, IssuesTruncated: false, Issues: issues, SampleIssues: issues);
        var locateCalls = new List<(int? CadId, int? RegularId)>();
        var vm = new IndexMappingReportViewModel("Mapping audit", report,
            locateTarget: (cadId, regularId) =>
            {
                locateCalls.Add((cadId, regularId));
                return true;
            });
        var selected = Assert.Single(vm.DecisionRows, row => row.CadPadId == 777);
        var other = Assert.Single(vm.DecisionRows, row => row.CadPadId == 701);
        vm.SelectDecisionCommand.Execute(selected);

        vm.SelectDecisionCommand.Execute(null);

        Assert.Same(selected, vm.SelectedDecision);
        Assert.Same(selected.Issue, vm.SelectedIssue);
        Assert.True(selected.IsInspectorSelected);
        Assert.False(other.IsInspectorSelected);
        Assert.Equal("CAD 777", vm.SelectedEntityText);
        Assert.Equal("Diagnostic row focused on canvas.", vm.ActionStatus);
        var located = Assert.Single(locateCalls);
        Assert.Equal(777, located.CadId);
        Assert.Equal(88, located.RegularId);
    }
}
