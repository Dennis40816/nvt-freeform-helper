// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CadDocumentEngineCharacterizationTests
{
    private static readonly int[] _sourceCadIds = [1, 2, 3];
    private static readonly int[] _savedGroupIds = [10, 20];

    [Fact]
    public void ClearGroupsRestartsGroupNumberingButKeepsPadAllocation()
    {
        var state = CreateCombinedState();
        var nextPadId = state.NextPadId;

        state.ClearGroups();

        AssertCombinedEmpty(state);
        Assert.Equal(1, state.NextGroupId);
        Assert.Equal(nextPadId, DxfCombinedGroupRules.AllocatePadId(null, state));
    }

    [Fact]
    public void ClearRestartsBothAllocators()
    {
        var state = CreateCombinedState();

        state.Clear();

        AssertCombinedEmpty(state);
        Assert.Equal(1, state.NextGroupId);
        Assert.Equal(CombinedCadState.FirstPadId, DxfCombinedGroupRules.AllocatePadId(null, state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedCopiesIsolateGroupListsIndexesAndAllocators(bool copyIntoExisting)
    {
        var original = CreateCombinedState();
        var copy = CopyCombinedState(original, copyIntoExisting);
        AssertCombinedStateEqual(original, copy);

        copy.Groups[7].SourceCadIds.Add(3);
        copy.Groups[7].OutputCadIds.Clear();
        copy.GroupIdBySourceCadId.Clear();
        copy.GroupIdByOutputCadId.Clear();
        copy.NextGroupId++;
        copy.NextPadId++;

        AssertCombinedStateEqual(CreateCombinedState(), original);
        original.Groups[7].SourceCadIds.Clear();
        original.Groups.Clear();
        Assert.Equal(_sourceCadIds, copy.Groups[7].SourceCadIds);
        Assert.Empty(copy.Groups[7].OutputCadIds);
        Assert.Equal(9, copy.NextGroupId);
        Assert.Equal(CombinedCadState.FirstPadId + 2, copy.NextPadId);
    }

    [Fact]
    public void CapturedProjectEditsOwnTheirMutableCollections()
    {
        var project = new ProjectFile
        {
            HiddenCadPadIds = [1],
            VisibleDuplicateCadPadIds = [2],
            DxfCadLayerOverrides = new() { [1] = "moved" },
            DxfCadGeometryOverrides = new() { [1] = new() { Vertices = Rectangle(2, 4) } },
            DxfCombinedCadGroups = [CreateGroup(7, [1, 2], CreateOutput(CombinedCadState.FirstPadId))],
        };
        var captured = FreeformHelperViewModel.CapturePersistedDxfEditProjectState(project);

        project.HiddenCadPadIds.Clear();
        project.VisibleDuplicateCadPadIds.Clear();
        project.DxfCadLayerOverrides[1] = "changed";
        project.DxfCadGeometryOverrides[1].Vertices.Clear();
        project.DxfCombinedCadGroups[0].SourceCadIds.Clear();
        project.DxfCombinedCadGroups[0].OutputPads[0].Name = "changed";
        project.DxfCombinedCadGroups[0].OutputPads[0].Vertices.Clear();
        project.DxfCombinedCadGroups.Clear();

        Assert.Equal(1, Assert.Single(captured.HiddenCadPadIds));
        Assert.Equal(2, Assert.Single(captured.VisibleDuplicateCadPadIds));
        Assert.Equal("moved", captured.CadLayerOverrides[1]);
        Assert.Equal(Rectangle(2, 4), captured.CadGeometryOverrides[1].Vertices);
        var group = Assert.Single(captured.CombinedCadGroups);
        AssertGroupEqual(CreateGroup(7, [1, 2], CreateOutput(CombinedCadState.FirstPadId)), group);
    }

    [AvaloniaFact]
    public async Task ReplayAppliesSourceOverridesBeforeAddingCombinedOutputsAsync()
    {
        using var files = new LoadingModeTestScope();
        var (project, baseline) = await CreateReplayProjectAsync(files);
        var output = Assert.Single(project.DxfCombinedCadGroups[0].OutputPads);
        project.DxfCadLayerOverrides[output.Id] = "must not replace output layer";
        project.DxfCadGeometryOverrides[output.Id] = new() { Vertices = Rectangle(1, 1) };

        var vm = await LoadAsync(files, project);

        AssertPadEqual(output.ToCadPad(), Assert.Single(vm.CadPads));
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.Equal(1, vm.RotatedCadPadCount);
        var saved = await SaveAsync(files, vm);
        Assert.Equal(baseline[0].Id, Assert.Single(saved.DxfCadLayerOverrides).Key);
        Assert.Equal(baseline[0].Id, Assert.Single(saved.DxfCadGeometryOverrides).Key);
        await SelectCadPadsAsync(vm, [output.Id]);
        vm.ClearCombinedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var edited = Assert.Single(vm.CadPads, pad => pad.Id == baseline[0].Id);
        Assert.Equal("moved", edited.Layer);
        Assert.Equal(Rectangle(6, 3), edited.Polygon.Vertices);
        AssertPadEqual(baseline[1], Assert.Single(vm.CadPads, pad => pad.Id == baseline[1].Id));
    }

    [AvaloniaFact]
    public async Task ResetAfterReplayRestoresImportedBaselineAndUndoRestoresCurrentCadAsync()
    {
        using var files = new LoadingModeTestScope();
        var (project, baseline) = await CreateReplayProjectAsync(files);
        var vm = await LoadAsync(files, project);
        var edited = Assert.Single(vm.CadPads);
        Assert.False(vm.CanUndo);

        vm.ResetAllDxfEditsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        AssertPadsEqual(baseline, vm.CadPads);
        Assert.False(vm.HasAnyCadEdits);
        var reset = await SaveAsync(files, vm);
        Assert.Empty(reset.DxfCadLayerOverrides);
        Assert.Empty(reset.DxfCadGeometryOverrides);
        Assert.Empty(reset.DxfCombinedCadGroups);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        AssertPadEqual(edited, Assert.Single(vm.CadPads));
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.False(vm.CanUndo);
        var restored = await SaveAsync(files, vm);
        AssertGroupEqual(Assert.Single(project.DxfCombinedCadGroups), Assert.Single(restored.DxfCombinedCadGroups));
        Assert.Equal(project.DxfCadLayerOverrides, restored.DxfCadLayerOverrides);
        Assert.Equal(project.DxfCadGeometryOverrides[baseline[0].Id].Vertices,
            restored.DxfCadGeometryOverrides[baseline[0].Id].Vertices);
    }

    [AvaloniaFact]
    public async Task ReplayOrdersGroupsByIdBeforeResolvingDependentSourcesAsync()
    {
        using var files = new LoadingModeTestScope();
        var (project, baseline) = await CreateReplayProjectAsync(files);
        var first = CreateOutput(CombinedCadState.FirstPadId + 10);
        var last = CreateOutput(first.Id + 1);
        project.DxfCombinedCadGroups =
        [
            CreateGroup(20, [first.Id, baseline[1].Id], last),
            CreateGroup(10, [baseline[0].Id], first),
        ];

        var vm = await LoadAsync(files, project);

        AssertPadEqual(last.ToCadPad(), Assert.Single(vm.CadPads));
        Assert.Equal(2, vm.CombinedCadPadCount);
        var saved = await SaveAsync(files, vm);
        Assert.Equal(_savedGroupIds, saved.DxfCombinedCadGroups.Select(static group => group.GroupId));
        Assert.True(saved.HiddenCadPadIds.SetEquals([baseline[0].Id, baseline[1].Id, first.Id]));
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SkippedGroupsSurviveResaveAlongsideRestoredGroupsAsync(int failure)
    {
        using var files = new LoadingModeTestScope();
        var (project, baseline) = await CreateReplayProjectAsync(files);
        var accepted = CreateGroup(7, [baseline[0].Id], CreateOutput(CombinedCadState.FirstPadId));
        var skipped = CreateSkippedGroup(failure, baseline[1].Id);
        project.DxfCombinedCadGroups = [skipped, accepted];

        var vm = await LoadAsync(files, project);

        Assert.Contains("Combined DXF groups could not be restored: 23", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(1, vm.CombinedCadPadCount);
        Assert.Equal(new[] { baseline[1].Id, accepted.OutputPads[0].Id }, vm.CadPads.Select(static pad => pad.Id).Order());
        var saved = await SaveAsync(files, vm);
        Assert.Equal(2, saved.DxfCombinedCadGroups.Count);
        AssertGroupEqual(skipped, Assert.Single(saved.DxfCombinedCadGroups, static group => group.GroupId == 23));
        var reloaded = await LoadAsync(files, saved);
        AssertPadsEqual(vm.CadPads, reloaded.CadPads);
        var resaved = await SaveAsync(files, reloaded);
        AssertGroupEqual(skipped, Assert.Single(resaved.DxfCombinedCadGroups, static group => group.GroupId == 23));
    }

    [AvaloniaFact]
    public async Task RejectedGroupRetainsAllocatorAdvanceFromRolledBackOutputAsync()
    {
        using var files = new LoadingModeTestScope();
        var (project, baseline) = await CreateReplayProjectAsync(files);
        var rejected = CreateOutput(CombinedCadState.FirstPadId + 40);
        project.DxfCombinedCadGroups =
            [CreateGroup(23, baseline.Select(static pad => pad.Id).ToArray(), rejected, CreateOutput(baseline[0].Id))];
        var vm = await LoadAsync(files, project);
        Assert.Equal(0, vm.CombinedCadPadCount);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == rejected.Id);

        await CombineAsync(vm);

        // KNOWN DEFECT: a rejected group's rolled-back outputs still advance the pad allocator.
        Assert.Equal(rejected.Id + 1, vm.CadPads.Min(static pad => pad.Id));
        var saved = await SaveAsync(files, vm);
        var accepted = Assert.Single(saved.DxfCombinedCadGroups, static group => group.GroupId != 23);
        Assert.Equal(24, accepted.GroupId);
        AssertGroupEqual(project.DxfCombinedCadGroups[0],
            Assert.Single(saved.DxfCombinedCadGroups, static group => group.GroupId == 23));
    }

    [AvaloniaFact]
    public async Task HiddenAndVisibleDuplicateChoicesRemainIndependentAfterLoadAsync()
    {
        using var files = new LoadingModeTestScope();
        var original = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("signal", 0, 0, 10, 20), ("signal", 0, 0, 10, 20), ("signal", 0, 0, 10, 20), ("aux", 30, 0, 40, 20));
        var canonical = Assert.Single(original.CadPads, static pad => pad.Layer == "signal");
        var other = Assert.Single(original.CadPads, static pad => pad.Layer == "aux");
        var duplicates = FreeformHelperViewModelPrivateAccess.CadEdit(original).DuplicateCadPadIdToCanonicalId.Keys.Order().ToArray();
        Assert.Equal(2, duplicates.Length);
        var project = await SaveAsync(files, original);
        project.HiddenCadPadIds = [duplicates[0], other.Id, -1];
        project.VisibleDuplicateCadPadIds = [canonical.Id, duplicates[0], duplicates[1], -1];

        var vm = await LoadAsync(files, project);

        Assert.Equal(new[] { canonical.Id, duplicates[1] }.Order(), vm.CadPads.Select(static pad => pad.Id).Order());
        Assert.Equal(2, vm.DeletedCadPadCount);
        Assert.Equal(2, vm.DuplicateCadPadCount);
        Assert.True(vm.HasDetectedDuplicateCadPads);
        var saved = await SaveAsync(files, vm);
        Assert.True(saved.HiddenCadPadIds.SetEquals([duplicates[0], other.Id]));
        Assert.True(saved.VisibleDuplicateCadPadIds.SetEquals(duplicates));
        vm.RestoreAllHiddenCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(4, vm.CadPads.Count);
        Assert.Equal(2, vm.DuplicateCadPadCount);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(new[] { canonical.Id, duplicates[1] }.Order(), vm.CadPads.Select(static pad => pad.Id).Order());
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task FreshLoadKeepsHiddenEditsWithoutCreatingUndoOrHiddenBatchHistoryAsync()
    {
        using var files = new LoadingModeTestScope();
        var original = await DxfCharacterizationWorkspace.OpenAsync(files);
        var hiddenId = original.CadPads[0].Id;
        await HideAsync(original, hiddenId);
        var project = await SaveAsync(files, original);

        var vm = await LoadAsync(files, project);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.False(vm.CanUndo);
        Assert.False(vm.UndoCommand.CanExecute(null));
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == hiddenId);
        await HideAsync(vm, Assert.Single(vm.CadPads).Id);
        Assert.True(vm.CanUndo);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Single(vm.CadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task LoadingAnotherProjectKeepsOldUndoThatCanRestorePreviousCadAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var previousPads = vm.CadPads.ToArray();
        await HideAsync(vm, previousPads[0].Id);
        var replacement = files.CreateViewModel();
        await replacement.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(replacement);
        await SaveAsync(files, replacement);

        await ReloadAsync(vm, files.PathFor("project.json"));

        AssertPadsEqual(replacement.CadPads, vm.CadPads);
        // KNOWN DEFECT: project load clears hidden batches but retains undo actions from the previous document.
        Assert.True(vm.CanUndo);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        AssertPadsEqual(previousPads, vm.CadPads);
        Assert.False(vm.CanUndo);
        vm.ResetAllDxfEditsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        AssertPadsEqual(replacement.CadPads, vm.CadPads);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearVersusResetUsesDifferentPadAllocationAndRestartsGroupIdsAsync(bool resetAll)
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenOverlappingAsync(files);
        await CombineAsync(vm);
        var first = Assert.Single((await SaveAsync(files, vm)).DxfCombinedCadGroups);
        var firstOutput = Assert.Single(first.OutputPads);

        await ClearCombinedAsync(vm, resetAll);
        await CombineAsync(vm);

        var second = Assert.Single((await SaveAsync(files, vm)).DxfCombinedCadGroups);
        Assert.Equal(first.GroupId, second.GroupId);
        Assert.Equal(firstOutput.Id + (resetAll ? 0 : 1), Assert.Single(second.OutputPads).Id);
        Assert.Equal(first.SourceCadIds, second.SourceCadIds);
    }

    [AvaloniaFact]
    public async Task UndoCombineRestoresAllocatorAndDoesNotShareTheSavedGroupSnapshotAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenOverlappingAsync(files);
        var baseline = vm.CadPads.ToArray();
        await CombineAsync(vm);
        var first = Assert.Single((await SaveAsync(files, vm)).DxfCombinedCadGroups);

        vm.Undo();
        await WaitForGridRebuildAsync(vm);

        AssertPadsEqual(baseline, vm.CadPads);
        Assert.False(vm.CanUndo);
        await CombineAsync(vm);
        var repeated = Assert.Single((await SaveAsync(files, vm)).DxfCombinedCadGroups);
        AssertGroupEqual(first, repeated);
        await ClearCombinedAsync(vm, resetAll: false);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        AssertGroupEqual(first, Assert.Single((await SaveAsync(files, vm)).DxfCombinedCadGroups));
    }

    [AvaloniaFact]
    public async Task UndoResetRestoresAdvancedPadAllocationAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenOverlappingAsync(files);
        await CombineAsync(vm);
        await ClearCombinedAsync(vm, resetAll: false);
        await CombineAsync(vm);
        var beforeReset = Assert.Single(vm.CadPads).Id;
        await ClearCombinedAsync(vm, resetAll: true);

        vm.Undo();
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(beforeReset, Assert.Single(vm.CadPads).Id);
        await ClearCombinedAsync(vm, resetAll: false);
        await CombineAsync(vm);
        Assert.Equal(beforeReset + 1, Assert.Single(vm.CadPads).Id);
    }

    [AvaloniaFact]
    public async Task UndoRestoreAllRestoresHiddenBatchesInLastInFirstOutOrderAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var baseline = vm.CadPads.ToArray();
        await HideAsync(vm, baseline[0].Id);
        await HideAsync(vm, baseline[1].Id);
        vm.RestoreAllHiddenCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        AssertPadsEqual(baseline, vm.CadPads);

        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        AssertPadEqual(baseline[1], Assert.Single(vm.CadPads));
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        AssertPadsEqual(baseline, vm.CadPads);
    }

    [AvaloniaFact]
    public async Task UndoRestoreLastDoesNotPutTheLatestHiddenBatchBackInHistoryAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var baseline = vm.CadPads.ToArray();
        await HideAsync(vm, baseline[0].Id);
        await HideAsync(vm, baseline[1].Id);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        AssertPadEqual(baseline[1], Assert.Single(vm.CadPads));

        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        // KNOWN DEFECT: restore-last pops its batch before capturing undo, so undo loses that batch's restore history.
        AssertPadEqual(baseline[0], Assert.Single(vm.CadPads));
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        AssertPadEqual(baseline[0], Assert.Single(vm.CadPads));
        Assert.Equal(1, vm.DeletedCadPadCount);
    }

    [AvaloniaFact]
    public async Task RejectedAndNoOpEditsLeaveTheAcceptedUndoUntouchedAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var baseline = vm.CadPads.ToArray();
        await HideAsync(vm, baseline[0].Id);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [baseline[1].Id]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        vm.DxfEditRotationDegrees = 360m;
        vm.RotateSelectedCadPadsCommand.Execute(null);
        vm.DxfEditTargetLayerName = baseline[1].Layer;
        vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
        vm.ClearCombinedCadPadsCommand.Execute(null);
        Assert.Equal(1, vm.DeletedCadPadCount);

        vm.Undo();
        await WaitForGridRebuildAsync(vm);

        AssertPadsEqual(baseline, vm.CadPads);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HideAndUndoRebuildWithoutFittingAndClearSelectionAsync(bool recalcBounds)
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, recalcBounds);
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;
        var changes = LoadingModeTestScope.ObserveChanges(vm);
        var bounds = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var id = vm.CadPads[0].Id;
        await SelectCadPadsAsync(vm, [id]);
        host.SetSelection([id], []);

        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Contains(nameof(vm.RegularPads), changes);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(host.SelectedCadIds);
        changes.Clear();
        await SelectCadPadsAsync(vm, [Assert.Single(vm.CadPads).Id]);
        host.SetSelection(vm.SelectedCadPadIds, []);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Contains(nameof(vm.RegularPads), changes);
        Assert.Equal(bounds, vm.RegularPads.Select(static pad => pad.Bounds));
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(host.SelectedCadIds);
        Assert.Equal(0, host.FitToContentCount);
        Assert.Equal(0, host.ResetViewCount);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task ResetWithOnlyDuplicatesLeavesUndoAndVisibilityUntouchedAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("signal", 0, 0, 10, 20), ("signal", 0, 0, 10, 20));
        var visible = Assert.Single(vm.CadPads);
        vm.HasUnsavedChanges = false;

        vm.ResetAllDxfEditsCommand.Execute(null);

        AssertPadEqual(visible, Assert.Single(vm.CadPads));
        Assert.Equal(1, vm.DuplicateCadPadCount);
        Assert.False(vm.HasAnyCadEdits);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task RealProjectHideResaveAndFreshLoadKeepCadAndStartWithoutUndoAsync()
    {
        Assert.SkipWhen(!ExampleData.IsAvailable && !ExampleData.IsRequired, ExampleData.MissingMessage);
        using var fixture = ExampleProjectFixture.Create(TestPaths.FromRepo("example", "BOE36.35", "project_3635.json"));
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        await ReloadAsync(vm, fixture.ProjectPath);
        Assert.NotEmpty(vm.CadPads);
        var hiddenId = vm.CadPads[0].Id;
        await HideAsync(vm, hiddenId);
        var expected = vm.CadPads.ToArray();
        var project = await SaveAsync(files, vm);

        var loaded = await LoadAsync(files, project);

        AssertPadsEqual(expected, loaded.CadPads);
        Assert.Equal(vm.DeletedCadPadCount, loaded.DeletedCadPadCount);
        Assert.Equal(vm.CombinedCadPadCount, loaded.CombinedCadPadCount);
        Assert.Equal(vm.DuplicateCadPadCount, loaded.DuplicateCadPadCount);
        Assert.False(loaded.CanUndo);
        loaded.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(loaded);
        Assert.DoesNotContain(loaded.CadPads, pad => pad.Id == hiddenId);
        Assert.False(loaded.CanUndo);
    }

    private static CombinedCadState CreateCombinedState()
    {
        var state = new CombinedCadState { NextGroupId = 8, NextPadId = CombinedCadState.FirstPadId + 1 };
        state.Groups[7] = new CombinedCadGroupState([1, 2], [CombinedCadState.FirstPadId]);
        state.GroupIdBySourceCadId[1] = 7;
        state.GroupIdBySourceCadId[2] = 7;
        state.GroupIdByOutputCadId[CombinedCadState.FirstPadId] = 7;
        return state;
    }

    private static CombinedCadState CopyCombinedState(CombinedCadState original, bool copyIntoExisting)
    {
        if (!copyIntoExisting)
        {
            return original.Clone();
        }

        var copy = new CombinedCadState { NextGroupId = 99, NextPadId = CombinedCadState.FirstPadId + 99 };
        copy.Groups[90] = new CombinedCadGroupState([9], [10]);
        copy.GroupIdBySourceCadId[9] = 90;
        copy.GroupIdByOutputCadId[10] = 90;
        copy.CopyFrom(original);
        return copy;
    }

    private static void AssertCombinedEmpty(CombinedCadState state)
    {
        Assert.Empty(state.Groups);
        Assert.Empty(state.GroupIdBySourceCadId);
        Assert.Empty(state.GroupIdByOutputCadId);
        Assert.Empty(state.OutputCadIds);
    }

    private static void AssertCombinedStateEqual(CombinedCadState expected, CombinedCadState actual)
    {
        Assert.Equal(expected.NextGroupId, actual.NextGroupId);
        Assert.Equal(expected.NextPadId, actual.NextPadId);
        Assert.Equal(expected.GroupIdBySourceCadId, actual.GroupIdBySourceCadId);
        Assert.Equal(expected.GroupIdByOutputCadId, actual.GroupIdByOutputCadId);
        Assert.Equal(expected.OutputCadIds.Order(), actual.OutputCadIds.Order());
        var group = Assert.Single(actual.Groups);
        Assert.Equal(7, group.Key);
        Assert.Equal(expected.Groups[7].SourceCadIds, group.Value.SourceCadIds);
        Assert.Equal(expected.Groups[7].OutputCadIds, group.Value.OutputCadIds);
    }

    private static async Task<(ProjectFile Project, CadPad[] Baseline)> CreateReplayProjectAsync(LoadingModeTestScope files)
    {
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var baseline = vm.CadPads.OrderBy(static pad => pad.Id).ToArray();
        var project = await SaveAsync(files, vm);
        project.DxfCadLayerOverrides = new() { [baseline[0].Id] = "moved" };
        project.DxfCadGeometryOverrides = new() { [baseline[0].Id] = new() { Vertices = Rectangle(6, 3) } };
        project.DxfCombinedCadGroups =
            [CreateGroup(7, baseline.Select(static pad => pad.Id).ToArray(), CreateOutput(CombinedCadState.FirstPadId))];
        return (project, baseline);
    }

    private static ProjectDxfCombinedCadGroup CreateSkippedGroup(int failure, int sourceId)
    {
        var output = CreateOutput(CombinedCadState.FirstPadId + 5);
        switch (failure)
        {
            case 0:
                return CreateGroup(23, [-1], output);
            case 1:
                output.Id = sourceId;
                break;
            case 2:
                output.Vertices = [new Point2(0, 0), new Point2(1, 0)];
                break;
        }

        return CreateGroup(23, [sourceId], output);
    }

    private static ProjectDxfCombinedCadGroup CreateGroup(int id, int[] sources, params ProjectCadPadSnapshot[] outputs) => new()
    {
        GroupId = id,
        SourceCadIds = sources.ToList(),
        OutputPads = outputs.ToList(),
    };

    private static ProjectCadPadSnapshot CreateOutput(int id) => new()
    {
        Id = id,
        Name = "Saved union",
        Layer = "alpha",
        Vertices = Rectangle(40, 20),
    };

    private static List<Point2> Rectangle(double width, double height) =>
        [new(0, 0), new(width, 0), new(width, height), new(0, height)];

    private static Task<FreeformHelperViewModel> OpenOverlappingAsync(LoadingModeTestScope files) =>
        DxfCharacterizationWorkspace.OpenAsync(files, false, ("signal", 0, 0, 20, 20), ("signal", 10, 0, 30, 20));

    private static async Task CombineAsync(FreeformHelperViewModel vm)
    {
        await SelectCadPadsAsync(vm, vm.CadPads.Select(static pad => pad.Id).ToArray());
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.True(vm.HasCombinedCadPads);
    }

    private static async Task ClearCombinedAsync(FreeformHelperViewModel vm, bool resetAll)
    {
        await SelectCadPadsAsync(vm, vm.CadPads.Select(static pad => pad.Id).ToArray());
        var command = resetAll ? vm.ResetAllDxfEditsCommand : vm.ClearCombinedCadPadsCommand;
        command.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(0, vm.CombinedCadPadCount);
    }

    private static async Task HideAsync(FreeformHelperViewModel vm, int id)
    {
        await SelectCadPadsAsync(vm, [id]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == id);
    }

    private static async Task<ProjectFile> SaveAsync(LoadingModeTestScope files, FreeformHelperViewModel vm)
    {
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        return JsonProjectStore.Load(files.PathFor("project.json"));
    }

    private static async Task<FreeformHelperViewModel> LoadAsync(LoadingModeTestScope files, ProjectFile project)
    {
        var path = files.PathFor("project.json");
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();
        await ReloadAsync(vm, path);
        return vm;
    }

    private static async Task ReloadAsync(FreeformHelperViewModel vm, string path)
    {
        Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded, vm.StatusText);
        await WaitForGridRebuildAsync(vm);
    }

    private static void AssertGroupEqual(ProjectDxfCombinedCadGroup expected, ProjectDxfCombinedCadGroup actual)
    {
        Assert.Equal(expected.GroupId, actual.GroupId);
        Assert.Equal(expected.SourceCadIds, actual.SourceCadIds);
        Assert.Equal(expected.OutputPads.Count, actual.OutputPads.Count);
        Assert.All(expected.OutputPads.Zip(actual.OutputPads), pair =>
        {
            Assert.Equal(pair.First.Id, pair.Second.Id);
            Assert.Equal(pair.First.Name, pair.Second.Name);
            Assert.Equal(pair.First.Layer, pair.Second.Layer);
            Assert.Equal(pair.First.Vertices, pair.Second.Vertices);
        });
    }

    private static void AssertPadsEqual(IEnumerable<CadPad> expected, IEnumerable<CadPad> actual)
    {
        var expectedPads = expected.OrderBy(static pad => pad.Id).ToArray();
        var actualPads = actual.OrderBy(static pad => pad.Id).ToArray();
        Assert.Equal(expectedPads.Length, actualPads.Length);
        Assert.All(expectedPads.Zip(actualPads), pair => AssertPadEqual(pair.First, pair.Second));
    }

    private static void AssertPadEqual(CadPad expected, CadPad actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Layer, actual.Layer);
        Assert.Equal(expected.Polygon.Vertices, actual.Polygon.Vertices);
    }
}
