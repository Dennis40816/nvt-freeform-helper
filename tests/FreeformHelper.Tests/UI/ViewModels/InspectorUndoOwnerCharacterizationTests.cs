// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

// TestAppSettingsIsolation initializes the default store before any VM is constructed.
[Collection("HeadlessUiSerial")]
public sealed class InspectorUndoOwnerCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CadRegularAndClearKeepRootNotificationOrderAndEventValuesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateInspectorWorkspace();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var ui = vm.PadInspectorUi;
        var events = RecordInspectorChanges(vm);

        vm.ApplyCanvasSelection([11], []);
        var cad = vm.PadInspectorDisplay;
        var cadEvents = events.ToArray();
        events.Clear();
        vm.ApplyCanvasSelection([], [101]);
        var regular = vm.PadInspectorDisplay;
        var regularEvents = events.ToArray();
        events.Clear();
        vm.ApplyCanvasSelection([], []);
        var empty = vm.PadInspectorDisplay;
        await Task.WhenAll(
            work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName),
            work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(ExpectedSelectionChanges(cad, hadSummary: false, hasSummary: true), cadEvents);
        Assert.Equal(ExpectedSelectionChanges(regular, hadSummary: true, hasSummary: true), regularEvents);
        Assert.Equal(ExpectedSelectionChanges(empty, hadSummary: true, hasSummary: false), events);
        Assert.Equal("CAD 11 · Diff -", cad.CompactHeaderText);
        Assert.Equal("REG 101 · Diff 7", regular.CompactHeaderText);
        Assert.False(empty.HasSummary);
        Assert.Same(ui, vm.PadInspectorUi);
        Assert.Null(ui.Snapshot);
        Assert.False(ui.IsDeferredRefreshPending);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void SamePadPublicationKeepsDebugAndResetsRawTraceBeforeSnapshotChanged(bool cad)
    {
        var vm = CreateInspectorWorkspace();
        var ui = vm.PadInspectorUi;
        ui.Snapshot = BuildSnapshot(vm, cad);
        var previousDisplay = vm.PadInspectorDisplay;
        ui.IsDebugDetailsExpanded = true;
        vm.IsPadInspectorRawTraceExpanded = true;
        var events = new List<(object? Sender, string? Name, bool Debug, bool Raw)>();
        vm.PropertyChanged += (sender, args) =>
            events.Add((sender, args.PropertyName, ui.IsDebugDetailsExpanded, vm.IsPadInspectorRawTraceExpanded));
        ui.PropertyChanged += (sender, args) =>
            events.Add((sender, args.PropertyName, ui.IsDebugDetailsExpanded, vm.IsPadInspectorRawTraceExpanded));

        FreeformHelperViewModelPrivateAccess.BumpNotchExportGridFingerprint(vm);
        var popoverSnapshot = BuildSnapshot(vm, cad);
        ui.Snapshot = popoverSnapshot;

        Assert.Same(ui, vm.PadInspectorUi);
        Assert.Same(popoverSnapshot, ui.Snapshot);
        Assert.True(ui.IsDebugDetailsExpanded);
        Assert.False(vm.IsPadInspectorRawTraceExpanded);
        Assert.Equal(new (object?, string?, bool, bool)[]
        {
            (vm, nameof(vm.PadInspectorCompensationLines), true, true),
            (vm, nameof(vm.PadInspectorRuleTraceLines), true, true),
            (vm, nameof(vm.PadInspectorRuleTraceSections), true, true),
            (vm, nameof(vm.PadInspectorDisplay), true, true),
            (vm, nameof(vm.IsPadInspectorRawTraceExpanded), true, false),
            (ui, nameof(ui.Snapshot), true, false),
        }, events);
        Assert.Equal(previousDisplay.RuleTraceLines, vm.PadInspectorRuleTraceLines);
        Assert.Equal(previousDisplay.CompensationLines, vm.PadInspectorCompensationLines);
        Assert.Equal(cad ? "CAD 11 · Diff -" : "REG 101 · Diff 7", vm.PadInspectorCompactHeaderText);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void DifferentPadResetsDebugBeforeDisplayPublication()
    {
        var vm = CreateInspectorWorkspace();
        var ui = vm.PadInspectorUi;
        ui.Snapshot = BuildSnapshot(vm, cad: true);
        ui.IsDebugDetailsExpanded = true;
        var events = new List<(object? Sender, string? Name, string Header)>();
        ui.PropertyChanged += (sender, args) => events.Add((sender, args.PropertyName, vm.PadInspectorCompactHeaderText));
        vm.PropertyChanged += (sender, args) => events.Add((sender, args.PropertyName, vm.PadInspectorCompactHeaderText));

        ui.Snapshot = BuildSnapshot(vm, cad: false);

        Assert.Equal((ui, nameof(ui.IsDebugDetailsExpanded), "CAD 11 · Diff -"), events[0]);
        Assert.Equal((ui, nameof(ui.Snapshot), "REG 101 · Diff 7"), events[^1]);
        Assert.False(ui.IsDebugDetailsExpanded);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void DirectDisplayAssignmentKeepsSnapshotAndFoldsAndUsesRootChangingThenChanged()
    {
        var vm = CreateInspectorWorkspace();
        vm.PadInspectorUi.Snapshot = BuildSnapshot(vm, cad: true);
        vm.PadInspectorUi.IsDebugDetailsExpanded = true;
        vm.IsPadInspectorRawTraceExpanded = true;
        var snapshot = vm.PadInspectorUi.Snapshot;
        var before = vm.PadInspectorDisplay;
        var replacement = before with { PrimaryText = "Direct display" };
        var events = new List<(object? Sender, string Phase, string? Name, PadInspectorDisplay Display)>();
        vm.PropertyChanging += (sender, args) => events.Add((sender, "changing", args.PropertyName, vm.PadInspectorDisplay));
        vm.PropertyChanged += (sender, args) => events.Add((sender, "changed", args.PropertyName, vm.PadInspectorDisplay));

        vm.PadInspectorDisplay = replacement;
        vm.PadInspectorDisplay = replacement;

        Assert.Equal(new (object?, string, string?, PadInspectorDisplay)[]
        {
            (vm, "changing", nameof(vm.PadInspectorDisplay), before),
            (vm, "changed", nameof(vm.PadInspectorPrimaryText), replacement),
            (vm, "changed", nameof(vm.PadInspectorDisplay), replacement),
        }, events);
        Assert.Same(snapshot, vm.PadInspectorUi.Snapshot);
        Assert.True(vm.PadInspectorUi.IsDebugDetailsExpanded);
        Assert.True(vm.IsPadInspectorRawTraceExpanded);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void MatchCacheReusesAcceptedDetailsUntilWorkflowInvalidation(bool cad)
    {
        var vm = CreateInspectorWorkspace();
        var first = BuildSnapshot(vm, cad);
        var cached = BuildSnapshot(vm, cad);

        FreeformHelperViewModelPrivateAccess.BumpNotchExportGridFingerprint(vm);
        var refreshed = BuildSnapshot(vm, cad);

        Assert.Same(MatchDetails(first, cad), MatchDetails(cached, cad));
        Assert.NotSame(MatchDetails(first, cad), MatchDetails(refreshed, cad));
        Assert.Equal(first.Kind, refreshed.Kind);
        Assert.Equal(first.RuleTrace, refreshed.RuleTrace);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void MatchCacheKeepsCapacityHitsAndClearsOnlyTheOverflowingKind(bool cad)
    {
        var vm = CreateInspectorWorkspace();
        var otherKind = BuildSnapshot(vm, !cad);
        var first = BuildSnapshot(vm, cad);
        FillInspectorCache(vm, cad, 4095);
        var atCapacity = BuildSnapshot(vm, cad);

        AddAndInspectPad(vm, cad, 9000);
        var afterOverflow = BuildSnapshot(vm, cad);
        var otherKindAfterOverflow = BuildSnapshot(vm, !cad);

        Assert.Same(MatchDetails(first, cad), MatchDetails(atCapacity, cad));
        Assert.NotSame(MatchDetails(first, cad), MatchDetails(afterOverflow, cad));
        Assert.Same(MatchDetails(otherKind, !cad), MatchDetails(otherKindAfterOverflow, !cad));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CadCompletionLeavesAnOverlappingRegularSlotPendingAsync()
    {
        var vm = CreateInspectorWorkspace();
        var work = new PadInspectorWorkQueue();
        vm.BackgroundWork.Runner = work;
        vm.PadInspectorUi.Snapshot = BuildSnapshot(vm, cad: false);
        var pending = new List<bool>();
        vm.PadInspectorUi.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PadInspectorUiState.IsDeferredRefreshPending))
            {
                pending.Add(vm.PadInspectorUi.IsDeferredRefreshPending);
            }
        };

        vm.QueueDeferredCadInspectorSnapshotRefresh(11);
        work.HoldNewWork = true;
        vm.QueueDeferredRegularInspectorSnapshotRefresh(101);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));
        var pendingAfterCad = vm.PadInspectorUi.IsDeferredRefreshPending;
        work.StartQueued();
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(pendingAfterCad);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
        Assert.Equal<bool>([true, false], pending);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OldCompletionCannotReleaseTheReplacementSlotAsync(bool cad)
    {
        var vm = CreateInspectorWorkspace();
        var work = new PadInspectorWorkQueue();
        vm.BackgroundWork.Runner = work;
        vm.PadInspectorUi.Snapshot = BuildSnapshot(vm, cad);
        QueueRefresh(vm, cad);
        var original = work.LastStartedAsync(WorkName(cad));
        work.HoldNewWork = true;

        QueueRefresh(vm, cad);
        await original.WaitAsync(TimeSpan.FromSeconds(10));
        var pendingAfterOldCompletion = vm.PadInspectorUi.IsDeferredRefreshPending;
        work.StartQueued();
        await work.LastStartedAsync(WorkName(cad)).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(pendingAfterOldCompletion);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublicRefreshRejectsAnOldSelectionRevisionAsync(bool cad)
    {
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var cadPad = Assert.Single(vm.CadPads);
        var regular = Assert.Single(vm.RegularPads);
        vm.ApplyCanvasSelection(cad ? [cadPad.Id] : [], cad ? [] : [regular.Index]);
        var oldWork = work.LastStartedAsync(WorkName(cad));
        var snapshot = vm.PadInspectorUi.Snapshot;

        QueueRefresh(vm, cad, cad ? cadPad.Id : regular.RegularPadId, selectionRevision: 0);
        await Task.WhenAll(oldWork, work.LastStartedAsync(WorkName(cad))).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(snapshot, vm.PadInspectorUi.Snapshot);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task PublicRefreshRejectsACadRemovedFromTheCurrentSourceAsync()
    {
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var cad = Assert.Single(vm.CadPads);
        vm.ApplyCanvasSelection([cad.Id], []);
        var snapshot = vm.PadInspectorUi.Snapshot;

        vm.CadPads.Remove(cad);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(snapshot, vm.PadInspectorUi.Snapshot);
        Assert.Equal(new[] { cad.Id }, vm.SelectedCadPadIds);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task MissingGridRejectsCadButStillAcceptsRegularRefreshAsync()
    {
        var vm = CreateInspectorWorkspace();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.PadInspectorUi.Snapshot = BuildSnapshot(vm, cad: true);
        var cad = vm.PadInspectorUi.Snapshot;

        vm.QueueDeferredCadInspectorSnapshotRefresh(11);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));
        var cadAfterRefresh = vm.PadInspectorUi.Snapshot;
        vm.PadInspectorUi.Snapshot = vm.BuildRegularPadInspectorSnapshot(
            101, includeMatchDetails: false, includeRuleTrace: false);
        var regular = vm.PadInspectorUi.Snapshot;
        vm.QueueDeferredRegularInspectorSnapshotRefresh(101);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(cad, cadAfterRefresh);
        Assert.NotSame(regular, vm.PadInspectorUi.Snapshot);
        Assert.Equal(101, vm.PadInspectorUi.Snapshot?.Regular?.RegularPadId);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void UndoPushAndExecutionKeepAvailabilityStatusAndSenderOrder()
    {
        var vm = new FreeformHelperViewModel();
        var command = vm.UndoCommand;
        var original = vm.ShowCad;
        var events = new List<(object? Sender, string? Name, bool Available, bool ShowCad, string Status)>();
        command.CanExecuteChanged += (sender, _) =>
            events.Add((sender, "command", vm.CanUndo, vm.ShowCad, vm.StatusText));
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName is nameof(vm.CanUndo) or nameof(vm.StatusText) or nameof(vm.ShowCad))
            {
                events.Add((sender, args.PropertyName, vm.CanUndo, vm.ShowCad, vm.StatusText));
            }
        };
        var status = vm.StatusText;

        vm.ShowCad = !original;
        var push = events.ToArray();
        events.Clear();
        command.Execute(null);
        var undo = events.ToArray();
        events.Clear();
        vm.Undo();

        Assert.Equal(new (object?, string?, bool, bool, string)[]
        {
            (command, "command", true, original, status),
            (vm, nameof(vm.CanUndo), true, original, status),
            (vm, nameof(vm.ShowCad), true, !original, status),
        }, push);
        Assert.Equal(new (object?, string?, bool, bool, string)[]
        {
            (vm, nameof(vm.ShowCad), false, original, status),
            (vm, nameof(vm.StatusText), false, original, "Undo: Toggle CAD layer"),
            (command, "command", false, original, "Undo: Toggle CAD layer"),
            (vm, nameof(vm.CanUndo), false, original, "Undo: Toggle CAD layer"),
        }, undo);
        Assert.Empty(events);
        Assert.Same(command, vm.UndoCommand);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ThrowingUndoReleasesOnlyItsNestedSuppressionAndSkipsFeedback()
    {
        var vm = new FreeformHelperViewModel();
        var suppression = FreeformHelperViewModelPrivateAccess.UndoSuppression(vm);
        var history = FreeformHelperViewModelPrivateAccess.UndoService(vm);
        history.Push(() => throw new InvalidOperationException("undo failure"), "Failing action");
        var status = vm.StatusText;
        var events = new List<string?>();
        vm.PropertyChanged += (_, args) => events.Add(args.PropertyName);
        vm.UndoCommand.CanExecuteChanged += (_, _) => events.Add("command");

        using (suppression.Enter())
        {
            Assert.Throws<InvalidOperationException>(vm.Undo);
            Assert.True(suppression.IsActive);
            Assert.False(vm.CanUndo);
            Assert.Equal(status, vm.StatusText);
            Assert.Empty(events);
            vm.ShowCad = !vm.ShowCad;
            Assert.False(vm.CanUndo);
        }

        Assert.False(suppression.IsActive);
        vm.ShowCad = !vm.ShowCad;
        Assert.True(vm.CanUndo);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task LoadingProjectPreservesHistoryAndDoesNotRecordSettingsApplicationAsync()
    {
        using var scope = new ProjectLoadCharacterizationScope();
        var saved = await scope.CreateSavedCadAsync("inspector-undo.json");
        saved.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        var vm = scope.CreateViewModel();
        var restored = 0;
        var history = FreeformHelperViewModelPrivateAccess.UndoService(vm);
        history.Push(() => restored++, "Before load");
        var command = vm.UndoCommand;
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(scope.PathFor("inspector-undo.json"));

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        vm.Undo();

        Assert.Single(vm.CadPads);
        Assert.Equal(1, restored);
        Assert.False(vm.CanUndo);
        Assert.Equal("Undo: Before load", vm.StatusText);
        Assert.Same(command, vm.UndoCommand);
    }

    private static FreeformHelperViewModel CreateInspectorWorkspace()
    {
        var vm = new FreeformHelperViewModel { CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect };
        vm.CadPads.Add(TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, 5, 10));
        vm.RegularPads.Add(TestGeometryFactory.CreateRegularPad(0, 0, 101, 0, 0, 10, 10, diffIndex: 7));
        return vm;
    }

    private static PadInspectorSnapshot BuildSnapshot(FreeformHelperViewModel vm, bool cad, int? id = null) =>
        Assert.IsType<PadInspectorSnapshot>(cad
            ? vm.BuildCadPadInspectorSnapshot(id ?? 11, includeExpensiveNotchDetails: false,
                includeNotchRowEligibilityDetails: false)
            : vm.BuildRegularPadInspectorSnapshot(id ?? 101));

    private static object MatchDetails(PadInspectorSnapshot snapshot, bool cad) => cad
        ? Assert.IsType<CadPadInspectorSnapshot>(snapshot.Cad).MatchedRegularDetails
        : Assert.IsType<RegularPadInspectorSnapshot>(snapshot.Regular).MatchedCadDetails;

    private static void FillInspectorCache(FreeformHelperViewModel vm, bool cad, int count)
    {
        for (var index = 0; index < count; index++)
        {
            AddAndInspectPad(vm, cad, 1000 + index);
        }
    }

    private static void AddAndInspectPad(FreeformHelperViewModel vm, bool cad, int id)
    {
        if (cad)
        {
            vm.CadPads.Add(TestGeometryFactory.CreateCadPad(id, "signal", 0, 0, 5, 10));
        }
        else
        {
            vm.RegularPads.Add(TestGeometryFactory.CreateRegularPad(0, id, id, 0, 0, 10, 10));
        }

        _ = BuildSnapshot(vm, cad, id);
    }

    private static string WorkName(bool cad) => cad
        ? FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName
        : FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName;

    private static void QueueRefresh(FreeformHelperViewModel vm, bool cad, int? id = null, long? selectionRevision = null)
    {
        if (cad)
        {
            if (selectionRevision.HasValue)
            {
                vm.QueueDeferredCadInspectorSnapshotRefresh(id ?? 11, selectionRevision.Value);
            }
            else
            {
                vm.QueueDeferredCadInspectorSnapshotRefresh(id ?? 11);
            }
        }
        else if (selectionRevision.HasValue)
        {
            vm.QueueDeferredRegularInspectorSnapshotRefresh(id ?? 101, selectionRevision.Value);
        }
        else
        {
            vm.QueueDeferredRegularInspectorSnapshotRefresh(id ?? 101);
        }
    }

    private static List<(string Name, object? Value)> RecordInspectorChanges(FreeformHelperViewModel vm)
    {
        var changes = new List<(string Name, object? Value)>();
        vm.PropertyChanged += (sender, args) =>
        {
            var name = args.PropertyName;
            if (name is not null && (name.StartsWith("PadInspector", StringComparison.Ordinal)
                || name.StartsWith("HasPadInspector", StringComparison.Ordinal)
                || name.StartsWith("HasNoPadInspector", StringComparison.Ordinal)))
            {
                Assert.Same(vm, sender);
                changes.Add((name, vm.GetType().GetProperty(name)!.GetValue(vm)));
            }
        };
        return changes;
    }

    private static (string Name, object? Value)[] ExpectedSelectionChanges(
        PadInspectorDisplay display, bool hadSummary, bool hasSummary)
    {
        var events = new List<(string Name, object? Value)>
        {
            (nameof(FreeformHelperViewModel.HasPadInspectorMultiSelection), false),
            (nameof(FreeformHelperViewModel.PadInspectorSelectionSummaryText), string.Empty),
            (nameof(FreeformHelperViewModel.HasNoPadInspectorSummary), !hadSummary),
        };
        if (hadSummary != hasSummary)
        {
            events.Add((nameof(FreeformHelperViewModel.HasPadInspectorSummary), hasSummary));
            events.Add((nameof(FreeformHelperViewModel.HasNoPadInspectorSummary), !hasSummary));
        }

        events.AddRange(new (string, object?)[]
        {
            (nameof(FreeformHelperViewModel.PadInspectorCompactHeaderText), display.CompactHeaderText),
            (nameof(FreeformHelperViewModel.PadInspectorPrimaryText), display.PrimaryText),
            (nameof(FreeformHelperViewModel.PadInspectorSecondaryText), display.SecondaryText),
            (nameof(FreeformHelperViewModel.PadInspectorMatchText), display.MatchText),
            (nameof(FreeformHelperViewModel.PadInspectorCompensationLines), display.CompensationLines),
            (nameof(FreeformHelperViewModel.PadInspectorRuleTraceLines), display.RuleTraceLines),
            (nameof(FreeformHelperViewModel.PadInspectorRuleTraceSections), display.RuleTraceSections),
        });
        if (hadSummary != hasSummary)
        {
            events.Add((nameof(FreeformHelperViewModel.HasPadInspectorRuleTrace), hasSummary));
        }

        events.Add((nameof(FreeformHelperViewModel.PadInspectorDisplay), display));
        return events.ToArray();
    }
}
