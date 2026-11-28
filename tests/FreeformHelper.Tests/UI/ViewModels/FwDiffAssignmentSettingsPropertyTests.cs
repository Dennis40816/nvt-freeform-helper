// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the five CAD Output FW Diff assignment settings of the main view model: the start values, the change
/// notifications, the clamps of the start index and the anchor, the unsaved flag, the undo entry and a project
/// file that stays identical, except for its save time, after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class FwDiffAssignmentSettingsPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly string[] _recalculated = [nameof(FreeformHelperViewModel.CadOutputFwDiffAutoMode), nameof(FreeformHelperViewModel.CadOutputFwDiffIndexAnchorCadId), nameof(FreeformHelperViewModel.CadOutputFwDiffAssignmentSummary)];

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.CadOutputFwDiffIndexStart), 5m, true, true),
        new(nameof(FreeformHelperViewModel.CadOutputFwDiffAutoMode), FreeformHelper.Application.Settings.CadOutputFwDiffAutoMode.StrictUnique, true, true),
        new(nameof(FreeformHelperViewModel.CadOutputFwDiffIndexAnchorCadId), 3m, true, true),
        new(nameof(FreeformHelperViewModel.CadOutputFwDiffAssignmentSummary), "changed summary", false, false),
        new(nameof(FreeformHelperViewModel.ShowDiffIndexOverlay), false, true, false),
    ];

    private static readonly (string Name, decimal Input, decimal Clamped)[] _clamps =
    [
        (nameof(FreeformHelperViewModel.CadOutputFwDiffIndexStart), -5m, 0m),
        (nameof(FreeformHelperViewModel.CadOutputFwDiffIndexStart), 2000000m, 1000000m),
        (nameof(FreeformHelperViewModel.CadOutputFwDiffIndexAnchorCadId), -9m, -1m),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    public static TheoryData<int> ClampIndexes => new(Enumerable.Range(0, _clamps.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal(0m, vm.CadOutputFwDiffIndexStart);
        Assert.Equal(FreeformHelper.Application.Settings.CadOutputFwDiffAutoMode.BestMatchDirect, vm.CadOutputFwDiffAutoMode);
        Assert.Equal(-1m, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.Equal("CAD Output FW Diff assignment (per-IC): auto, scan-order based.", vm.CadOutputFwDiffAssignmentSummary);
        Assert.True(vm.ShowDiffIndexOverlay);
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
            vm => AssertValues(vm, _settings.Where(setting => !_recalculated.Contains(setting.Name))
                .Select(setting => (setting.Name, RoundTripValue(setting)))));
    }

    private static object RoundTripValue(Setting setting) => setting.NewValue;
}
