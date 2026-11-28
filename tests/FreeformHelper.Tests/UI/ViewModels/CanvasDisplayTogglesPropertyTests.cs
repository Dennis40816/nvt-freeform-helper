// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the seven canvas display switches and DXF import flags of the main view model: the start values, the
/// change notifications, the unsaved flag, the undo entry and a project file that stays identical, except for
/// its save time, after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class CanvasDisplayTogglesPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.ShowCad), false, true, true),
        new(nameof(FreeformHelperViewModel.ShowRegular), false, true, true),
        new(nameof(FreeformHelperViewModel.HighlightUnmatched), false, true, false),
        new(nameof(FreeformHelperViewModel.HighlightFreeform), false, true, false),
        new(nameof(FreeformHelperViewModel.ColorCadByArea), true, true, true),
        new(nameof(FreeformHelperViewModel.ImportOnlyClosedPolylines), false, true, true),
        new(nameof(FreeformHelperViewModel.ImportBlockPolylines), false, true, true),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.True(vm.ShowCad);
        Assert.True(vm.ShowRegular);
        Assert.True(vm.HighlightUnmatched);
        Assert.True(vm.HighlightFreeform);
        Assert.False(vm.ColorCadByArea);
        Assert.True(vm.ImportOnlyClosedPolylines);
        Assert.True(vm.ImportBlockPolylines);
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
