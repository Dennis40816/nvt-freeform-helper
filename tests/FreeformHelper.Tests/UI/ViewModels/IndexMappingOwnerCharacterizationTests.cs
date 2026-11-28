// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class IndexMappingOwnerCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RebuildAndEmptyInputRetainMapAndStep4IdentityAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var maps = vm.CadIndexMaps;
        var diff = maps.OutputFwDiffIndexByCadId;
        var display = maps.DisplayIndexByCadId;
        var ic = maps.IcIndexByCadId;
        var step4 = vm.Step4Index;
        var command = vm.OffsetSelectedCadOutputFwDiffIndicesCommand;
        var pads = vm.CadPads.ToArray();

        FreeformHelperViewModelPrivateAccess.UpdateCadOutputFwDiffIndexing(vm, []);

        Assert.Same(maps, vm.CadIndexMaps);
        Assert.Same(diff, maps.OutputFwDiffIndexByCadId);
        Assert.Same(display, maps.DisplayIndexByCadId);
        Assert.Same(ic, maps.IcIndexByCadId);
        Assert.Empty(diff);
        Assert.Empty(display);
        Assert.Empty(ic);
        Assert.Empty(maps.LatestAssignmentDecisionsByCadId);
        Assert.Empty(vm.Step4DuplicateDiffGroups);
        Assert.False(vm.HasStep4DuplicateDiffGroups);
        Assert.Equal("CAD Output FW Diff assignment: no CAD output pads.", vm.CadOutputFwDiffAssignmentSummary);

        FreeformHelperViewModelPrivateAccess.UpdateCadOutputFwDiffIndexing(vm, pads);

        Assert.Same(maps, vm.CadIndexMaps);
        Assert.Same(step4, vm.Step4Index);
        Assert.Same(command, vm.OffsetSelectedCadOutputFwDiffIndicesCommand);
        Assert.Equal(pads.Length, display.Count);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task DisplayOrderIsOneBasedAndIndependentOfFwDiffAndIcAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("bottom-right", 30, 0, 40, 10), ("top-left", 0, 10, 10, 20),
            ("bottom-left", 0, 0, 10, 10), ("top-right", 30, 10, 40, 20));
        Assert.Single(vm.CascadeIcSettings).YChannels = 2;
        vm.SelectedScanOrder = ScanOrder.RightToLeft_BottomToTop;
        await vm.WaitForGridRebuildIdleAsync();
        var topLeft = Assert.Single(vm.CadPads, pad => pad.Layer == "top-left");
        vm.SetCadOutputFwDiffIndexOverride(topLeft.Id, 37);
        var expected = new[] { "top-left", "top-right", "bottom-left", "bottom-right" };

        var actual = vm.CadPads.OrderBy(pad => vm.CadIndexMaps.DisplayIndexByCadId[pad.Id]).ToArray();

        Assert.Equal(expected, actual.Select(pad => pad.Layer));
        Assert.Equal<int>([1, 2, 3, 4], actual.Select(pad => vm.CadIndexMaps.DisplayIndexByCadId[pad.Id]));
        Assert.True(vm.TryGetCadDisplayIndex(topLeft.Id, out var display));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(topLeft.Id, out var diff));
        Assert.True(vm.TryGetCadIcIndex(topLeft.Id, out var ic));
        Assert.Equal(1, display);
        Assert.Equal(37, diff);
        Assert.Equal(0, ic);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LegacyAnchorMigrationPreservesExistingAndInvisibleChoicesAsync(bool existing, bool invisible)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var legacy = Assert.Single(vm.CadPads, pad => pad.Layer == "left-a");
        var current = Assert.Single(vm.CadPads, pad => pad.Layer == "left-b");
        var project = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        project.CadOutputFwDiffIndexAnchorCadPadByIc.Clear();
        G3BSeedExistingAnchor(project.CadOutputFwDiffIndexAnchorCadPadByIc, current.Id, existing);
        project.CadOutputFwDiffIndexAnchorCadPadId = legacy.Id;
        var visible = vm.CadPads.Where(pad => !invisible || pad.Id != legacy.Id).ToArray();

        FreeformHelperViewModelPrivateAccess.UpdateCadOutputFwDiffIndexing(vm, visible);

        Assert.Equal(invisible ? legacy.Id : (int?)null, project.CadOutputFwDiffIndexAnchorCadPadId);
        var expected = invisible ? new Dictionary<int, int>() : new Dictionary<int, int> { [0] = existing ? current.Id : legacy.Id };
        Assert.Equal(expected, project.CadOutputFwDiffIndexAnchorCadPadByIc);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(2.9, 12, true)]
    [InlineData(0.5, 10, false)]
    [InlineData(0.0, 10, false)]
    [InlineData(1.0, 10, false)]
    public async Task ShiftTruncatesInputAndPreservesOverridesOnZeroOrConflictAsync(double delta, int expected, bool changed)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var left = Assert.Single(vm.CadPads, pad => pad.Layer == "left-a").Id;
        var sibling = Assert.Single(vm.CadPads, pad => pad.Layer == "left-b").Id;
        vm.SetCadOutputFwDiffIndexOverride(left, 10);
        vm.SetCadOutputFwDiffIndexOverride(sibling, 11);
        vm.ApplyCanvasSelection([left], []);
        vm.Step4Index.ShiftDelta = (decimal)delta;
        vm.HasUnsavedChanges = false;

        vm.OffsetSelectedCadOutputFwDiffIndicesCommand.Execute(null);

        Assert.Equal(expected, vm.GetCadOutputFwDiffIndexOverride(left));
        Assert.Equal(11, vm.GetCadOutputFwDiffIndexOverride(sibling));
        Assert.Equal(changed, vm.HasUnsavedChanges);
        Assert.Equal(delta == 1.0, vm.CanOffsetSelectedCadOutputFwDiffIndices);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ShiftSkipsSelectedIdsWithoutAnAssignedIndexAndNoChangeAddsNoUndoAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var left = Assert.Single(vm.CadPads, pad => pad.Layer == "left-a").Id;
        var sibling = Assert.Single(vm.CadPads, pad => pad.Layer == "left-b").Id;
        vm.SetCadOutputFwDiffIndexOverride(left, 10);
        vm.ApplyCanvasSelection([left, sibling], []);
        vm.CadIndexMaps.OutputFwDiffIndexByCadId.Remove(sibling);
        vm.InvalidateWorkflowDataSnapshot();
        var snapshot = FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm);
        Assert.DoesNotContain(sibling, snapshot.CadOutputFwDiffIndexByCadId.Keys);
        vm.Step4Index.ShiftDelta = 2;
        var project = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        project.CadOutputFwDiffIndexOverrides[left] = 12;
        vm.HasUnsavedChanges = false;

        vm.OffsetSelectedCadOutputFwDiffIndicesCommand.Execute(null);

        Assert.Equal("CAD Output FW Diff offset: selected CAD pads already match the requested offset.", vm.StatusText);
        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(sibling));
        Assert.False(vm.HasUnsavedChanges);
        vm.Undo();
        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(left));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ReplacingOverrideAndUndoRestoreBothCapturedEntriesAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var left = Assert.Single(vm.CadPads, pad => pad.Layer == "left-a").Id;
        var sibling = Assert.Single(vm.CadPads, pad => pad.Layer == "left-b").Id;
        vm.SetCadOutputFwDiffIndexOverride(left, 20);
        vm.SetCadOutputFwDiffIndexOverride(sibling, 30);

        vm.SetCadOutputFwDiffIndexOverride(left, 30);

        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(sibling));
        Assert.Equal(30, vm.GetCadOutputFwDiffIndexOverride(left));
        vm.Undo();
        Assert.Equal(20, vm.GetCadOutputFwDiffIndexOverride(left));
        Assert.Equal(30, vm.GetCadOutputFwDiffIndexOverride(sibling));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ClearedAnchorCanBeSelectedAgainAndHiddenOverridesSurviveRebuildAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var anchor = Assert.Single(vm.CadPads, pad => pad.Layer == "left-b");
        var hidden = Assert.Single(vm.CadPads, pad => pad.Layer == "right-a");
        vm.SetCadOutputFwDiffIndexOverride(hidden.Id, 37);
        vm.SetCadOutputFwDiffIndexAnchorCadPad(anchor.Id);

        vm.ClearCadOutputFwDiffIndexAnchorCadPad(anchor.Id);

        Assert.Equal(-1m, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.False(vm.IsCadOutputFwDiffIndexAnchorCadPad(anchor.Id));
        vm.SetCadOutputFwDiffIndexAnchorCadPad(anchor.Id);
        FreeformHelperViewModelPrivateAccess.UpdateCadOutputFwDiffIndexing(vm, [anchor]);
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(anchor.Id));
        Assert.Equal(37, vm.GetCadOutputFwDiffIndexOverride(hidden.Id));
        Assert.DoesNotContain(hidden.Id, vm.CadOutputFwDiffIndexOverrideCadIds);
        Assert.False(vm.TryGetCadOutputFwDiffIndex(hidden.Id, out _));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task HintAndGroupObserversShareAcceptedSnapshotBeforeFinalInvalidationAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await DxfIndexingBoundaryTestWorkspace.OpenAsync(files);
        var keep = Assert.Single(vm.CadPads, pad => pad.Layer == "left-a");
        using var observer = new G3BPublicationObserver(vm);

        FreeformHelperViewModelPrivateAccess.UpdateCadOutputFwDiffIndexing(vm, [keep]);

        Assert.Equal(new[] { nameof(vm.CadOutputFwDiffIndexOverrideCadIds), nameof(vm.Step4DuplicateDiffGroups) }, observer.Names);
        Assert.Same(observer.Snapshots[0], observer.Snapshots[1]);
        Assert.NotSame(observer.Snapshots[1], FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(vm));
        Assert.Equal(keep.Id, Assert.Single(observer.Snapshots[0].CadDisplayIndexByCadId).Key);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CsvRepairPublishesAutoRepairedSourceBeforeHintsAndKeepsItOnRefreshAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await G3BOpenRepairWorkspaceAsync(files);
        var pads = vm.CadPads.ToArray();
        using var observer = new G3BPublicationObserver(vm);

        FreeformHelperViewModelPrivateAccess.UpdateCadOutputFwDiffIndexing(vm, pads);

        var repaired = Assert.Single(vm.CadIndexMaps.LatestAssignmentDecisionsByCadId.Values,
            decision => decision.DecisionSource == CadOutputFwDiffAssignmentDecisionSource.AutoRepaired);
        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(repaired.CadPadId));
        Assert.Same(observer.Snapshots[0], observer.Snapshots[1]);
        Assert.Equal(repaired, observer.Snapshots[0].CadOutputFwDiffAssignmentDecisionsByCadId[repaired.CadPadId]);
        FreeformHelperViewModelPrivateAccess.UpdateCadOutputFwDiffIndexing(vm, pads);
        var refreshed = vm.CadIndexMaps.LatestAssignmentDecisionsByCadId[repaired.CadPadId];
        Assert.Equal(repaired.CurrentPrimaryDiffIndex, refreshed.CurrentPrimaryDiffIndex);
        Assert.Equal(repaired.DecisionSource, refreshed.DecisionSource);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ReportCallbacksLocateAndEditTheCurrentAttachedProjectAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await G3BOpenRepairWorkspaceAsync(files, maskSecondPad: true);
        var cad = Assert.Single(vm.CadPads, pad => pad.Layer == "repair");
        vm.SetCadOutputFwDiffIndexOverride(cad.Id, 37);
        IndexMappingReportViewModel? opened = null;
        vm.OpenIndexMappingReportAsync = report => { opened = report; return Task.CompletedTask; };

        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.NotNull(opened);
        opened.SetDecisionFilterModeCommand.Execute(IndexMappingDecisionFilterMode.ChangedByMask);
        var row = Assert.Single(opened.DecisionRows, item => item.CanApplyOverride && item.CadPadId == cad.Id);
        opened.SelectDecisionCommand.Execute(row);
        opened.ApplyOverrideCommand.Execute(null);
        Assert.Equal(row.ApplyRegularPadIndex, FreeformHelperViewModelPrivateAccess.ProjectFile(vm).DxfRegularMappingOverrides[cad.Id]);
        Assert.Equal(cad.Id, Assert.Single(vm.SelectedCadPadIds));
        opened.ClearOverrideCommand.Execute(null);
        Assert.False(FreeformHelperViewModelPrivateAccess.ProjectFile(vm).DxfRegularMappingOverrides.ContainsKey(cad.Id));
        using var loadGate = new SavePathGate();
        vm.PickLoadProjectPathAsync = () => loadGate.PickAsync("project", "");
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.False(vm.IsProjectEditingEnabled);
            Assert.False(opened.ApplyOverrideCommand.CanExecute(null));
        }
        finally
        {
            loadGate.Release();
            await load;
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task AnalyzeMaskOnlyIssuesKeepsFlagWithoutSampleIssuesAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("left", 0, 0, 20, 20), ("right", 20, 0, 40, 20));
        await vm.MatchCommand.ExecuteAsync(null);
        await G3BImportMaskAsync(vm, "10,0");

        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.Empty(vm.DxfRegularMappingIssues);
        Assert.True(vm.HasDxfRegularMappingIssues);
        Assert.Contains("maskRemoved=1", vm.DxfRegularMappingSummary, StringComparison.Ordinal);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void MaskOnlyDiagnosticsKeepFlagAndNestedRootNotificationOrder()
    {
        // The assembly's TestAppSettingsIsolation module initializer redirects default settings writes.
        var vm = new FreeformHelperViewModel();
        var previous = vm.MappingDiagnosticsResult;
        var accepted = new MappingDiagnosticsResult("mask only", new ObservableCollection<string>(), true);
        var events = new List<string>();
        vm.PropertyChanging += (sender, args) =>
        {
            Assert.Same(vm, sender);
            Assert.Same(previous, vm.MappingDiagnosticsResult);
            events.Add($"changing:{args.PropertyName}");
        };
        vm.PropertyChanged += (sender, args) =>
        {
            Assert.Same(vm, sender);
            Assert.Same(accepted, vm.MappingDiagnosticsResult);
            events.Add(args.PropertyName!);
        };

        vm.MappingDiagnosticsResult = accepted;
        vm.MappingDiagnosticsResult = accepted;

        Assert.Equal<string>(
            [
                "changing:MappingDiagnosticsResult",
                "DxfRegularMappingSummary",
                "DxfRegularMappingIssues",
                "HasDxfRegularMappingIssues",
                "MappingDiagnosticsResult",
            ],
            events);
        Assert.Empty(vm.DxfRegularMappingIssues);
        Assert.True(vm.HasDxfRegularMappingIssues);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ShiftDeltaNotifiesRootProjectionsBeforeChildChanged()
    {
        var vm = new FreeformHelperViewModel();
        var step4 = vm.Step4Index;
        var events = new List<string>();
        vm.PropertyChanged += (sender, args) => { Assert.Same(vm, sender); events.Add(args.PropertyName!); };
        step4.PropertyChanged += (sender, args) => { Assert.Same(step4, sender); events.Add(args.PropertyName!); };

        step4.ShiftDelta = 2;

        Assert.Equal<string>(["CanOffsetSelectedCadOutputFwDiffIndices", "Step4DiffShiftSelectionSummary", "ShiftDelta"], events);
    }

    private static void G3BSeedExistingAnchor(Dictionary<int, int> anchors, int cadId, bool existing)
    {
        if (existing) anchors[0] = cadId;
    }

    private static async Task<FreeformHelperViewModel> G3BOpenRepairWorkspaceAsync(
        LoadingModeTestScope files, bool maskSecondPad = false)
    {
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("repair", 18, 0, 40, 20), ("seed", 20, 0, 40, 20));
        await vm.MatchCommand.ExecuteAsync(null);
        await G3BImportMaskAsync(vm, maskSecondPad ? "10,0" : "10,10");
        return vm;
    }

    private static async Task G3BImportMaskAsync(FreeformHelperViewModel vm, string values)
    {
        var maskPath = TestFiles.WriteTempCsv($"Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,{values}\n");
        try
        {
            vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(maskPath);
            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
            Assert.True(vm.RegularMask.IsEnabled);
        }
        finally
        {
            File.Delete(maskPath);
        }
    }

    private sealed class G3BPublicationObserver : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;

        public G3BPublicationObserver(FreeformHelperViewModel vm)
        {
            _vm = vm;
            vm.PropertyChanged += OnChanged;
        }

        public List<string> Names { get; } = [];
        public List<FreeformHelper.UI.Services.WorkflowDataSnapshot> Snapshots { get; } = [];

        public void Dispose() => _vm.PropertyChanged -= OnChanged;

        private void OnChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName is not (nameof(_vm.CadOutputFwDiffIndexOverrideCadIds) or nameof(_vm.Step4DuplicateDiffGroups))) return;
            Assert.Same(_vm, sender);
            Names.Add(args.PropertyName);
            Snapshots.Add(FreeformHelperViewModelPrivateAccess.BuildWorkflowDataSnapshot(_vm));
        }
    }
}
