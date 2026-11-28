// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ManualSizingRangeAndReloadBehaviorTests
{
    [AvaloniaFact]
    public async Task ManualRange_InvalidColumnWithValidRow_PreservesGeometryAndSelectionUntilCorrectedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        var selected = Assert.Single(vm.RegularPads, pad => vm.ToDisplayRow(pad.Row) == 1 && pad.Col == 0);
        var next = Assert.Single(vm.RegularPads, pad => pad.Row == selected.Row && pad.Col == 1);
        vm.ApplyCanvasSelection([], [selected.Index]);
        var bounds = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        vm.HasUnsavedChanges = false;

        vm.ManualColsRange = "999999";

        Assert.Equal("Cols: invalid.", vm.ManualRangeStatus);
        Assert.NotEmpty(vm.ManualRangeError);
        Assert.True(vm.HasManualRangeError);
        Assert.Equal(selected.Index, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(bounds, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.False(vm.HasUnsavedChanges);

        vm.PendingColumnWidth = 0m;
        vm.PendingRowHeight = 0m;
        vm.ManualColsRange = "1";
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(vm.ManualRangeError);
        Assert.False(vm.HasManualRangeError);
        Assert.Equal("Manual range: Rows 1, Cols 1.", vm.ManualRangeStatus);
        Assert.Equal(next.Index, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(bounds, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_ActiveManualSizing_ResetsInputsWithoutApplyingThemToTheLoadedGridAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        var savedBounds = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        Assert.True(await vm.SaveProjectAsync());
        var selected = Assert.Single(vm.RegularPads, pad => vm.ToDisplayRow(pad.Row) == 1 && pad.Col == 0);
        vm.ApplyCanvasSelection([], [selected.Index]);
        vm.PendingColumnWidth = 6m;
        await WaitForGridRebuildAsync(vm);
        vm.PendingRowHeight = 7m;
        await WaitForGridRebuildAsync(vm);
        var resized = Assert.Single(vm.RegularPads, pad => pad.Index == selected.Index);
        Assert.Equal(6d, resized.Bounds.Width, 6);
        Assert.Equal(7d, resized.Bounds.Height, 6);
        Assert.Equal("1", vm.ManualRowsRange);
        Assert.Equal("0", vm.ManualColsRange);
        var statuses = new List<string>();
        var rangeStatuses = new List<string>();
        var clearedRangeSizes = new List<(decimal Width, decimal Height)>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(FreeformHelperViewModel.StatusText))
            {
                statuses.Add(vm.StatusText);
            }
            if (args.PropertyName is nameof(FreeformHelperViewModel.ManualRangeStatus))
            {
                rangeStatuses.Add(vm.ManualRangeStatus);
            }
            if (args.PropertyName is nameof(FreeformHelperViewModel.ManualColsRange) && vm.ManualColsRange.Length == 0)
            {
                clearedRangeSizes.Add((vm.PendingColumnWidth, vm.PendingRowHeight));
            }
        };

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.DoesNotContain("Manual sizing applied.", statuses);
        Assert.DoesNotContain("Rows: invalid.", rangeStatuses);
        Assert.DoesNotContain("Cols: invalid.", rangeStatuses);
        Assert.NotEmpty(clearedRangeSizes);
        Assert.All(clearedRangeSizes, static sizes => Assert.Equal((0m, 0m), sizes));
        Assert.Equal(savedBounds, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
        Assert.Equal(0m, vm.PendingColumnWidth);
        Assert.Equal(0m, vm.PendingRowHeight);
        Assert.Empty(vm.ManualRowsRange);
        Assert.Empty(vm.ManualColsRange);
        Assert.Empty(vm.ManualRangeError);
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.False(vm.HasUnsavedChanges);
    }
}
