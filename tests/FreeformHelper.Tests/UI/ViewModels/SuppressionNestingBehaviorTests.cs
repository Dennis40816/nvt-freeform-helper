// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SettingsSynchronizationBehaviorTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SuppressionNestingBehaviorTests
{
    [Fact]
    public async Task ApplyCanvasSelection_NestedRangeNotification_KeepsOuterManualSizingHandlerSuppressed()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var outer = vm.RegularPads[0];
        var inner = vm.RegularPads.First(pad => pad.Row != outer.Row && pad.Col != outer.Col);
        var originalBounds = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var sizingStatuses = new List<string>();
        OnNextChange(vm, nameof(vm.StatusText), () => sizingStatuses.Add(vm.StatusText));
        OnNextChange(vm, nameof(vm.ManualRowsRange), () => vm.ApplyCanvasSelection([], [inner.Index]));

        vm.ApplyCanvasSelection([], [outer.Index]);

        Assert.Empty(sizingStatuses);
        Assert.False(vm.CanUndo);
        Assert.Equal([inner.Index], vm.SelectedRegularPadIndices);
        Assert.Equal(vm.ToDisplayRow(inner.Row).ToString(CultureInfo.InvariantCulture), vm.ManualRowsRange);
        Assert.Equal(outer.Col.ToString(CultureInfo.InvariantCulture), vm.ManualColsRange);
        Assert.Equal((decimal)outer.Bounds.Width, vm.PendingColumnWidth);
        Assert.Equal((decimal)outer.Bounds.Height, vm.PendingRowHeight);
        Assert.Equal(originalBounds, vm.RegularPads.Select(static pad => pad.Bounds));

        var width = (decimal)outer.Bounds.Width + 1;
        vm.PendingColumnWidth = width;
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(["Manual sizing applied."], sizingStatuses);
        Assert.True(vm.CanUndo);
        Assert.Equal((double)width,
            vm.RegularPads.Single(pad => pad.Row == inner.Row && pad.Col == outer.Col).Bounds.Width, 5);
    }

    [Fact]
    public async Task CascadeIcSetting_NestedChannelNotification_PreservesPerIcObjectsUntilOuterChangeCompletes()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.CascadeNum = 2;
        await WaitForGridRebuildAsync(vm);
        var first = vm.CascadeIcSettings[0];
        var second = vm.CascadeIcSettings[1];
        var originalTotal = vm.XChannels;
        OnNextChange(vm, nameof(vm.XChannels), () =>
        {
            second.XChannels += 1;
            vm.XChannels += 2;
        });

        first.XChannels += 1;

        Assert.Same(first, vm.CascadeIcSettings[0]);
        Assert.Same(second, vm.CascadeIcSettings[1]);
        Assert.Equal(originalTotal + 4, vm.XChannels);
        await WaitForGridRebuildAsync(vm);
        Assert.Same(first, vm.CascadeIcSettings[0]);
        Assert.Same(second, vm.CascadeIcSettings[1]);

        vm.XChannels += 2;

        Assert.NotSame(first, vm.CascadeIcSettings[0]);
        Assert.NotSame(second, vm.CascadeIcSettings[1]);
        await WaitForGridRebuildAsync(vm);
    }
}
