// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.LoadingModeTestScope;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class GridRebuildBusyLifecycleBehaviorTests
{
    private static readonly bool[] _spinnerSequence = [true, false];
    [AvaloniaFact]
    public async Task GridRebuild_LoadedDxf_ShowsSpinnerDuringGridChangeAndHidesItAtCompletion()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        using var changes = ObserveSpinner(vm);
        using var grid = OnNextChange(vm, nameof(vm.RegularPads), () => Assert.True(vm.IsModalLoadingSpinnerVisible));

        vm.GridPaddingPercent += 5m;
        var rebuild = vm.WaitForGridRebuildIdleAsync();
        Assert.True(vm.IsModalLoadingSpinnerVisible);
        await rebuild;

        Assert.True(grid.WasInvoked);
        Assert.Equal(_spinnerSequence, changes.Changes.Select(change => change.Busy));
        Assert.False(vm.IsModalLoadingSpinnerVisible);
        Assert.Equal(2, vm.RegularPads.Count);
        Assert.Equal("Ready.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task GridRebuild_NoCad_HidesSpinnerAfterBuildingFallbackGrid()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateWithoutDxfAsync();
        using var changes = ObserveSpinner(vm);

        vm.GridPaddingPercent += 5m;
        var rebuild = vm.WaitForGridRebuildIdleAsync();
        Assert.True(vm.IsModalLoadingSpinnerVisible);
        await rebuild;

        Assert.Equal(_spinnerSequence, changes.Changes.Select(change => change.Busy));
        Assert.False(vm.IsModalLoadingSpinnerVisible);
        Assert.Empty(vm.CadPads);
        Assert.NotEmpty(vm.RegularPads);
        Assert.Equal("Ready (DXF not loaded; using panel AA alignment).", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task GridRebuild_RequestsDuringRebuild_CoalescesOneFollowUpUsingLatestChannels()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        using var changes = new LifecycleNotifications(vm, () => vm.IsModalLoadingSpinnerVisible, () => 0, "RegularPads");
        using var requests = OnNextChange(vm, nameof(vm.RegularPads), () =>
        {
            Assert.False(vm.WaitForGridRebuildIdleAsync().IsCompleted);
            Assert.Single(vm.CascadeIcSettings).XChannels = 3;
            vm.GridPaddingPercent += 5m;
            vm.GridPaddingPercent += 5m;
        });

        vm.GridPaddingPercent += 5m;
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.True(requests.WasInvoked);
        Assert.Equal(2, changes.Changes.Length);
        Assert.All(changes.Changes, change => Assert.True(change.Busy));
        Assert.Equal(3, vm.RegularPads.Count);
        Assert.Equal(3, vm.RegularPads.Select(pad => pad.RegularPadId).Distinct().Count());
        Assert.False(vm.IsModalLoadingSpinnerVisible);
        Assert.Equal("Ready.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task GridRebuild_OneRequest_ChangesRegularPadsOnce()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateLoadedAsync();
        using var changes = new LifecycleNotifications(vm, () => vm.IsModalLoadingSpinnerVisible, () => 0, "RegularPads");

        vm.GridPaddingPercent += 5m;
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Single(changes.Changes);
        Assert.Equal(2, vm.RegularPads.Count);
        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    private static LifecycleNotifications ObserveSpinner(FreeformHelperViewModel vm) => new(vm,
        () => vm.IsModalLoadingSpinnerVisible, () => 0, "IsModalLoadingSpinnerVisible");
}
