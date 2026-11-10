// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the sixteen notch algorithm settings of the main view model: the start values, the
/// change notifications, the unsaved flag, the undo entry, the clamps, the threshold link and a
/// project file that stays identical, except for its save time, after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class NotchAlgorithmSettingsPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo);

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.EnableV21), false, true),
        new(nameof(FreeformHelperViewModel.EnableV22), false, true),
        new(nameof(FreeformHelperViewModel.LenScale), 3m, true),
        new(nameof(FreeformHelperViewModel.NullValue), 7m, true),
        new(nameof(FreeformHelperViewModel.NotchThresholdQ7), 5m, true),
        new(nameof(FreeformHelperViewModel.NotchThresholdPercent), 5m, true),
        new(nameof(FreeformHelperViewModel.LinkNotchThresholds), false, true),
        new(nameof(FreeformHelperViewModel.EnableToRegular), false, true),
        new(nameof(FreeformHelperViewModel.EnableToFull), false, true),
        new(nameof(FreeformHelperViewModel.ToFullStrictOverlapPercent), 2.5m, true),
        new(nameof(FreeformHelperViewModel.EnableToFullRuleEngine), false, false),
        new(nameof(FreeformHelperViewModel.EnableToFullRuleTrace), true, false),
        new(nameof(FreeformHelperViewModel.EnableBoundaryVirtualAreaCap), false, false),
        new(nameof(FreeformHelperViewModel.BoundaryVirtualAreaCapPercent), 50m, false),
        new(nameof(FreeformHelperViewModel.EnableTargetCoverageGuard), false, false),
        new(nameof(FreeformHelperViewModel.TargetCoverageCapPercent), 80m, false),
    ];

    private static readonly (string Name, decimal Input, decimal Clamped)[] _clamps =
    [
        (nameof(FreeformHelperViewModel.ToFullStrictOverlapPercent), 150m, 100m),
        (nameof(FreeformHelperViewModel.NotchThresholdQ7), 200m, 128m),
        (nameof(FreeformHelperViewModel.NotchThresholdPercent), 150m, 100m),
        (nameof(FreeformHelperViewModel.BoundaryVirtualAreaCapPercent), 2000m, 1000m),
        (nameof(FreeformHelperViewModel.TargetCoverageCapPercent), 300m, 255m),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    public static TheoryData<int> ClampIndexes => new(Enumerable.Range(0, _clamps.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.True(vm.EnableV21);
        Assert.True(vm.EnableV22);
        Assert.Equal(10m, vm.LenScale);
        Assert.Equal(65535m, vm.NullValue);
        Assert.Equal(0m, vm.NotchThresholdQ7);
        Assert.Equal(0m, vm.NotchThresholdPercent);
        Assert.True(vm.LinkNotchThresholds);
        Assert.True(vm.EnableToRegular);
        Assert.True(vm.EnableToFull);
        Assert.Equal(0.1m, vm.ToFullStrictOverlapPercent);
        Assert.True(vm.EnableToFullRuleEngine);
        Assert.False(vm.EnableToFullRuleTrace);
        Assert.True(vm.EnableBoundaryVirtualAreaCap);
        Assert.Equal(100m, vm.BoundaryVirtualAreaCapPercent);
        Assert.True(vm.EnableTargetCoverageGuard);
        Assert.Equal(120m, vm.TargetCoverageCapPercent);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_RaisesChangingBeforeChangedOnceAndKeepsTheOtherValues(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        if (setting.Name != nameof(FreeformHelperViewModel.LinkNotchThresholds))
        {
            // The two thresholds follow each other while linked. Unlinked, each one is independent.
            vm.LinkNotchThresholds = false;
        }

        await AssertChangeNotificationsAsync(vm, setting.Name, setting.NewValue, _settings.Select(item => item.Name));
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_SameValue_RaisesNothing(int index)
    {
        var setting = _settings[index];
        AssertSameValueRaisesNothing(vm => Write(vm, setting.Name, Read(vm, setting.Name)));
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_MarksTheProjectAsChanged(int index)
    {
        var setting = _settings[index];
        var vm = await SetValueOnCleanProjectAsync(setting.Name, setting.NewValue);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_AddsAnUndoEntryOnlyWhereTheSettingHasUndo(int index)
    {
        var setting = _settings[index];
        await AssertUndoAsync(setting.Name, setting.NewValue, setting.HasUndo);
    }

    [Theory]
    [MemberData(nameof(ClampIndexes))]
    public void Set_ValueAboveTheLimit_StoresTheLimit(int index)
    {
        var (name, input, clamped) = _clamps[index];
        var vm = new FreeformHelperViewModel();

        Write(vm, name, input);

        Assert.Equal(clamped, Read(vm, name));
    }

    [Fact]
    public void Set_ThresholdQ7WhileLinked_UpdatesThePercentThreshold()
    {
        var vm = new FreeformHelperViewModel();
        Assert.True(vm.LinkNotchThresholds);

        vm.NotchThresholdQ7 = 64m;

        Assert.Equal(50m, vm.NotchThresholdPercent);
    }

    [Fact]
    public void Set_ThresholdQ7WhileNotLinked_KeepsThePercentThreshold()
    {
        var vm = new FreeformHelperViewModel();
        vm.LinkNotchThresholds = false;

        vm.NotchThresholdQ7 = 64m;

        Assert.Equal(0m, vm.NotchThresholdPercent);
    }

    [AvaloniaFact]
    public async Task SaveLoadSave_ChangedValues_WritesTheSameProjectFileExceptTheSaveTime()
    {
        await AssertProjectRoundTripAsync(
            saved =>
            {
                saved.LinkNotchThresholds = false;
                WriteValues(saved, _settings.Where(item => item.Name != nameof(FreeformHelperViewModel.LinkNotchThresholds))
                    .Select(setting => (setting.Name, RoundTripValue(setting))));
            },
            vm => AssertValues(vm, _settings.Select(setting => (setting.Name, RoundTripValue(setting)))));
    }

    // A loaded project takes the version 2.2 output path and the two compensation switches from its
    // compensation model and output version, so the round trip leaves these three on.
    private static object RoundTripValue(Setting setting) =>
        setting.Name is nameof(FreeformHelperViewModel.EnableV22)
            or nameof(FreeformHelperViewModel.EnableToRegular)
            or nameof(FreeformHelperViewModel.EnableToFull)
            ? true
            : setting.NewValue;
}
