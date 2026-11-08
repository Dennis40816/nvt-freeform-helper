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

    [Fact]
    public void DecisionActions_AfterSearchHidesAllRows_ReportMissingSelectionWithoutCallingHandlers()
    {
        using var culture = TestCulture.UseEnUs();
        var calls = new List<string>();
        var vm = CreateActionAvailabilityViewModel(CreateRepairOutcomeReport(CreateRepairOutcomeRow(9011, 22)), calls);
        var previousSelection = Assert.Single(vm.DecisionRows);

        vm.SearchKeyword = "no matching CAD or REG";

        Assert.Null(vm.SelectedDecision);
        Assert.False(previousSelection.IsInspectorSelected);
        Assert.Equal("Segment preview: select a row.", vm.SelectedSegmentPreviewText);
        var commands = new[]
        {
            vm.LocateSelectedIssueCommand, vm.ApplyOverrideCommand, vm.ApplyDiffOverrideCommand,
            vm.ApplySegmentDiffOverridesCommand, vm.ClearOverrideCommand,
        };
        foreach (var command in commands)
        {
            vm.ActionStatus = string.Empty;

            command.Execute(null);

            Assert.Equal("Select a row first.", vm.ActionStatus);
            Assert.Null(vm.SelectedDecision);
        }

        Assert.Empty(calls);
        Assert.Null(previousSelection.OverrideRegularPadIndex);
    }

    [Fact]
    public void DecisionActions_OnCountMismatchDiagnostic_ReportUnsupportedActionsWithoutCallingHandlers()
    {
        using var culture = TestCulture.UseEnUs();
        var calls = new List<string>();
        var vm = CreateActionAvailabilityViewModel(
            CreateVerificationFallbackReport(IndexMappingDecisionFilterMode.CountMismatch), calls);
        var aggregate = Assert.Single(vm.AggregateDecisionRows);

        vm.SelectDecisionCommand.Execute(aggregate);

        Assert.Same(aggregate, vm.SelectedDecision);
        Assert.Equal("Segment preview: selected row has no segment metadata.", vm.SelectedSegmentPreviewText);
        var actions = new[]
        {
            (vm.LocateSelectedIssueCommand, "Selected row has no locate target."),
            (vm.ApplyOverrideCommand, "Selected row cannot create an override."),
            (vm.ApplyDiffOverrideCommand, "Selected row has no diff repair suggestion."),
            (vm.ApplySegmentDiffOverridesCommand, "Selected row has no segment metadata."),
            (vm.ClearOverrideCommand, "Selected row has no CAD override to clear."),
        };
        foreach (var (command, expectedStatus) in actions)
        {
            vm.ActionStatus = string.Empty;

            command.Execute(null);

            Assert.Equal(expectedStatus, vm.ActionStatus);
            Assert.Same(aggregate, vm.SelectedDecision);
        }

        Assert.Empty(calls);
        Assert.True(aggregate.IsInspectorSelected);
        Assert.Null(aggregate.OverrideRegularPadIndex);
    }

    [Fact]
    public void DecisionActions_WithoutOptionalHandlers_ReportUnavailableAndPreserveExistingOverride()
    {
        using var culture = TestCulture.UseEnUs();
        var report = CreateRepairOutcomeReport(CreateRepairOutcomeRow(9011, 22));
        var vm = new IndexMappingReportViewModel("mapping", report, getOverrideRegularIndex: static _ => 66);
        var selected = Assert.Single(vm.DecisionRows);
        Assert.True(vm.CanLocateSelectedDecision);
        Assert.True(vm.CanApplySelectedIssueOverride);
        Assert.True(vm.CanApplySelectedSegmentDiffOverrides);
        Assert.True(vm.CanApplyCadOutputFwDiffOverrides);
        Assert.True(vm.CanClearSelectedIssueOverride);
        var actions = new[]
        {
            (vm.LocateSelectedIssueCommand, "Locate handler not available."),
            (vm.ApplyOverrideCommand, "Override handler not available."),
            (vm.ClearOverrideCommand, "Override clear handler not available."),
            (vm.ApplySegmentDiffOverridesCommand, "Diff override handler not available."),
            (vm.ApplyCadOutputFwDiffOverridesCommand, "Diff override handler not available."),
        };
        foreach (var (command, expectedStatus) in actions)
        {
            vm.ActionStatus = string.Empty;

            command.Execute(null);

            Assert.Equal(expectedStatus, vm.ActionStatus);
            Assert.Same(selected, vm.SelectedDecision);
            Assert.Equal(66, selected.OverrideRegularPadIndex);
            Assert.Equal("Override: CAD -> regular id 66.", selected.OverrideText);
        }
    }

    [Fact]
    public void ApplySegmentDiffOverrides_WithoutSuggestions_ReportsEmptySegmentAndKeepsSelection()
    {
        using var culture = TestCulture.UseEnUs();
        var calls = new List<string>();
        var report = CreateRepairOutcomeReport(CreateRepairOutcomeRow(9011, null, memberCount: 3));
        var vm = CreateActionAvailabilityViewModel(report, calls);
        var selected = Assert.Single(vm.DecisionRows);
        Assert.True(selected.HasSegmentScope);
        Assert.False(vm.CanApplySelectedSegmentDiffOverrides);
        Assert.Equal("Segment preview: IC 1, row 7, segment 0, repair rows 0/3.", vm.SelectedSegmentPreviewText);

        vm.ApplySegmentDiffOverridesCommand.Execute(null);

        Assert.Equal("Selected segment has no diff repair suggestions.", vm.ActionStatus);
        Assert.Empty(calls);
        Assert.Same(selected, vm.SelectedDecision);
        Assert.Null(selected.OverrideRegularPadIndex);
    }

    private static IndexMappingReportViewModel CreateActionAvailabilityViewModel(
        DxfRegularMappingReport report, List<string> calls)
    {
        return new IndexMappingReportViewModel("mapping", report,
            locateTarget: (_, _) =>
            {
                calls.Add("locate");
                return true;
            },
            applyOverride: (_, _) =>
            {
                calls.Add("regular override");
                return true;
            },
            applyDiffOverride: (_, _) =>
            {
                calls.Add("diff override");
                return true;
            },
            clearOverride: _ =>
            {
                calls.Add("clear override");
                return true;
            });
    }
}
