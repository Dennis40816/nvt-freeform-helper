// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class IndexMappingRepairEffectTests
{
    [AvaloniaFact]
    public async Task ApplyDiffOverrideChangesOnlySelectedPadInfoAndReanalysisKeepsRepairAsync()
    {
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var cadId = selected.CadPadId!.Value;
        var before = CapturePadInfo(vm);
        var originalDecision = CaptureDecision(selected);
        var suggested = selected.ApplyDiffIndex!.Value;
        Assert.NotEqual(before[cadId].DiffIndex, suggested);

        report.ApplyDiffOverrideCommand.Execute(null);

        Assert.Equal(suggested, CapturePadInfo(vm)[cadId].DiffIndex);
        Assert.Equal(suggested, CapturePadInfo(vm)[cadId].Override);
        AssertOtherPadsUnchanged(vm, before, [cadId]);
        // The open report is a snapshot; Analyze is the entry that refreshes its decisions.
        Assert.Equal(originalDecision, CaptureDecision(selected));
        var refreshed = await RepairWorkspace.AnalyzeAsync(vm);
        var decision = GetRepair(refreshed, cadId);
        Assert.Equal($"diff {suggested}", decision.CurrentDisplay);
        Assert.NotEqual(originalDecision, CaptureDecision(decision));
        Assert.Equal(suggested, CapturePadInfo(vm)[cadId].DiffIndex);
        AssertOtherPadsUnchanged(vm, before, [cadId]);
    }

    [AvaloniaFact]
    public async Task ApplySegmentDiffOverridesChangesEverySegmentMemberAndNoOtherPadAsync()
    {
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var members = report.DecisionRows.Where(row =>
                row.FilterMode == IndexMappingDecisionFilterMode.ChangedByMask &&
                row.IcIndex == selected.IcIndex && row.RowIndex == selected.RowIndex &&
                row.SegmentIndex == selected.SegmentIndex)
            .ToArray();
        Assert.Equal(2, members.Length);
        Assert.All(members, row => Assert.Equal(2, row.SegmentMemberCount));
        var before = CapturePadInfo(vm);
        var expected = members.ToDictionary(row => row.CadPadId!.Value, row => row.ApplyDiffIndex!.Value);
        Assert.All(expected, pair => Assert.NotEqual(before[pair.Key].DiffIndex, pair.Value));

        report.ApplySegmentDiffOverridesCommand.Execute(null);

        var after = CapturePadInfo(vm);
        Assert.All(expected, pair => Assert.Equal(pair.Value, after[pair.Key].DiffIndex));
        AssertOtherPadsUnchanged(vm, before, expected.Keys);
        var refreshed = await RepairWorkspace.AnalyzeAsync(vm);
        Assert.All(expected, pair => Assert.Equal($"diff {pair.Value}", GetRepair(refreshed, pair.Key).CurrentDisplay));
    }

    [AvaloniaFact]
    public async Task ApplyOutputFwDiffOverridesChangesVisibleRepairAndNoHiddenPadAsync()
    {
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var cadId = selected.CadPadId!.Value;
        report.SearchKeyword = selected.Title;
        Assert.Same(selected, Assert.Single(report.DecisionRows));
        var before = CapturePadInfo(vm);

        report.ApplyCadOutputFwDiffOverridesCommand.Execute(null);

        Assert.Equal(selected.ApplyDiffIndex, CapturePadInfo(vm)[cadId].DiffIndex);
        Assert.NotEqual(before[cadId], CapturePadInfo(vm)[cadId]);
        AssertOtherPadsUnchanged(vm, before, [cadId]);
    }

    [AvaloniaFact]
    public async Task ClearReportOverrideAfterDiffRepairReportsFailureAndKeepsRepairedPadInfoAsync()
    {
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var cadId = selected.CadPadId!.Value;
        var before = CapturePadInfo(vm);
        report.ApplyDiffOverrideCommand.Execute(null);
        var repaired = CapturePadInfo(vm);
        Assert.NotEqual(before[cadId], repaired[cadId]);

        report.ClearOverrideCommand.Execute(null);

        Assert.Equal("Failed to clear mapping override.", report.ActionStatus);
        AssertPadInfoEquals(repaired, CapturePadInfo(vm));
        Assert.Equal(selected.ApplyDiffIndex, vm.GetCadOutputFwDiffIndexOverride(cadId));
        var refreshed = await RepairWorkspace.AnalyzeAsync(vm);
        Assert.Equal($"diff {selected.ApplyDiffIndex}", GetRepair(refreshed, cadId).CurrentDisplay);
    }

    [AvaloniaFact]
    public async Task UndoDiffRepairRestoresPreApplyPadInfoAndReanalyzedDecisionAsync()
    {
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var cadId = selected.CadPadId!.Value;
        var before = CapturePadInfo(vm);
        var decisionBefore = CaptureDecision(selected);
        report.ApplyDiffOverrideCommand.Execute(null);
        Assert.NotEqual(before[cadId], CapturePadInfo(vm)[cadId]);
        Assert.True(vm.UndoCommand.CanExecute(null));

        vm.UndoCommand.Execute(null);

        AssertPadInfoEquals(before, CapturePadInfo(vm));
        var refreshed = await RepairWorkspace.AnalyzeAsync(vm);
        Assert.Equal(decisionBefore, CaptureDecision(GetRepair(refreshed, cadId)));
    }

    [AvaloniaFact]
    public async Task LocateIssueReplacesCanvasSelectionWithItsCadAndRegularPadAsync()
    {
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var otherCadId = vm.CadPads.First(pad => pad.Id != selected.CadPadId).Id;
        vm.ApplyCanvasSelection([otherCadId], []);

        report.LocateSelectedIssueCommand.Execute(null);

        Assert.Equal(selected.CadPadId, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(selected.LocateRegularPadIndex, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Diagnostic row focused on canvas.", report.ActionStatus);
    }

    [AvaloniaFact]
    public async Task SelectVerificationStepKeepsExistingCanvasSelectionAsync()
    {
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var otherCadId = vm.CadPads.First(pad => pad.Id != selected.CadPadId).Id;
        var otherRegularId = vm.RegularPads.First(pad => pad.RegularPadId != selected.LocateRegularPadIndex).RegularPadId;
        vm.ApplyCanvasSelection([otherCadId], [otherRegularId]);

        report.SelectVerificationStepCommand.Execute(IndexMappingVerificationStep.Step6);

        Assert.Equal(IndexMappingVerificationStep.Step6, report.ActiveVerificationStep);
        Assert.Same(selected, report.SelectedDecision);
        Assert.Equal(otherCadId, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(otherRegularId, Assert.Single(vm.SelectedRegularPadIndices));
    }
}
