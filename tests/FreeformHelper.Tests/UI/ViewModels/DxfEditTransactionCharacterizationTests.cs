// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.DxfEditTraceNames;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditTransactionCharacterizationTests
{
    private static readonly bool[] _falseOnly = [false];
    private static readonly bool[] _falseThenTrue = [false, true];
    private static readonly string[] _alphaBeta = ["alpha", "beta"];

    public enum EditAction
    {
        Hide, RestoreLast, RestoreAll, Combine, ClearCombined, Reset, Move, CreateLayer, Rotate,
        RestoreHiddenRow, RestoreDuplicateRow, RehideDuplicateRow, ClearCombinedRow,
        ClearCombinedSourceRow, RestoreMovedRow, RestoreRotatedRow,
    }


    [AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.DeleteSelectedCadPadsCommand))]
    [InlineData(nameof(FreeformHelperViewModel.RestoreLastDeletedCadPadsCommand))]
    [InlineData(nameof(FreeformHelperViewModel.RestoreAllHiddenCadPadsCommand))]
    [InlineData(nameof(FreeformHelperViewModel.CombineSelectedCadPadsCommand))]
    [InlineData(nameof(FreeformHelperViewModel.ClearCombinedCadPadsCommand))]
    [InlineData(nameof(FreeformHelperViewModel.ResetAllDxfEditsCommand))]
    [InlineData(nameof(FreeformHelperViewModel.OpenHiddenDxfEditChangeListCommand))]
    [InlineData(nameof(FreeformHelperViewModel.OpenDuplicateDxfEditChangeListCommand))]
    [InlineData(nameof(FreeformHelperViewModel.OpenCombinedDxfEditChangeListCommand))]
    [InlineData(nameof(FreeformHelperViewModel.OpenMovedDxfEditChangeListCommand))]
    [InlineData(nameof(FreeformHelperViewModel.OpenRotatedDxfEditChangeListCommand))]
    [InlineData(nameof(FreeformHelperViewModel.MoveSelectedCadPadsToLayerCommand))]
    [InlineData(nameof(FreeformHelperViewModel.CreateLayerAndMoveSelectedCadPadsCommand))]
    [InlineData(nameof(FreeformHelperViewModel.RotateSelectedCadPadsCommand))]
    public async Task EditCommandAvailabilityFollowsLoadSessionWithoutActionEligibilityAsync(string name)
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        await WaitForGridRebuildAsync(vm);
        var command = EditCommands(vm)[name];
        Assert.Empty(vm.CadPads);
        Assert.False(vm.CanRotateSelectedCadPads);
        Assert.False(vm.CanMoveSelectedCadPadsToLayer);
        Assert.True(command.CanExecute(null));
        var changes = new List<bool>();
        command.CanExecuteChanged += (_, _) => changes.Add(command.CanExecute(null));
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PickLoadProjectPathAsync = () => picker.Task.WaitAsync(TestContext.Current.CancellationToken);

        var loading = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.False(vm.IsProjectEditingEnabled);
            Assert.False(command.CanExecute(null));
            Assert.Equal(_falseOnly, changes);
        }
        finally
        {
            picker.TrySetResult(files.PathFor("project.json"));
            await loading;
        }

        Assert.True(vm.IsProjectEditingEnabled);
        Assert.True(command.CanExecute(null));
        Assert.Equal(_falseThenTrue, changes);
        Assert.Same(command, EditCommands(vm)[name]);
    }

    [AvaloniaFact]
    public async Task ProjectLoadKeepsPanelObjectsAndTransientInputsButReplacesLayerChoicesAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        Assert.True(await vm.SaveProjectAsync());
        var commands = EditCommands(vm);
        var scopes = vm.DxfEditRotationScopeOptions;
        var interaction = vm.InteractionState;
        var layers = vm.DxfEditLayerOptions;
        vm.SelectedDxfEditRotationScopeOption = scopes[1];
        vm.DxfEditTargetLayerName = "missing";
        vm.DxfEditRotationLayerName = "missing";
        vm.DxfEditNewLayerName = "unfinished";
        vm.DxfEditRotationDegrees = 37m;
        vm.IsDxfEditAdvancedPanelExpanded = true;

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.All(commands, pair => Assert.Same(pair.Value, EditCommands(vm)[pair.Key]));
        Assert.Same(interaction, vm.InteractionState);
        Assert.Same(scopes, vm.DxfEditRotationScopeOptions);
        Assert.Equal(scopes[1], vm.SelectedDxfEditRotationScopeOption);
        Assert.NotSame(layers, vm.DxfEditLayerOptions);
        Assert.Equal(_alphaBeta, vm.DxfEditLayerOptions);
        Assert.Equal("alpha", vm.DxfEditTargetLayerName);
        Assert.Equal("alpha", vm.DxfEditRotationLayerName);
        Assert.Equal("unfinished", vm.DxfEditNewLayerName);
        Assert.Equal(37m, vm.DxfEditRotationDegrees);
        Assert.True(vm.IsDxfEditAdvancedPanelExpanded);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task OldDialogRestoresMatchingHiddenIdInReplacementProjectAsync()
    {
        using var files = new LoadingModeTestScope();
        using var replacementFiles = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var oldPad = vm.CadPads[0];
        await HideWithoutUndoAsync(vm, oldPad.Id);
        var dialog = await OpenChangesAsync(vm, DxfEditChangeKind.Hidden);
        var oldRow = Assert.Single(dialog.Rows);
        var replacement = await DxfCharacterizationWorkspace.OpenAsync(replacementFiles, false,
            ("replacement", 100, 0, 110, 20), ("other", 130, 0, 140, 20));
        var newPad = Assert.Single(replacement.CadPads, pad => pad.Id == oldPad.Id);
        await HideWithoutUndoAsync(replacement, newPad.Id);
        Assert.True(await replacement.SaveProjectAsync());
        Assert.True(await vm.SaveProjectAsync());
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(replacementFiles.PathFor("project.json"));
        using var duringLoad = LoadingModeTestScope.OnNextChange(vm, nameof(vm.IsProjectEditingEnabled), () =>
        {
            Assert.False(dialog.ApplySelectedEntryCommand.CanExecute(null));
            dialog.ApplySelectedEntryCommand.Execute(null);
            Assert.Same(oldRow, dialog.SelectedEntry);
        });

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.True(duringLoad.WasInvoked);
        Assert.Same(oldRow, dialog.SelectedEntry);
        Assert.True(dialog.ApplySelectedEntryCommand.CanExecute(null));
        Assert.False(vm.CanUndo);
        // KNOWN DEFECT: an old dialog can edit a replacement project when CAD IDs collide.
        dialog.ApplySelectedEntryCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(dialog.Rows);
        AssertPadEqual(newPad, Assert.Single(vm.CadPads, pad => pad.Id == newPad.Id));
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.True(vm.HasUnsavedChanges);
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == newPad.Id);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData(EditAction.Hide)]
    [InlineData(EditAction.RestoreLast)]
    [InlineData(EditAction.RestoreAll)]
    [InlineData(EditAction.Combine)]
    [InlineData(EditAction.ClearCombined)]
    [InlineData(EditAction.Reset)]
    [InlineData(EditAction.Move)]
    [InlineData(EditAction.CreateLayer)]
    [InlineData(EditAction.Rotate)]
    [InlineData(EditAction.RestoreHiddenRow)]
    [InlineData(EditAction.RestoreDuplicateRow)]
    [InlineData(EditAction.RehideDuplicateRow)]
    [InlineData(EditAction.ClearCombinedRow)]
    [InlineData(EditAction.ClearCombinedSourceRow)]
    [InlineData(EditAction.RestoreMovedRow)]
    [InlineData(EditAction.RestoreRotatedRow)]
    public async Task AcceptedActionPublishesItsEffectsInOrderAndAddsOneUndoAsync(EditAction action)
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenTransactionWorkspaceAsync(files);
        var execute = await PrepareActionAsync(vm, action);
        var before = Capture(vm);
        var regular = vm.RegularPads;
        var canvas = new SelectionLayerCanvasProbe();
        vm.CanvasHost = canvas;
        vm.HasUnsavedChanges = false;
        using var trace = new DxfEditTrace(vm, ActionStatusPrefix(action));

        execute();
        await WaitForGridRebuildAsync(vm);

        AssertEffectOrder(trace, action);
        AssertProjectionHandlerValues(trace, before, Capture(vm));
        Assert.Single(trace.Events, item => item.Name == Undo && !item.Changing);
        Assert.True(vm.CanUndo);
        Assert.True(vm.UndoCommand.CanExecute(null));
        Assert.True(vm.HasUnsavedChanges);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Equal(RequiresRebuild(action), !ReferenceEquals(regular, vm.RegularPads));
        Assert.Equal(0, canvas.FitToContentCount);
        Assert.Equal(0, canvas.ResetViewCount);
        trace.Events.Clear();

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        AssertSnapshot(before, vm);
        Assert.False(vm.CanUndo);
        Assert.False(vm.UndoCommand.CanExecute(null));
        Assert.Single(trace.Events, item => item.Name == Undo && !item.Changing);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.NotSame(regular, vm.RegularPads);
        Assert.Equal(0, canvas.FitToContentCount);
    }

    [AvaloniaTheory]
    [InlineData(EditAction.Hide)]
    [InlineData(EditAction.RestoreLast)]
    [InlineData(EditAction.RestoreAll)]
    [InlineData(EditAction.Combine)]
    [InlineData(EditAction.ClearCombined)]
    [InlineData(EditAction.Reset)]
    [InlineData(EditAction.Move)]
    [InlineData(EditAction.CreateLayer)]
    [InlineData(EditAction.Rotate)]
    public async Task RejectedOrNoOpActionKeepsSelectionGridAndUndoIncludingDuplicateOnlyResetAsync(EditAction action)
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenTransactionWorkspaceAsync(files);
        await PrepareNoOpAsync(vm, action);
        var before = Capture(vm);
        var selected = vm.SelectedCadPadIds.ToArray();
        var regular = vm.RegularPads;
        vm.HasUnsavedChanges = false;
        using var trace = new DxfEditTrace(vm);

        ExecuteCommand(vm, action);
        await WaitForGridRebuildAsync(vm);

        AssertSnapshot(before, vm);
        Assert.Equal(selected, vm.SelectedCadPadIds);
        Assert.Same(regular, vm.RegularPads);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        Assert.Empty(trace.Events);
        Assert.Equal(1, vm.DuplicateCadPadCount);
    }

    [AvaloniaTheory]
    [InlineData(DxfEditChangeKind.Hidden)]
    [InlineData(DxfEditChangeKind.Duplicate)]
    [InlineData(DxfEditChangeKind.Combined)]
    [InlineData(DxfEditChangeKind.Moved)]
    [InlineData(DxfEditChangeKind.Rotated)]
    public async Task OpeningChangeListPreservesDocumentSelectionAndUndoAsync(DxfEditChangeKind kind)
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenTransactionWorkspaceAsync(files);
        await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
        var before = Capture(vm);
        var selected = vm.SelectedCadPadIds.ToArray();
        vm.HasUnsavedChanges = false;
        using var trace = new DxfEditTrace(vm);

        var dialog = await OpenChangesAsync(vm, kind);

        Assert.Equal(kind, dialog.SelectedKind);
        AssertSnapshot(before, vm);
        Assert.Equal(selected, vm.SelectedCadPadIds);
        Assert.False(vm.CanUndo);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Empty(trace.Events);
    }

    [AvaloniaTheory]
    [InlineData(EditAction.Move, false)]
    [InlineData(EditAction.Move, true)]
    [InlineData(EditAction.RestoreMovedRow, false)]
    [InlineData(EditAction.RestoreMovedRow, true)]
    public async Task LayerEditRebuildFollowsBoundsPolicyWithoutFittingAsync(EditAction action, bool recalc)
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenTransactionWorkspaceAsync(files);
        var execute = await PrepareActionAsync(vm, action);
        var selection = vm.SelectedCadPadIds.ToArray();
        using (FreeformHelperViewModelPrivateAccess.UndoSuppression(vm).Enter())
        {
            vm.RecalcBoundsOnLayerFilter = recalc;
        }
        await SelectCadPadsAsync(vm, selection);
        var regular = vm.RegularPads;
        var canvas = new SelectionLayerCanvasProbe();
        vm.CanvasHost = canvas;

        execute();
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(recalc, !ReferenceEquals(regular, vm.RegularPads));
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(0, canvas.FitToContentCount);
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.False(vm.CanUndo);
        Assert.NotSame(regular, vm.RegularPads);
        Assert.Equal(0, canvas.FitToContentCount);
    }

    [AvaloniaFact]
    public async Task EditAndDisplayActionsShareUndoAndNestedSuppressionDoesNotRecordEditsAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var originalShowCad = vm.ShowCad;
        var hidden = vm.CadPads[0];
        var rotated = vm.CadPads[1];
        vm.ShowCad = !originalShowCad;
        using (FreeformHelperViewModelPrivateAccess.UndoSuppression(vm).Enter())
        {
            await HideWithoutUndoAsync(vm, hidden.Id);
            vm.ShowRegular = !vm.ShowRegular;
        }
        await SelectCadPadsAsync(vm, [rotated.Id]);
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        AssertPadEqual(rotated, Assert.Single(vm.CadPads));
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(!originalShowCad, vm.ShowCad);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(originalShowCad, vm.ShowCad);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task RestoreLastUndoRehidesPadButLosesItsBatchBeforeRestoringOlderBatchAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var first = vm.CadPads[0];
        var last = vm.CadPads[1];
        await HideWithoutUndoAsync(vm, first.Id);
        await HideWithoutUndoAsync(vm, last.Id);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(last.Id, Assert.Single(vm.CadPads).Id);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        Assert.False(vm.CanUndo);
        // KNOWN DEFECT: restore-last pops its batch before the undo snapshot, so undo cannot restore that batch.
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(first.Id, Assert.Single(vm.CadPads).Id);
        Assert.Equal(1, vm.DeletedCadPadCount);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        Assert.Equal(first.Id, Assert.Single(vm.CadPads).Id);
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task UndoRestoreAllRestoresHiddenBatchesInLastInFirstOutOrderAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenTransactionWorkspaceAsync(files);
        var firstBatch = vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id).ToArray();
        var last = Assert.Single(vm.CadPads, static pad => pad.Layer == "beta").Id;
        using (FreeformHelperViewModelPrivateAccess.UndoSuppression(vm).Enter())
        {
            await SelectCadPadsAsync(vm, firstBatch);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await HideWithoutUndoAsync(vm, last);
        }
        vm.RestoreAllHiddenCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(3, vm.CadPads.Count);

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(vm.CadPads);
        Assert.False(vm.CanUndo);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(last, Assert.Single(vm.CadPads).Id);
        vm.RestoreLastDeletedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(firstBatch, vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id));
        Assert.Equal(0, vm.DeletedCadPadCount);
    }

    [AvaloniaFact]
    public async Task UndoAfterProjectReplacementRestoresPreviousProjectsCadSnapshotAsync()
    {
        using var files = new LoadingModeTestScope();
        using var replacementFiles = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        var original = Capture(vm);
        await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.True(await vm.SaveProjectAsync());
        var replacement = await DxfCharacterizationWorkspace.OpenAsync(replacementFiles, false,
            ("replacement", 100, 0, 110, 20));
        Assert.True(await replacement.SaveProjectAsync());
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(replacementFiles.PathFor("project.json"));
        await vm.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal("replacement", Assert.Single(vm.CadPads).Layer);
        Assert.True(vm.CanUndo);

        // KNOWN DEFECT: project replacement retains old CAD undo closures but keeps the replacement project's baseline.
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        AssertPadsEqual(original.Pads, vm.CadPads);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(1, vm.RelayeredCadPadCount);
        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.False(vm.CanUndo);
        Assert.True(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task CombineHandlersSeeBothMutatedCountsBeforeSelectionAndVisiblePadsChangeAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenTransactionWorkspaceAsync(files);
        await SelectCadPadsAsync(vm, vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id).ToArray());
        var events = new List<(string Name, bool Changing, int Hidden, int Combined, bool HasAny, int Visible, int Selected)>();
        void Record(string? name, bool changing)
        {
            if (name is nameof(vm.CombinedCadPadCount) or Aggregate or nameof(vm.DeletedCadPadCount))
                events.Add((name, changing, vm.DeletedCadPadCount, vm.CombinedCadPadCount,
                    vm.HasAnyCadEdits, vm.CadPads.Count, vm.SelectedCadPadIds.Count));
        }
        vm.PropertyChanging += (_, args) => Record(args.PropertyName, true);
        vm.PropertyChanged += (_, args) => Record(args.PropertyName, false);

        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(new[]
        {
            (nameof(vm.CombinedCadPadCount), true, 2, 1, true, 3, 2),
            (nameof(vm.CombinedCadPadCount), false, 2, 1, true, 3, 2),
            (Aggregate, true, 2, 1, true, 3, 2),
            (Aggregate, false, 2, 1, true, 3, 2),
            (nameof(vm.DeletedCadPadCount), true, 2, 1, true, 3, 2),
            (nameof(vm.DeletedCadPadCount), false, 2, 1, true, 3, 2),
        }, events);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(2, vm.CadPads.Count);
    }

    [AvaloniaFact]
    public async Task UndoCombineRestoresAllocationForTheNextAcceptedCombineAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = await OpenTransactionWorkspaceAsync(files);
        var sources = vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id).ToArray();
        await SelectCadPadsAsync(vm, sources);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        var outputs = vm.CadPads.Where(static pad => pad.Layer == "alpha").ToArray();
        Assert.Single(outputs);
        vm.UndoCommand.Execute(null);
        await SelectCadPadsAsync(vm, sources);

        vm.CombineSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        AssertPadsEqual(outputs, vm.CadPads.Where(static pad => pad.Layer == "alpha"));
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.False(vm.CanUndo);
        Assert.Equal(sources, vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id));
    }

    [AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.DxfEditTargetLayerName))]
    [InlineData(nameof(FreeformHelperViewModel.DxfEditRotationLayerName))]
    [InlineData(nameof(FreeformHelperViewModel.SelectedDxfEditRotationScopeOption))]
    [InlineData(nameof(FreeformHelperViewModel.DxfEditNewLayerName))]
    [InlineData(nameof(FreeformHelperViewModel.DxfEditRotationDegrees))]
    [InlineData(nameof(FreeformHelperViewModel.IsDxfEditAdvancedPanelExpanded))]
    public async Task InputCallbackPublishesNewValuesBeforeFinalChangedAndSameValueIsSilentAsync(string name)
    {
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files);
        await SelectCadPadsAsync(vm, [vm.CadPads[0].Id]);
        var (read, write) = DxfEditInputs.Input(vm, name);
        var oldValue = read();
        var events = new List<(string? Name, bool Changing, object Value, bool CanRotate)>();
        vm.PropertyChanging += (_, args) => events.Add((args.PropertyName, true, read(), vm.CanRotateSelectedCadPads));
        vm.PropertyChanged += (_, args) => events.Add((args.PropertyName, false, read(), vm.CanRotateSelectedCadPads));

        write();

        Assert.Equal((name, true, oldValue, true), events[0]);
        Assert.Equal(DxfEditInputs.InputNotifications(name), events.Select(static item => (item.Name, item.Changing)));
        Assert.All(events.Skip(1), item => Assert.Equal(read(), item.Value));
        Assert.All(events.Skip(1), item => Assert.Equal(name != nameof(vm.DxfEditRotationDegrees), item.CanRotate));
        events.Clear();
        write();
        Assert.Empty(events);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task RealProjectHideUsesOneUndoAndRestoresVisibleGeometryWithoutFittingAsync()
    {
        Assert.SkipWhen(!ExampleData.IsAvailable && !ExampleData.IsRequired, ExampleData.MissingMessage);
        using var project = ExampleProjectFixture.Create(TestPaths.FromRepo("example", "BOE36.35", "project_3635.json"));
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel(project.ProjectPath);
        await vm.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        var before = Capture(vm);
        Assert.NotEmpty(before.Pads);
        Assert.False(vm.CanUndo);
        await SelectCadPadsAsync(vm, [before.Pads[0].Id]);
        var canvas = new SelectionLayerCanvasProbe();
        vm.CanvasHost = canvas;

        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(before.Deleted + 1, vm.DeletedCadPadCount);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == before.Pads[0].Id);
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        AssertSnapshot(before, vm);
        Assert.False(vm.CanUndo);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(0, canvas.FitToContentCount);
    }

    private static Task<FreeformHelperViewModel> OpenTransactionWorkspaceAsync(LoadingModeTestScope files) =>
        DxfCharacterizationWorkspace.OpenAsync(files, false,
            ("alpha", 0, 0, 10, 20), ("alpha", 8, 0, 18, 20), ("beta", 30, 0, 40, 20), ("alpha", 0, 0, 10, 20));

    private static async Task<Action> PrepareActionAsync(FreeformHelperViewModel vm, EditAction action)
    {
        var sourceIds = vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id).ToArray();
        var otherId = Assert.Single(vm.CadPads, static pad => pad.Layer == "beta").Id;
        using (FreeformHelperViewModelPrivateAccess.UndoSuppression(vm).Enter())
        {
            await PreparePriorEditAsync(vm, action, sourceIds, otherId);
        }
        await SelectCadPadsAsync(vm, action switch
        {
            EditAction.Combine => sourceIds,
            EditAction.ClearCombined or EditAction.ClearCombinedRow or EditAction.ClearCombinedSourceRow or EditAction.Reset =>
                vm.CadPads.Where(static pad => pad.Layer == "alpha").Select(static pad => pad.Id).ToArray(),
            EditAction.RestoreLast or EditAction.RestoreAll or EditAction.RestoreHiddenRow => [otherId],
            _ => [sourceIds[0]],
        });
        vm.DxfEditTargetLayerName = "beta";
        vm.DxfEditNewLayerName = "created";
        Assert.False(vm.CanUndo);
        var kind = RowKind(action);
        if (kind is null) return () => ExecuteCommand(vm, action);
        var dialog = await OpenChangesAsync(vm, kind.Value);
        Assert.NotEmpty(dialog.Rows);
        return () => dialog.ApplySelectedEntryCommand.Execute(null);
    }

    private static async Task PreparePriorEditAsync(FreeformHelperViewModel vm, EditAction action, int[] sources, int other)
    {
        switch (action)
        {
            case EditAction.RestoreLast or EditAction.RestoreAll or EditAction.RestoreHiddenRow:
                await HideWithoutUndoAsync(vm, sources[0]);
                break;
            case EditAction.ClearCombined or EditAction.ClearCombinedRow or EditAction.ClearCombinedSourceRow or EditAction.Reset:
                await SelectCadPadsAsync(vm, sources);
                vm.CombineSelectedCadPadsCommand.Execute(null);
                await WaitForGridRebuildAsync(vm);
                if (action == EditAction.Reset) await PrepareResetAsync(vm, other);
                break;
            case EditAction.RestoreMovedRow:
                await SelectCadPadsAsync(vm, [sources[0]]);
                vm.DxfEditTargetLayerName = "beta";
                vm.MoveSelectedCadPadsToLayerCommand.Execute(null);
                break;
            case EditAction.RestoreRotatedRow:
                await SelectCadPadsAsync(vm, [sources[0]]);
                vm.RotateSelectedCadPadsCommand.Execute(null);
                break;
            case EditAction.RehideDuplicateRow:
                var dialog = await OpenChangesAsync(vm, DxfEditChangeKind.Duplicate);
                dialog.ApplySelectedEntryCommand.Execute(null);
                break;
        }
        await WaitForGridRebuildAsync(vm);
    }

    private static async Task PrepareResetAsync(FreeformHelperViewModel vm, int id)
    {
        await SelectCadPadsAsync(vm, [id]);
        vm.DxfEditNewLayerName = "edited";
        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [id]);
        vm.RotateSelectedCadPadsCommand.Execute(null);
        await HideWithoutUndoAsync(vm, id);
    }

    private static async Task PrepareNoOpAsync(FreeformHelperViewModel vm, EditAction action)
    {
        await SelectCadPadsAsync(vm, action == EditAction.Hide ? [] : [vm.CadPads[0].Id]);
        vm.DxfEditTargetLayerName = "alpha";
        vm.DxfEditNewLayerName = "alpha";
        vm.DxfEditRotationDegrees = 0m;
    }

    private static async Task HideWithoutUndoAsync(FreeformHelperViewModel vm, int id)
    {
        using (FreeformHelperViewModelPrivateAccess.UndoSuppression(vm).Enter())
        {
            await SelectCadPadsAsync(vm, [id]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
        }
    }

    private static void ExecuteCommand(FreeformHelperViewModel vm, EditAction action)
    {
        var command = action switch
        {
            EditAction.Hide => vm.DeleteSelectedCadPadsCommand,
            EditAction.RestoreLast => vm.RestoreLastDeletedCadPadsCommand,
            EditAction.RestoreAll => vm.RestoreAllHiddenCadPadsCommand,
            EditAction.Combine => vm.CombineSelectedCadPadsCommand,
            EditAction.ClearCombined => vm.ClearCombinedCadPadsCommand,
            EditAction.Reset => vm.ResetAllDxfEditsCommand,
            EditAction.Move => vm.MoveSelectedCadPadsToLayerCommand,
            EditAction.CreateLayer => vm.CreateLayerAndMoveSelectedCadPadsCommand,
            EditAction.Rotate => vm.RotateSelectedCadPadsCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        command.Execute(null);
    }

    private static DxfEditChangeKind? RowKind(EditAction action) => action switch
    {
        EditAction.RestoreHiddenRow or EditAction.ClearCombinedSourceRow => DxfEditChangeKind.Hidden,
        EditAction.RestoreDuplicateRow or EditAction.RehideDuplicateRow => DxfEditChangeKind.Duplicate,
        EditAction.ClearCombinedRow => DxfEditChangeKind.Combined,
        EditAction.RestoreMovedRow => DxfEditChangeKind.Moved,
        EditAction.RestoreRotatedRow => DxfEditChangeKind.Rotated,
        _ => null,
    };

    private static async Task<DxfEditChangeListViewModel> OpenChangesAsync(FreeformHelperViewModel vm, DxfEditChangeKind kind)
    {
        DxfEditChangeListViewModel? opened = null;
        vm.OpenDxfEditChangeListAsync = dialog => { opened = dialog; return Task.CompletedTask; };
        var command = kind switch
        {
            DxfEditChangeKind.Hidden => vm.OpenHiddenDxfEditChangeListCommand,
            DxfEditChangeKind.Duplicate => vm.OpenDuplicateDxfEditChangeListCommand,
            DxfEditChangeKind.Combined => vm.OpenCombinedDxfEditChangeListCommand,
            DxfEditChangeKind.Moved => vm.OpenMovedDxfEditChangeListCommand,
            DxfEditChangeKind.Rotated => vm.OpenRotatedDxfEditChangeListCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        await command.ExecuteAsync(null);
        return Assert.IsType<DxfEditChangeListViewModel>(opened);
    }

    private static Dictionary<string, IRelayCommand> EditCommands(FreeformHelperViewModel vm) => new()
    {
        [nameof(vm.DeleteSelectedCadPadsCommand)] = vm.DeleteSelectedCadPadsCommand,
        [nameof(vm.RestoreLastDeletedCadPadsCommand)] = vm.RestoreLastDeletedCadPadsCommand,
        [nameof(vm.RestoreAllHiddenCadPadsCommand)] = vm.RestoreAllHiddenCadPadsCommand,
        [nameof(vm.CombineSelectedCadPadsCommand)] = vm.CombineSelectedCadPadsCommand,
        [nameof(vm.ClearCombinedCadPadsCommand)] = vm.ClearCombinedCadPadsCommand,
        [nameof(vm.ResetAllDxfEditsCommand)] = vm.ResetAllDxfEditsCommand,
        [nameof(vm.OpenHiddenDxfEditChangeListCommand)] = vm.OpenHiddenDxfEditChangeListCommand,
        [nameof(vm.OpenDuplicateDxfEditChangeListCommand)] = vm.OpenDuplicateDxfEditChangeListCommand,
        [nameof(vm.OpenCombinedDxfEditChangeListCommand)] = vm.OpenCombinedDxfEditChangeListCommand,
        [nameof(vm.OpenMovedDxfEditChangeListCommand)] = vm.OpenMovedDxfEditChangeListCommand,
        [nameof(vm.OpenRotatedDxfEditChangeListCommand)] = vm.OpenRotatedDxfEditChangeListCommand,
        [nameof(vm.MoveSelectedCadPadsToLayerCommand)] = vm.MoveSelectedCadPadsToLayerCommand,
        [nameof(vm.CreateLayerAndMoveSelectedCadPadsCommand)] = vm.CreateLayerAndMoveSelectedCadPadsCommand,
        [nameof(vm.RotateSelectedCadPadsCommand)] = vm.RotateSelectedCadPadsCommand,
    };

    private static bool RequiresRebuild(EditAction action) =>
        action is not (EditAction.Move or EditAction.CreateLayer or EditAction.RestoreMovedRow);

    private static void AssertEffectOrder(DxfEditTrace trace, EditAction action)
    {
        string[] expected = action switch
        {
            EditAction.Hide or EditAction.RestoreLast or EditAction.RestoreAll or EditAction.RestoreHiddenRow =>
                [Hidden, Aggregate, Selection, Pads, Status, Dirty, Undo],
            EditAction.Combine => [Moved, Combined, Aggregate, Hidden, Selection, Pads, Status, Dirty, Undo],
            EditAction.ClearCombined or EditAction.ClearCombinedRow or EditAction.ClearCombinedSourceRow =>
                [Moved, Rotated, Combined, Hidden, Aggregate, Selection, Pads, Status, Dirty, Undo],
            // Reset delegates selection clearing to the rebuild, after the counter/layer phases.
            EditAction.Reset => [Moved, Rotated, Hidden, Combined, Aggregate, Duplicate, Pads, Status, Dirty, Undo],
            EditAction.Move or EditAction.CreateLayer or EditAction.RestoreMovedRow =>
                [Pads, Hidden, Moved, Aggregate, Selection, Status, Dirty, Undo],
            EditAction.Rotate or EditAction.RestoreRotatedRow => [Moved, Rotated, Aggregate, Selection, Pads, Hidden, Status, Dirty, Undo],
            EditAction.RestoreDuplicateRow or EditAction.RehideDuplicateRow => [Duplicate, Selection, Pads, Hidden, Status, Dirty, Undo],
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        Assert.Equal(expected, trace.Events.Where(item => !item.Changing && expected.Contains(item.Name, StringComparer.Ordinal))
            .Select(static item => item.Name).Distinct(StringComparer.Ordinal));
    }

    private static string ActionStatusPrefix(EditAction action) => action switch
    {
        EditAction.Hide => "DXF edit: hid ",
        EditAction.RestoreLast or EditAction.RestoreAll or EditAction.RestoreHiddenRow or
            EditAction.RestoreMovedRow or EditAction.RestoreRotatedRow => "DXF edit: restored ",
        EditAction.Combine => "DXF combine: merged ",
        EditAction.ClearCombined or EditAction.ClearCombinedRow or EditAction.ClearCombinedSourceRow => "DXF combine: cleared ",
        EditAction.Reset => "DXF edit: reset hidden ",
        EditAction.Move or EditAction.CreateLayer => "DXF edit: moved ",
        EditAction.Rotate => "DXF rotate: rotated ",
        EditAction.RestoreDuplicateRow => "DXF duplicate: restored ",
        EditAction.RehideDuplicateRow => "DXF duplicate: auto-hidden ",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private sealed record Snapshot(CadPad[] Pads, int Deleted, int Combined, int Moved, int Rotated, int Duplicate);

    private static Snapshot Capture(FreeformHelperViewModel vm) => new(vm.CadPads.OrderBy(static pad => pad.Id).ToArray(),
        vm.DeletedCadPadCount, vm.CombinedCadPadCount, vm.RelayeredCadPadCount, vm.RotatedCadPadCount, vm.DuplicateCadPadCount);

    private static void AssertSnapshot(Snapshot expected, FreeformHelperViewModel vm)
    {
        AssertPadsEqual(expected.Pads, vm.CadPads);
        Assert.Equal((expected.Deleted, expected.Combined, expected.Moved, expected.Rotated, expected.Duplicate),
            (vm.DeletedCadPadCount, vm.CombinedCadPadCount, vm.RelayeredCadPadCount, vm.RotatedCadPadCount, vm.DuplicateCadPadCount));
    }

    private static void AssertPadsEqual(IEnumerable<CadPad> expected, IEnumerable<CadPad> actual)
    {
        var left = expected.OrderBy(static pad => pad.Id).ToArray();
        var right = actual.OrderBy(static pad => pad.Id).ToArray();
        Assert.Equal(left.Length, right.Length);
        Assert.All(left.Zip(right), pair => AssertPadEqual(pair.First, pair.Second));
    }

    private static void AssertPadEqual(CadPad expected, CadPad actual)
    {
        Assert.Equal((expected.Id, expected.Name, expected.Layer), (actual.Id, actual.Name, actual.Layer));
        Assert.Equal(expected.Polygon.Vertices.ToArray(), actual.Polygon.Vertices.ToArray());
    }

    private static void AssertProjectionHandlerValues(DxfEditTrace trace, Snapshot before, Snapshot after)
    {
        DxfEditTraceAsserts.AssertCountEvents(trace, nameof(FreeformHelperViewModel.DeletedCadPadCount), before.Deleted, after.Deleted, stored: false);
        DxfEditTraceAsserts.AssertCountEvents(trace, nameof(FreeformHelperViewModel.CombinedCadPadCount), before.Combined, after.Combined, stored: false);
        DxfEditTraceAsserts.AssertCountEvents(trace, nameof(FreeformHelperViewModel.RelayeredCadPadCount), before.Moved, after.Moved, stored: true);
        DxfEditTraceAsserts.AssertCountEvents(trace, nameof(FreeformHelperViewModel.RotatedCadPadCount), before.Rotated, after.Rotated, stored: true);
        DxfEditTraceAsserts.AssertFlagEvents(trace, nameof(FreeformHelperViewModel.HasDeletedCadPads), before.Deleted > 0, after.Deleted > 0);
        DxfEditTraceAsserts.AssertFlagEvents(trace, nameof(FreeformHelperViewModel.HasCombinedCadPads), before.Combined > 0, after.Combined > 0);
        DxfEditTraceAsserts.AssertFlagEvents(trace, nameof(FreeformHelperViewModel.HasRelayeredCadPads), before.Moved > 0, after.Moved > 0);
        DxfEditTraceAsserts.AssertFlagEvents(trace, nameof(FreeformHelperViewModel.HasRotatedCadPads), before.Rotated > 0, after.Rotated > 0);
        DxfEditTraceAsserts.AssertFlagEvents(trace, Aggregate, HasEdits(before), HasEdits(after));
    }

    private static bool HasEdits(Snapshot state) => state.Deleted > 0 || state.Combined > 0 || state.Moved > 0 || state.Rotated > 0;
}
