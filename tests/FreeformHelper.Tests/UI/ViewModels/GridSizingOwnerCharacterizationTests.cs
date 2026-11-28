// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

// TestAppSettingsIsolation initializes the process-wide settings sandbox before any VM is constructed.
[Collection("HeadlessUiSerial")]
public sealed class GridSizingOwnerCharacterizationTests
{
    [Fact]
    public void ManualWidth_SameValueIsSilentAndNestedNotificationsKeepTheRootSender()
    {
        var vm = new FreeformHelperViewModel { IsPendingColumnWidthMixed = true };
        var events = new List<(string Event, decimal Width, bool Mixed)>();
        vm.PropertyChanging += (sender, args) =>
        {
            Assert.Same(vm, sender);
            events.Add(($"changing:{args.PropertyName}", vm.PendingColumnWidth, vm.IsPendingColumnWidthMixed));
        };
        vm.PropertyChanged += (sender, args) =>
        {
            Assert.Same(vm, sender);
            events.Add(($"changed:{args.PropertyName}", vm.PendingColumnWidth, vm.IsPendingColumnWidthMixed));
        };

        vm.PendingColumnWidth = 0m;
        Assert.Empty(events);
        Assert.False(vm.CanUndo);
        vm.PendingColumnWidth = 5m;

        Assert.Equal([
            ("changed:CanUndo", 0m, true),
            ("changing:PendingColumnWidth", 0m, true),
            ("changing:IsPendingColumnWidthMixed", 5m, true),
            ("changed:IsPendingColumnWidthMixed", 5m, false),
            ("changed:PendingColumnWidth", 5m, false),
        ], events);
        events.Clear();
        vm.PendingColumnWidth = 5m;
        vm.PendingRowHeight = 0m;
        vm.IsPendingColumnWidthMixed = false;
        vm.IsPendingRowHeightMixed = false;
        vm.ManualRowsRange = "";
        vm.ManualColsRange = "";
        vm.ManualRangeStatus = "No manual range.";
        vm.ManualRangeError = "";
        Assert.Empty(events);
    }

