// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the five notch preview settings of the main view model: the start values, the change notifications,
/// the clamp of the visualization step and the auto-play interval, the unsaved flag, the undo entry and a project
/// file that stays identical, except for its save time, after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class NotchPreviewSettingsPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.ShowNotchCanvasPreview), false, false, true),
        new(nameof(FreeformHelperViewModel.ShowNotchToRegularLabels), false, true, true),
        new(nameof(FreeformHelperViewModel.NotchPreviewVisualizationStep), 2m, false, false),
        new(nameof(FreeformHelperViewModel.NotchPreviewAutoPlayEnabled), false, false, true),
        new(nameof(FreeformHelperViewModel.NotchPreviewAutoPlayIntervalMs), 900m, false, true),
    ];

    private static readonly (string Name, decimal Input, decimal Clamped)[] _clamps =
    [
        (nameof(FreeformHelperViewModel.NotchPreviewVisualizationStep), 0m, 1m),
        (nameof(FreeformHelperViewModel.NotchPreviewVisualizationStep), 9m, 3m),
        (nameof(FreeformHelperViewModel.NotchPreviewAutoPlayIntervalMs), 100m, 200m),
        (nameof(FreeformHelperViewModel.NotchPreviewAutoPlayIntervalMs), 9000m, 5000m),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    public static TheoryData<int> ClampIndexes => new(Enumerable.Range(0, _clamps.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.True(vm.ShowNotchCanvasPreview);
        Assert.True(vm.ShowNotchToRegularLabels);
        Assert.Equal(3m, vm.NotchPreviewVisualizationStep);
        Assert.True(vm.NotchPreviewAutoPlayEnabled);
        Assert.Equal(700m, vm.NotchPreviewAutoPlayIntervalMs);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public async Task Set_NewValue_RaisesChangingBeforeChangedOnceAndKeepsTheOtherValues(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();

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
    public async Task Set_NewValue_SetsTheUnsavedFlagAsBefore(int index)
    {
        var setting = _settings[index];
        var vm = await SetValueOnCleanProjectAsync(setting.Name, setting.NewValue);
        Assert.Equal(setting.MarksUnsaved, vm.HasUnsavedChanges);
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
            vm => AssertValues(vm, _settings
                .Select(setting => (setting.Name, RoundTripValue(setting)))));
    }

    private static object RoundTripValue(Setting setting) => setting.NewValue;
}
