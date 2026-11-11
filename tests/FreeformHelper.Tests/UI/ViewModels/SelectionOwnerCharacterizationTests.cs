// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests;

// TestAppSettingsIsolation redirects the default settings store in its module initializer.
[Collection("HeadlessUiSerial")]
public sealed class SelectionOwnerCharacterizationTests
{
    [Fact]
    public void EmptySelectionKeepsInitialSummaryAndQuickFocusKeepsRootNotificationSender()
    {
        var vm = new FreeformHelperViewModel();
        var initial = vm.SelectionSummaryResult;
        vm.ApplyCanvasSelection([], []);
        Assert.Same(initial, vm.SelectionSummaryResult);
        Assert.Equal("Select regular pads to edit size.", initial.SelectedGridRangeSummary);
        var original = vm.QuickFocusQueryText;
        var events = new List<(object? Sender, string Phase, string? Name, string Text)>();
        vm.PropertyChanging += (sender, args) => events.Add((sender, "changing", args.PropertyName, vm.QuickFocusQueryText));
        vm.PropertyChanged += (sender, args) => events.Add((sender, "changed", args.PropertyName, vm.QuickFocusQueryText));

        vm.QuickFocusQueryText = "CAD 11";
        vm.QuickFocusQueryText = "CAD 11";

        Assert.Equal<(object?, string, string?, string)>(
            [(vm, "changing", nameof(vm.QuickFocusQueryText), original), (vm, "changed", nameof(vm.QuickFocusQueryText), "CAD 11")],
            events);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SelectionNormalizesMembershipAndKeepsCollectionsAndCommands(bool cad, bool regular)
    {
        var vm = new FreeformHelperViewModel();
        var cadSelection = vm.SelectedCadPadIds;
        var regularSelection = vm.SelectedRegularPadIndices;
        var clear = vm.ClearSelectionCommand;
        var focus = vm.QuickFocusCommand;
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        vm.ApplyCanvasSelection(cad ? [9, 3, 9] : [], regular ? [70, 20, 70] : []);

        Assert.Equal<int>(cad ? [3, 9] : [], vm.SelectedCadPadIds);
        Assert.Equal<int>(regular ? [20, 70] : [], vm.SelectedRegularPadIndices);
        Assert.Same(cadSelection, vm.SelectedCadPadIds);
        Assert.Same(regularSelection, vm.SelectedRegularPadIndices);
        var summary = vm.SelectionSummaryResult;
        var timing = vm.GetLastSelectionTimingSnapshot();
        changed.Clear();

        vm.ApplyCanvasSelection(cad ? [3, 9] : [], regular ? [20, 70] : []);

        Assert.Empty(changed);
        Assert.Same(summary, vm.SelectionSummaryResult);
        Assert.Equal(timing, vm.GetLastSelectionTimingSnapshot());
        clear.Execute(null);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Equal("No selection.", vm.SelectionSummaryResult.SelectedGridRangeSummary);
        Assert.Same(clear, vm.ClearSelectionCommand);
        Assert.Same(focus, vm.QuickFocusCommand);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void SelectionPublishesSummaryBeforeManualInputsAndMembershipNotifications()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateSummaryWorkspace();
        var oldRows = vm.ManualRowsRange;
        var oldCols = vm.ManualColsRange;
        var oldWidth = vm.PendingColumnWidth;
        var oldHeight = vm.PendingRowHeight;
        var events = new List<Selection4BObservation>();
        vm.PropertyChanging += (sender, args) =>
        {
            if (args.PropertyName == nameof(vm.SelectionSummaryResult))
            {
                events.Add(Observe(vm, sender, "changing summary"));
            }
        };
        vm.PropertyChanged += (sender, args) =>
        {
            if (IsSelectionObservation(args.PropertyName))
            {
                events.Add(Observe(vm, sender, args.PropertyName!));
            }
        };

        vm.ApplyCanvasSelection([11], [40, 10, 20, 30]);

        Assert.Equal<string>(
            ["changing summary", "CadSelectionText", "RegularSelectionText", "SelectionDetailText",
                "SelectionSummaryResult", "ManualRowsRange", "ManualColsRange", "PendingColumnWidth",
                "PendingRowHeight", "SelectedCadPadIds", "SelectedRegularPadIndices",
                "HasPadInspectorMultiSelection", "PadInspectorSelectionSummaryText", "HasNoPadInspectorSummary"],
            events.Select(static entry => entry.Name));
        Assert.All(events, entry => Assert.Same(vm, entry.Sender));
        Assert.Equal("Regular: 0 pads.", events[0].SummaryText);
        Assert.Equal("Regular: Rows 0-1, Cols 0-1 (4)", events[1].SummaryText);
        Assert.All(events.Take(5), entry =>
            Assert.Equal((oldRows, oldCols, oldWidth, oldHeight), (entry.Rows, entry.Cols, entry.Width, entry.Height)));
        Assert.Equal(("0-1", oldCols), (events[5].Rows, events[5].Cols));
        Assert.Equal((10m, oldHeight), (events[7].Width, events[7].Height));
        Assert.Equal(("0-1", "0-1", 10m, 5m),
            (events[9].Rows, events[9].Cols, events[9].Width, events[9].Height));
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void NonRectangularAndMixedSizesUseOneSummaryWithoutUndo()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateSummaryWorkspace();
        vm.ApplyCanvasSelection([], [40, 10, 20]);

        Assert.False(vm.SelectionSummaryResult.IsRectangular);
        Assert.Equal("Regular: 3 pads (non-rectangular)", vm.RegularSelectionText);
        Assert.Empty(vm.ManualRowsRange);
        Assert.Empty(vm.ManualColsRange);
        Assert.Equal("Selection is not a full rectangle; rows/cols range not applied.", vm.ManualRangeError);
        Assert.Equal((10m, 5m), (vm.PendingColumnWidth, vm.PendingRowHeight));
        Assert.False(vm.CanUndo);

        var pads = new List<RegularPad>
        {
            TestGeometryFactory.CreateRegularPad(0, 0, 40, 0, 0, 10, 5),
            TestGeometryFactory.CreateRegularPad(0, 1, 10, 10, 0, 22, 7),
        };
        FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(1, 2, [0, 10, 22], [0, 7], pads);
        vm.RegularPads = new ObservableCollection<RegularPad>(pads);
        vm.ApplyCanvasSelection([], [40, 10]);

        Assert.True(vm.SelectionSummaryResult.IsRectangular);
        Assert.Equal((0m, 0m), (vm.PendingColumnWidth, vm.PendingRowHeight));
        Assert.True(vm.IsPendingColumnWidthMixed);
        Assert.True(vm.IsPendingRowHeightMixed);
        Assert.Empty(vm.ManualRangeError);
        Assert.Equal(vm.SelectionSummaryResult.ManualRowsRange, vm.ManualRowsRange);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void DirectSummaryAssignmentKeepsRootSenderAndDoesNotSynchronizeManualInputs()
    {
        var vm = new FreeformHelperViewModel();
        var before = vm.SelectionSummaryResult;
        var replacement = before with
        {
            CadSelectionText = "Direct CAD text",
            RegularSelectionText = "Direct regular text",
            SelectionDetailText = "Direct detail",
            ManualRowsRange = "7",
            PendingWidth = 19m,
        };
        var events = new List<(object? Sender, string? Name, object Summary)>();
        vm.PropertyChanging += (sender, args) => events.Add((sender, args.PropertyName, vm.SelectionSummaryResult));
        vm.PropertyChanged += (sender, args) => events.Add((sender, args.PropertyName, vm.SelectionSummaryResult));

        vm.SelectionSummaryResult = replacement;

        Assert.Equal<string?>(["SelectionSummaryResult", "CadSelectionText", "RegularSelectionText",
            "SelectionDetailText", "SelectionSummaryResult", "ManualOverrideHint"], events.Select(static entry => entry.Name));
        Assert.All(events, entry => Assert.Same(vm, entry.Sender));
        Assert.Same(before, events[0].Summary);
        Assert.All(events.Skip(1), entry => Assert.Same(replacement, entry.Summary));
        Assert.Empty(vm.ManualRowsRange);
        Assert.NotEqual(19m, vm.PendingColumnWidth);
        events.Clear();
        vm.SelectionSummaryResult = replacement with { };
        Assert.Empty(events);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CanvasIndexAndLocateIdentityDoNotUseListOffsetsOrDiffIndexesAsync()
    {
        var vm = CreateSummaryWorkspace();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.ApplyCanvasSelection([], [10]);

        Assert.Equal("1", vm.ManualRowsRange);
        Assert.Equal("1", vm.ManualColsRange);
        Assert.Equal(10, Assert.IsType<RegularPadInspectorSnapshot>(vm.PadInspectorUi.Snapshot?.Regular).RegularPadId);
        Assert.Equal((0, 71), vm.GetRegularPadIcDiff(10));

        vm.QuickFocusQueryText = "REG 20";
        vm.QuickFocusCommand.Execute(null);

        Assert.Equal(20, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("0", vm.ManualRowsRange);
        Assert.Equal("0", vm.ManualColsRange);
        Assert.Equal(20, Assert.IsType<RegularPadInspectorSnapshot>(vm.PadInspectorUi.Snapshot?.Regular).RegularPadId);
        vm.ApplyCanvasSelection([], []);
        await work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Null(vm.PadInspectorUi.Snapshot);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CadRegularRefreshAndMixedSelectionRejectPendingInspectorResultsAsync()
    {
        var vm = CreateSummaryWorkspace();
        vm.CadPads.Add(TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, 10, 5));
        var ui = vm.PadInspectorUi;
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.ApplyCanvasSelection([11], []);
        Assert.NotNull(ui.Snapshot?.Cad);
        vm.ApplyCanvasSelection([], [10]);
        Assert.Equal(10, Assert.IsType<RegularPadInspectorSnapshot>(ui.Snapshot?.Regular).RegularPadId);
        vm.RefreshInspectorFromSelection();
        Assert.Equal(10, Assert.IsType<RegularPadInspectorSnapshot>(ui.Snapshot?.Regular).RegularPadId);

        vm.ApplyCanvasSelection([11], [10]);
        await Task.WhenAll(
            work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName),
            work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName))
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Same(ui, vm.PadInspectorUi);
        Assert.True(vm.HasPadInspectorMultiSelection);
        Assert.Null(ui.Snapshot);
        Assert.False(ui.IsDeferredRefreshPending);
        vm.ClearSelectionCommand.Execute(null);
    }

    [Fact]
    public void QuickFocusKeepsCadRegularDiffPrecedenceAndIcFiltering()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        vm.CadPads.Add(TestGeometryFactory.CreateCadPad(72, "signal", 0, 0, 10, 10));
        vm.RegularPads.Add(TestGeometryFactory.CreateRegularPad(0, 0, 72, 0, 0, 10, 10, diffIndex: 73));
        vm.RegularPads.Add(TestGeometryFactory.CreateRegularPad(0, 1, 100, 10, 0, 20, 10, diffIndex: 72, icIndex: 1));
        vm.CadIndexMaps.IcIndexByCadId[72] = 0;
        vm.CadIndexMaps.OutputFwDiffIndexByCadId[72] = 72;
        vm.QuickFocusQueryText = "72";
        vm.QuickFocusCommand.Execute(null);
        Assert.Equal(72, Assert.Single(vm.SelectedCadPadIds));
        Assert.Empty(vm.SelectedRegularPadIndices);
        vm.CadPads.Clear();
        vm.QuickFocusCommand.Execute(null);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(72, Assert.Single(vm.SelectedRegularPadIndices));

        vm.QuickFocusQueryText = "72 ic2";
        vm.QuickFocusCommand.Execute(null);

        Assert.Equal(100, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Auto focus: IC 2 FW diff 72 focused (CAD output=0, REG=1).", vm.StatusText);
        vm.QuickFocusQueryText = "CAD 72";
        vm.QuickFocusCommand.Execute(null);
        Assert.Equal("Quick focus: CAD 72 is not visible.", vm.StatusText);
        Assert.Equal(100, Assert.Single(vm.SelectedRegularPadIndices));
    }

    [Fact]
    public void LegacyMatchIdsRemainDifferentFromLinksAndHighlightDoesNotFocus()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;
        vm.CadPads.Add(TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, 10, 10));
        var regular = TestGeometryFactory.CreateRegularPad(0, 0, 101, 0, 0, 10, 10, diffIndex: 7);
        regular.MatchedCadPadId = 11;
        vm.RegularPads.Add(regular);
        Assert.Equal(101, Assert.Single(vm.GetMatchedRegularPadIds(11)));
        Assert.Equal(11, Assert.Single(vm.GetMatchedCadPadIds(101)));
        Assert.Empty(vm.GetMatchedRegularLinks(11));
        Assert.Empty(vm.GetMatchedCadLinks(101));

        vm.FocusCadMatches(11);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2d, host.LastMinZoom);
        vm.HighlightRegularMatches(101);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        vm.HighlightCadPadGroup([999, 11, 11]);
        Assert.Equal(11, Assert.Single(host.SelectedCadIds));
        Assert.Empty(host.SelectedRegularIds);
        vm.HighlightRegularPadGroup([999, 101, 101]);
        Assert.Equal(101, Assert.Single(host.SelectedRegularIds));
        Assert.Empty(host.SelectedCadIds);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(0, host.FitToContentCount);
        Assert.Equal(0, host.ResetViewCount);
        Assert.Equal(0, host.FocusSelectionCount);
    }

    [Fact]
    public void TimingPublishesOnlyAfterTheSelectionAndKeepsOneAcceptedResult()
    {
        var vm = new FreeformHelperViewModel();
        Assert.Equal(new FreeformHelperViewModel.SelectionTimingSnapshot(0, 0, 0, 0), vm.GetLastSelectionTimingSnapshot());
        vm.ApplyCanvasSelection([1, 2], []);
        var previous = vm.GetLastSelectionTimingSnapshot();
        var observed = new List<FreeformHelperViewModel.SelectionTimingSnapshot>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.SelectedCadPadIds) || args.PropertyName == nameof(vm.SelectedRegularPadIndices))
            {
                observed.Add(vm.GetLastSelectionTimingSnapshot());
            }
        };

        vm.ApplyCanvasSelection([3, 4], []);

        Assert.Equal<FreeformHelperViewModel.SelectionTimingSnapshot>([previous, previous], observed);
        var accepted = vm.GetLastSelectionTimingSnapshot();
        Assert.InRange(accepted.SummaryMs, 0, accepted.TotalMs);
        Assert.InRange(accepted.InspectorMs, 0, accepted.TotalMs);
        Assert.InRange(accepted.NotchPreviewMs, 0, accepted.TotalMs);
        Assert.True(accepted.SummaryMs + accepted.InspectorMs + accepted.NotchPreviewMs <= accepted.TotalMs);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SelectionSizingRebuildAndUndoUseExistingRootApisAsync()
    {
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        var original = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var selected = vm.RegularPads.Where(static pad => pad.Row == 0 && pad.Col < 2).Select(static pad => pad.Index).ToArray();
        vm.ApplyCanvasSelection([], selected);
        vm.ApplySizingToCells([0], [0, 1], 6m, 7m);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(selected, vm.SelectedRegularPadIndices);
        Assert.Equal((6m, 7m), (vm.PendingColumnWidth, vm.PendingRowHeight));
        vm.UndoCommand.Execute(null);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(original, vm.RegularPads.Select(static pad => pad.Bounds));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task PreviewRefreshIsImmediateForOneCadAndReplacementRetainsItsDelayAndCancellationAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        using var dxf = new DxfEditingBehaviorTestScope();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var vm = await CreatePreviewWorkspaceAsync(files, dxf, clock);
        var ids = vm.CadPads.Select(static pad => pad.Id).ToArray();
        vm.ApplyCanvasSelection([ids[0]], []);
        Assert.Equal(ids[0], Assert.Single(vm.NotchCanvasPreviewItems).CadPadId);
        var preview = vm.NotchCanvasPreviewItems;
        vm.ApplyCanvasSelection(ids, []);
        clock.Advance(TimeSpan.FromMilliseconds(119));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Same(preview, vm.NotchCanvasPreviewItems);

        // Cancel the first wait and install the replacement before its continuation releases the old gate ticket.
        vm.ApplyCanvasSelection(ids, [vm.RegularPads[0].Index]);
        await HeadlessUiPump.FlushQueueAsync();
        clock.Advance(TimeSpan.FromMilliseconds(119));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Same(preview, vm.NotchCanvasPreviewItems);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await HeadlessUiPump.WaitUntilAsync(() => vm.NotchCanvasPreviewItems.Count == 2, "Replacement preview did not refresh.");
        Assert.Equal(ids, vm.NotchCanvasPreviewItems.Select(static item => item.CadPadId));

        vm.ApplyCanvasSelection(ids, []);
        vm.ApplyCanvasSelection([], []);
        clock.Advance(TimeSpan.FromMilliseconds(120));
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Empty(vm.NotchCanvasPreviewItems);
        Assert.False(vm.IsNotchCanvasPreviewVisible);
    }

    private static FreeformHelperViewModel CreateSummaryWorkspace()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 2, 10, 5,
            diffIndexSelector: static (row, col) => 70 + row * 2 + col,
            regularPadIdSelector: static (row, col) => row == 0 ? (col == 0 ? 40 : 10) : (col == 0 ? 20 : 30));
        var vm = new FreeformHelperViewModel
        {
            RegularPads = new ObservableCollection<RegularPad>(grid.Pads),
        };
        FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;
        return vm;
    }

    private static bool IsSelectionObservation(string? name) => name is
        "CadSelectionText" or "RegularSelectionText" or "SelectionDetailText" or "SelectionSummaryResult" or
        "ManualRowsRange" or "ManualColsRange" or "PendingColumnWidth" or "PendingRowHeight" or
        "SelectedCadPadIds" or "SelectedRegularPadIndices" or "HasPadInspectorMultiSelection" or
        "PadInspectorSelectionSummaryText" or "HasNoPadInspectorSummary";

    private static Selection4BObservation Observe(FreeformHelperViewModel vm, object? sender, string name) =>
        new(sender, name, vm.RegularSelectionText, vm.ManualRowsRange, vm.ManualColsRange,
            vm.PendingColumnWidth, vm.PendingRowHeight);

    private static async Task<FreeformHelperViewModel> CreatePreviewWorkspaceAsync(
        OperationProgressLifecycleTestScope files, DxfEditingBehaviorTestScope dxf, ManualTimeProvider clock)
    {
        var vm = await files.CreateLoadedAsync();
        vm.NotchPreviewAutoPlayEnabled = false;
        vm.NotchPreviewTimingForTests.TimeProvider = clock;
        vm.EnableToFull = true;
        vm.ShowNotchCanvasPreview = true;
        vm.NotchPreviewVisualizationStep = 1m;
        vm.RecalcBoundsOnLayerFilter = false;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        await dxf.OpenAsync(vm, ("signal", 0, 0, 8, 10), ("signal", 12, 0, 20, 10));
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result, vm.StatusText);
        vm.ApplyCanvasSelection([], []);
        return vm;
    }

    private sealed record Selection4BObservation(
        object? Sender, string Name, string SummaryText, string Rows, string Cols, decimal Width, decimal Height);
}