    [Fact]
    public void ManualRange_ErrorPresenceAndStatusNotifyBeforeTheRangeFinishesChanging()
    {
        var vm = new FreeformHelperViewModel { ManualRangeError = "previous" };
        var events = new List<(string Event, string Rows, string Error)>();
        vm.PropertyChanging += (sender, args) =>
        {
            Assert.Same(vm, sender);
            events.Add(($"changing:{args.PropertyName}", vm.ManualRowsRange, vm.ManualRangeError));
        };
        vm.PropertyChanged += (sender, args) =>
        {
            Assert.Same(vm, sender);
            events.Add(($"changed:{args.PropertyName}", vm.ManualRowsRange, vm.ManualRangeError));
        };

        vm.ManualRowsRange = "1";

        Assert.Equal([
            ("changed:CanUndo", "", "previous"),
            ("changing:ManualRowsRange", "", "previous"),
            ("changing:ManualRangeError", "1", "previous"),
            ("changed:ManualRangeError", "1", ""),
            ("changed:HasManualRangeError", "1", ""),
            ("changing:ManualRangeStatus", "1", ""),
            ("changed:ManualRangeStatus", "1", ""),
            ("changed:ManualRowsRange", "1", ""),
        ], events);
        Assert.Equal("Grid not built.", vm.ManualRangeStatus);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ManualRanges_EmptyIncompleteInvalidSelectionAndSizingKeepTheirDistinctEffectsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        var before = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        vm.HasUnsavedChanges = false;

        vm.ManualRowsRange = "1";
        Assert.Equal("Cols: invalid.", vm.ManualRangeStatus);
        Assert.Empty(vm.ManualRangeError);
        Assert.Empty(vm.SelectedRegularPadIndices);
        vm.ManualColsRange = "999999";
        Assert.NotEmpty(vm.ManualRangeError);
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.False(vm.HasUnsavedChanges);

        vm.ManualColsRange = "0";
        var selected = Assert.Single(vm.SelectedRegularPadIndices);
        Assert.Equal("Manual range: Rows 1, Cols 0.", vm.ManualRangeStatus);
        Assert.Empty(vm.ManualRangeError);
        Assert.Equal(before, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.False(vm.HasUnsavedChanges);
        vm.PendingColumnWidth = 6m;
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(6d, Assert.Single(vm.RegularPads, pad => pad.Index == selected).Bounds.Width, 6);
        Assert.Equal(selected, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.True(vm.HasUnsavedChanges);

        vm.ManualRowsRange = "";
        Assert.Equal(selected, Assert.Single(vm.SelectedRegularPadIndices));
        vm.ManualColsRange = "";
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Empty(vm.ManualRangeError);
        Assert.Equal("No manual range.", vm.ManualRangeStatus);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SelectionAndLoad_SuppressedDimensionsKeepMixedFlagsUntilSummaryPublicationAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        Assert.True(await vm.SaveProjectAsync());
        vm.ApplySizingToCells([0], [0], 6m, 7m);
        await WaitForGridRebuildAsync(vm);
        vm.ApplyCanvasSelection([], vm.RegularPads.Select(static pad => pad.Index).ToArray());
        Assert.True(vm.IsPendingColumnWidthMixed);
        Assert.True(vm.IsPendingRowHeightMixed);
        var changes = new List<(string Name, bool WidthMixed, bool HeightMixed)>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(vm.PendingColumnWidth) or nameof(vm.PendingRowHeight))
            {
                changes.Add((args.PropertyName, vm.IsPendingColumnWidthMixed, vm.IsPendingRowHeightMixed));
            }
        };
        var undoEvents = 0;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.CanUndo))
            {
                undoEvents++;
            }
        };

        var pad = Assert.Single(vm.RegularPads, static pad => pad.Row == 0 && pad.Col == 0);
        vm.ApplyCanvasSelection([], [pad.Index]);

        Assert.Contains((nameof(vm.PendingColumnWidth), true, true), changes);
        Assert.Contains((nameof(vm.PendingRowHeight), true, true), changes);
        Assert.False(vm.IsPendingColumnWidthMixed);
        Assert.False(vm.IsPendingRowHeightMixed);
        Assert.Equal(0, undoEvents);
        vm.IsPendingColumnWidthMixed = true;
        vm.IsPendingRowHeightMixed = true;
        changes.Clear();

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Contains((nameof(vm.PendingColumnWidth), true, true), changes);
        Assert.Contains((nameof(vm.PendingRowHeight), true, true), changes);
        Assert.Equal(0m, vm.PendingColumnWidth);
        Assert.Equal(0m, vm.PendingRowHeight);
        Assert.False(vm.IsPendingColumnWidthMixed);
        Assert.False(vm.IsPendingRowHeightMixed);
        Assert.Equal(0, undoEvents);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RejectedSizing_LeavesGeometryDirtyStateAndUndoAvailabilityUnchangedAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGlobalGridAsync(fixture);
        var before = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var undoBefore = vm.CanUndo;
        var undoEvents = 0;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.CanUndo))
            {
                undoEvents++;
            }
        };
        vm.HasUnsavedChanges = false;

        vm.ApplySizingToCells([0], [0], null, null);
        vm.ApplySizingToCells([0], [0], 6m, 21m);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal("Manual sizing: Manual sizes exceed available total.", vm.StatusText);
        Assert.Equal(before, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.Equal(undoBefore, vm.CanUndo);
        Assert.Equal(0, undoEvents);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SizingAndReset_PreserveSelectionOnceAndUndoRestoresEachSnapshotWithFitAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGlobalGridAsync(fixture);
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;
        var original = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var selected = vm.RegularPads[0].Index;
        host.SetSelection([], [selected]);
        vm.ApplyCanvasSelection([], [selected]);

        vm.ApplySizingToCells([0], [0], 6m, 7m);
        await WaitForGridRebuildAsync(vm);
        var sized = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        Assert.Equal(selected, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(1, host.FitToContentCount);
        Assert.Equal(selected, Assert.Single(host.SelectedRegularIds));
        vm.ResetSizingForCells([0], [0]);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(original, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.Equal(selected, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(2, host.FitToContentCount);
        Assert.Equal(selected, Assert.Single(host.SelectedRegularIds));

        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(sized, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Equal(3, host.FitToContentCount);
        Assert.Empty(host.SelectedRegularIds);
        vm.UndoCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(original, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.Equal(4, host.FitToContentCount);
        vm.CanvasHost = null;
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CadCustomValues_UndoRestoresPreviousValuesAndMissingEntriesAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = fixture.CreateViewModel();
        vm.SetCadPadCustomValues([11], 1.25m);
        vm.SetCadPadCustomValues([11, 22], 7.5m);
        Assert.Equal(7.5d, vm.GetCadPadCustomValue(11));
        Assert.Equal(7.5d, vm.GetCadPadCustomValue(22));

        vm.UndoCommand.Execute(null);

        Assert.Equal(1.25d, vm.GetCadPadCustomValue(11));
        Assert.Equal(0d, vm.GetCadPadCustomValue(22));
        Assert.True(await vm.SaveProjectAsync());
        var path = await vm.PickSaveProjectPathAsync!();
        Assert.NotNull(path);
        var saved = JsonProjectStore.Load(path);
        Assert.Equal(1.25d, saved.CadPadCustomValues[11]);
        Assert.False(saved.CadPadCustomValues.ContainsKey(22));
        vm.UndoCommand.Execute(null);
        Assert.Equal(0d, vm.GetCadPadCustomValue(11));
        Assert.False(vm.CanUndo);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task InteractiveMatch_ClearsStep2AndMovesForwardWhileLoadReplayKeepsOverridesAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGlobalGridAsync(fixture);
        vm.AutoReplayStep2AfterProjectLoad = false;
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.True(vm.IsStep2Expanded);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        var links = vm.MatchLinksForCanvas.ToArray();
        var pad = vm.RegularPads[0];
        vm.SetFreeformForPadIndices([pad.Index], FreeformType.XYWay);
        Assert.True(await vm.SaveProjectAsync());

        await vm.MatchCommand.ExecuteAsync(null);

        Assert.All(vm.RegularPads, static pad => Assert.Equal(FreeformType.None, pad.Freeform));
        Assert.True(vm.IsStep2Expanded);
        Assert.Equal("Matching complete.", vm.StatusText);
        var loaded = fixture.CreateViewModel();
        await loaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(loaded);
        Assert.Equal(FreeformType.XYWay, Assert.Single(loaded.RegularPads, item => item.Index == pad.Index).Freeform);
        Assert.Equal(links, loaded.MatchLinksForCanvas.ToArray());
        Assert.Contains("Step 1 match restored", loaded.StatusText, StringComparison.Ordinal);
        Assert.False(loaded.IsStep2Expanded);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task AutoDetect_InteractiveIgnoresManualOverridesAndLoadReplayReappliesThemAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGlobalGridAsync(fixture);
        vm.AutoReplayStep2AfterProjectLoad = true;
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
        var detected = vm.RegularPads.Select(static pad => pad.Freeform).ToArray();
        var pad = vm.RegularPads[0];
        var manual = detected[0] == FreeformType.XYWay ? FreeformType.None : FreeformType.XYWay;
        vm.SetFreeformForPadIndices([pad.Index], manual);
        Assert.True(await vm.SaveProjectAsync());

        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);

        Assert.Equal(detected, vm.RegularPads.Select(static pad => pad.Freeform).ToArray());
        Assert.True(vm.IsStep3Expanded);
        Assert.Equal(vm.FreeformAutoDetectSummary, vm.StatusText);
        var loaded = fixture.CreateViewModel();
        await loaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(loaded);
        Assert.Equal(manual, Assert.Single(loaded.RegularPads, item => item.Index == pad.Index).Freeform);
        Assert.Contains("Step 2 freeform restored", loaded.StatusText, StringComparison.Ordinal);
        Assert.False(loaded.IsStep3Expanded);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GridPublication_SourceEventPrecedesSelectionClearOnlyForSuccessfulBuildsAsync(bool empty)
    {
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGlobalGridAsync(fixture);
        vm.ApplyCanvasSelection([], [vm.RegularPads[0].Index]);
        var events = new List<string>();
        var sourceSelections = new List<int>();
        vm.SimulationWorkspaceSourceChanged += (_, _) =>
        {
            events.Add("source");
            sourceSelections.Add(vm.SelectedRegularPadIndices.Count);
        };
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.SelectedRegularPadIndices))
            {
                events.Add("selection");
            }
        };

        if (empty)
        {
            vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        }
        else
        {
            vm.GridPaddingPercent += 1m;
        }
        await WaitForGridRebuildAsync(vm);

        Assert.NotEmpty(sourceSelections);
        Assert.Contains("selection", events);
        Assert.Equal(empty ? 0 : 1, sourceSelections[^1]);
        Assert.Equal(empty, events.LastIndexOf("source") > events.IndexOf("selection"));
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Equal(empty, vm.RegularPads.Count == 0);
    }

    [Fact]
    public void PitchSummaries_UseActualPositiveSizesWithToleranceAndNominalFallback()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        var widths = new[] { 2d, 2.00005, 2.0002, 4d };
        var pads = widths.Select((width, index) => new RegularPad(0, index, index,
            new Polygon2([new(0, 0), new(width, 0), new(width, 3), new(0, 3)]))).ToArray();
        FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(1, 4, [], [], pads);

        Assert.Equal("Pitch X: X1=2 mm, X2=2 mm, X3=4 mm", vm.PitchSizeXSummary);
        Assert.Equal("Pitch Y: Y1=3 mm", vm.PitchSizeYSummary);
        FreeformHelperViewModelPrivateAccess.Grid(vm) = null;
        Assert.Equal($"Pitch X: X1={vm.PitchSizeX:0.###} mm", vm.PitchSizeXSummary);
        Assert.Equal($"Pitch Y: Y1={vm.PitchSizeY:0.###} mm", vm.PitchSizeYSummary);
    }

    private static async Task<FreeformHelperViewModel> OpenGlobalGridAsync(WorkspaceFixture fixture)
    {
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        vm.IsWidthRowLocal = false;
        vm.IsHeightColumnLocal = false;
        await WaitForGridRebuildAsync(vm);
        return vm;
    }
}
