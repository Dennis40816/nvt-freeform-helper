// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingReportViewModelTests
{
    [Fact]
    public void MappingOverrideCommands_WhenRetriedAfterRejection_UpdateDecisionAndIssueOnlyOnSuccess()
    {
        using var culture = TestCulture.UseEnUs();
        var acceptApply = false;
        var acceptClear = false;
        var applyCalls = new List<(int CadId, int RegularId)>();
        var clearCalls = new List<int>();
        var viewModel = new IndexMappingReportViewModel(
            "summary", CreateOverrideOutcomeReport(),
            applyOverride: (cadId, regularId) =>
            {
                applyCalls.Add((cadId, regularId));
                return acceptApply;
            },
            clearOverride: cadId =>
            {
                clearCalls.Add(cadId);
                return acceptClear;
            },
            getOverrideRegularIndex: static _ => 9);
        var row = Assert.Single(viewModel.DecisionRows);
        var issue = Assert.Single(viewModel.Issues);
        Assert.Same(issue, viewModel.SelectedIssue);

        viewModel.ApplyOverrideCommand.Execute(null);

        Assert.Equal("Failed to apply mapping override.", viewModel.ActionStatus);
        Assert.Equal(9, row.OverrideRegularPadIndex);
        Assert.Equal(9, issue.OverrideRegularPadIndex);
        acceptApply = true;
        viewModel.ApplyOverrideCommand.Execute(null);
        Assert.Equal("Mapping override applied.", viewModel.ActionStatus);
        Assert.Equal(88, row.OverrideRegularPadIndex);
        Assert.Equal(88, issue.OverrideRegularPadIndex);
        Assert.Equal("Override: CAD -> regular id 88.", row.OverrideText);

        viewModel.ClearOverrideCommand.Execute(null);

        Assert.Equal("Failed to clear mapping override.", viewModel.ActionStatus);
        Assert.Equal(88, row.OverrideRegularPadIndex);
        Assert.Equal(88, issue.OverrideRegularPadIndex);
        acceptClear = true;
        viewModel.ClearOverrideCommand.Execute(null);
        Assert.Equal("Mapping override cleared.", viewModel.ActionStatus);
        Assert.Null(row.OverrideRegularPadIndex);
        Assert.Null(issue.OverrideRegularPadIndex);
        Assert.Equal("Override: none.", row.OverrideText);
        Assert.Equal(2, applyCalls.Count);
        Assert.All(applyCalls, static call => Assert.Equal((777, 88), call));
        Assert.Equal(2, clearCalls.Count);
        Assert.All(clearCalls, static cadId => Assert.Equal(777, cadId));
        Assert.Same(row, viewModel.SelectedDecision);
    }

    [Fact]
    public void LocateSelectedIssueCommand_WhenTargetIsHidden_ReportsFailureAndKeepsSelection()
    {
        var locateCalls = new List<(int? CadId, int? RegularId)>();
        var viewModel = new IndexMappingReportViewModel(
            "summary", CreateOverrideOutcomeReport(),
            locateTarget: (cadId, regularId) =>
            {
                locateCalls.Add((cadId, regularId));
                return false;
            });
        var selected = Assert.Single(viewModel.DecisionRows);

        viewModel.LocateSelectedIssueCommand.Execute(null);

        var call = Assert.Single(locateCalls);
        Assert.Equal(777, call.CadId);
        Assert.Equal(88, call.RegularId);
        Assert.Equal("Diagnostic target is not visible.", viewModel.ActionStatus);
        Assert.Same(selected, viewModel.SelectedDecision);
        Assert.True(selected.IsInspectorSelected);
    }

    private static DxfRegularMappingReport CreateOverrideOutcomeReport()
    {
        var issue = new DxfRegularMappingIssue(
            DxfRegularMappingIssueKind.LowConfidence, "Low confidence CAD mapping",
            DxfIndex: 77, CadPadId: 777, RegularPadIndex: 88, DiffIndex: 88);
        return new DxfRegularMappingReport(
            CadCount: 1, RegularCount: 1, MappedCount: 1,
            UnmappedCadCount: 0, UnmappedRegularCount: 0,
            LowConfidenceCount: 1, AmbiguousCount: 0,
            HasIssues: true, Summary: "summary", TotalIssueCount: 1, IssuesTruncated: false,
            Issues: [issue], SampleIssues: [issue]);
    }
}
