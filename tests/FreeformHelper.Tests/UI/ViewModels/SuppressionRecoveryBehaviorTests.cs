// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SettingsSynchronizationBehaviorTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SuppressionRecoveryBehaviorTests
{
    [Fact]
    public void NotchThresholdQ7_NotificationThrows_RestoresThresholdSynchronization()
    {
        var vm = new FreeformHelperViewModel { LinkNotchThresholds = true };
        OnNextChange(vm, nameof(vm.NotchThresholdPercent), ThrowNotification);

        Assert.Throws<InvalidOperationException>(() => vm.NotchThresholdQ7 = 65);

        vm.NotchThresholdQ7 = 32;
        Assert.Equal(25m, vm.NotchThresholdPercent);
    }

    [Fact]
    public void NotchThresholdPercent_NotificationThrows_RestoresThresholdSynchronization()
    {
        var vm = new FreeformHelperViewModel { LinkNotchThresholds = true };
        OnNextChange(vm, nameof(vm.NotchThresholdQ7), ThrowNotification);

        Assert.Throws<InvalidOperationException>(() => vm.NotchThresholdPercent = 25);

        vm.NotchThresholdPercent = 50;
        Assert.Equal(64m, vm.NotchThresholdQ7);
    }

    [Fact]
    public void LinkNotchThresholds_NotificationThrows_RestoresThresholdSynchronization()
    {
        var vm = new FreeformHelperViewModel { LinkNotchThresholds = false, NotchThresholdQ7 = 64, NotchThresholdPercent = 10 };
        OnNextChange(vm, nameof(vm.NotchThresholdPercent), ThrowNotification);

        Assert.Throws<InvalidOperationException>(() => vm.LinkNotchThresholds = true);

        vm.NotchThresholdQ7 = 32;
        Assert.Equal(25m, vm.NotchThresholdPercent);
    }

    [Fact]
    public void UseLocalSizing_NotificationThrows_RestoresScopeSynchronization()
    {
        var vm = new FreeformHelperViewModel { UseLocalSizing = true };
        OnNextChange(vm, nameof(vm.IsWidthRowLocal), ThrowNotification);

        Assert.Throws<InvalidOperationException>(() => vm.UseLocalSizing = false);

        vm.IsWidthRowLocal = true;
        Assert.True(vm.UseLocalSizing);
    }

    [Fact]
    public void CascadeIcSetting_NotificationThrows_RestoresGlobalChannelHandler()
    {
        var vm = new FreeformHelperViewModel { CascadeNum = 2 };
        var first = vm.CascadeIcSettings[0];
        OnNextChange(vm, nameof(vm.XChannels), ThrowNotification);

        Assert.Throws<InvalidOperationException>(() => first.XChannels += 1);

        vm.XChannels += 2;
        Assert.NotSame(first, vm.CascadeIcSettings[0]);
    }

    [Fact]
    public async Task DeselectAllLayers_NotificationThrows_RestoresLayerToggleHandler()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        OnNextChange(vm.LayerToggles[0], "IsSelected", ThrowNotification);

        Assert.Throws<InvalidOperationException>(vm.DeselectAllLayers);

        vm.LayerToggles[0].IsSelected = true;
        vm.LayerToggles[1].IsSelected = false;
        Assert.Single(vm.CadPads);
        await WaitForGridRebuildAsync(vm);
    }

    [Fact]
    public async Task ApplyCanvasSelection_NotificationThrows_RestoresManualInputHandler()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        OnNextChange(vm, nameof(vm.ManualRowsRange), ThrowNotification);

        Assert.Throws<InvalidOperationException>(() => vm.ApplyCanvasSelection([], [vm.RegularPads[0].Index]));

        vm.ManualRowsRange = "999999";
        vm.ManualColsRange = "0";
        Assert.True(vm.HasManualRangeError);
        await WaitForGridRebuildAsync(vm);
    }

    [Fact]
    public async Task OpenDxfCommand_MaskResetNotificationThrows_RestoresMaskToggleHandler()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.RegularVisibilityMaskUi.IsEnabled = true;
        OnNextChange(vm.RegularVisibilityMaskUi, nameof(RegularVisibilityMaskUiState.IsEnabled), ThrowNotification);

        await vm.OpenDxfCommand.ExecuteAsync(null);

        Assert.False(vm.RegularVisibilityMaskUi.IsEnabled);
        vm.RegularVisibilityMaskUi.IsEnabled = true;
        Assert.Equal("Regular visibility mask enabled.", vm.StatusText);
        await WaitForGridRebuildAsync(vm);
    }

    [Fact]
    public async Task DeselectAllLayers_NestedBulkChange_KeepsOuterToggleHandlerSuppressed()
    {
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        var intermediateCounts = new List<int>();
        OnNextChange(vm.LayerToggles[0], "IsSelected", () =>
        {
            vm.SelectAllLayers();
            vm.LayerToggles[1].IsSelected = false;
            intermediateCounts.Add(vm.CadPads.Count);
        });

        vm.DeselectAllLayers();

        Assert.Equal([2], intermediateCounts);
        Assert.Single(vm.CadPads);
        vm.LayerToggles[1].IsSelected = true;
        Assert.Equal(2, vm.CadPads.Count);
        await WaitForGridRebuildAsync(vm);
    }

    private static void ThrowNotification() => throw new InvalidOperationException("Property notification failed.");
}
