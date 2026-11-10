// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the fourteen grid, coordinate, active area, panel offset and match settings of the main view model: the
/// start values, the change notifications, the unsaved flag, the undo entry, the coordinate rounding and a project
/// file that stays identical, except for its save time, after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class GridAndMatchSettingsPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo);

    private static readonly string[] _recalculated = [nameof(FreeformHelperViewModel.XChannels), nameof(FreeformHelperViewModel.YChannels), nameof(FreeformHelperViewModel.CascadeNum)];

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.XChannels), 4m, true),
        new(nameof(FreeformHelperViewModel.YChannels), 6m, true),
        new(nameof(FreeformHelperViewModel.CascadeNum), 2m, true),
        new(nameof(FreeformHelperViewModel.CoordinatePixelWidth), 3m, false),
        new(nameof(FreeformHelperViewModel.CoordinatePixelHeight), 5m, false),
        new(nameof(FreeformHelperViewModel.GridPaddingPercent), 5m, true),
        new(nameof(FreeformHelperViewModel.ActiveAreaWidth), 300m, true),
        new(nameof(FreeformHelperViewModel.ActiveAreaHeight), 170m, true),
        new(nameof(FreeformHelperViewModel.PanelBiasX), 2m, false),
        new(nameof(FreeformHelperViewModel.PanelBiasY), 3m, false),
        new(nameof(FreeformHelperViewModel.MatchThreshold), 0.3m, true),
        new(nameof(FreeformHelperViewModel.FreeformAxisThreshold), 0.4m, true),
        new(nameof(FreeformHelperViewModel.EnableAutoDetectXy), true, true),
        new(nameof(FreeformHelperViewModel.EnableFreeformEdgeSpecialization), true, true),
    ];

    private static readonly (string Name, decimal Input, decimal Clamped)[] _clamps =
    [
        (nameof(FreeformHelperViewModel.CoordinatePixelWidth), 0.4m, 1m),
        (nameof(FreeformHelperViewModel.CoordinatePixelHeight), 2.6m, 3m),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    public static TheoryData<int> ClampIndexes => new(Enumerable.Range(0, _clamps.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal(32m, vm.XChannels);
        Assert.Equal(20m, vm.YChannels);
        Assert.Equal(1m, vm.CascadeNum);
        Assert.Equal(32m, vm.CoordinatePixelWidth);
        Assert.Equal(20m, vm.CoordinatePixelHeight);
        Assert.Equal(0m, vm.GridPaddingPercent);
        Assert.Equal(310m, vm.ActiveAreaWidth);
        Assert.Equal(174m, vm.ActiveAreaHeight);
        Assert.Equal(0m, vm.PanelBiasX);
        Assert.Equal(0m, vm.PanelBiasY);
        Assert.Equal(0.35m, vm.MatchThreshold);
        Assert.Equal(0.1m, vm.FreeformAxisThreshold);
        Assert.False(vm.EnableAutoDetectXy);
        Assert.False(vm.EnableFreeformEdgeSpecialization);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_RaisesChangingBeforeChangedOnceAndKeepsTheOtherValues(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();

        await AssertChangeNotificationsAsync(vm, setting.Name, setting.NewValue, _settings.Select(item => item.Name), _recalculated);
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
    public void Set_ValueOutsideTheLimit_StoresTheLimit(int index)
    {
        var (name, input, clamped) = _clamps[index];
        var vm = new FreeformHelperViewModel();

        Write(vm, name, input);

        Assert.Equal(clamped, Read(vm, name));
    }

    [AvaloniaFact]
    public async Task SaveLoadSave_ChangedValues_WritesTheSameProjectFileExceptTheSaveTime()
    {
        await AssertProjectRoundTripAsync(
            saved => WriteValues(saved, _settings.Select(setting => (setting.Name, RoundTripValue(setting)))),
            vm => AssertValues(vm, _settings.Where(setting => !_recalculated.Contains(setting.Name))
                .Select(setting => (setting.Name, RoundTripValue(setting)))));
    }

    private static object RoundTripValue(Setting setting) => setting.NewValue;
}
