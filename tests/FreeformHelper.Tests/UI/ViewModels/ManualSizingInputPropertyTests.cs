// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the manual sizing inputs of the main view model: the start values, the change notifications,
/// the unsaved flag, the undo entry and a project file that stays identical, except for its save time,
/// after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class ManualSizingInputPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly string[] _recalculated = [nameof(FreeformHelperViewModel.ManualRangeStatus)];

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.PendingColumnWidth), 5m, true, false),
        new(nameof(FreeformHelperViewModel.PendingRowHeight), 6m, true, false),
        new(nameof(FreeformHelperViewModel.IsPendingColumnWidthMixed), true, false, false),
        new(nameof(FreeformHelperViewModel.IsPendingRowHeightMixed), true, false, false),
        new(nameof(FreeformHelperViewModel.ManualRowsRange), "1-3", true, false),
        new(nameof(FreeformHelperViewModel.ManualColsRange), "A-C", true, false),
        new(nameof(FreeformHelperViewModel.ManualRangeStatus), "changed", false, false),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal(0m, vm.PendingColumnWidth);
        Assert.Equal(0m, vm.PendingRowHeight);
        Assert.False(vm.IsPendingColumnWidthMixed);
        Assert.False(vm.IsPendingRowHeightMixed);
        Assert.Equal("", vm.ManualRowsRange);
        Assert.Equal("", vm.ManualColsRange);
        Assert.Equal("No manual range.", vm.ManualRangeStatus);
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
    [InlineData(nameof(FreeformHelperViewModel.ManualRowsRange), "1-3")]
    [InlineData(nameof(FreeformHelperViewModel.ManualColsRange), "A-C")]
    public void Set_ManualRange_RewritesTheRangeStatusWhileNoGridIsBuilt(string name, string value)
    {
        var vm = new FreeformHelperViewModel();
        Assert.Equal("No manual range.", vm.ManualRangeStatus);

        Write(vm, name, value);

        Assert.Equal("Grid not built.", vm.ManualRangeStatus);
    }

    [Fact]
    public void Set_PendingColumnWidth_ClearsTheMixedFlagOfTheWidth()
    {
        var vm = new FreeformHelperViewModel();
        vm.IsPendingColumnWidthMixed = true;

        vm.PendingColumnWidth = 5m;

        Assert.False(vm.IsPendingColumnWidthMixed);
    }

    [Fact]
    public void Set_PendingRowHeight_ClearsTheMixedFlagOfTheHeight()
    {
        var vm = new FreeformHelperViewModel();
        vm.IsPendingRowHeightMixed = true;

        vm.PendingRowHeight = 6m;

        Assert.False(vm.IsPendingRowHeightMixed);
    }

    [AvaloniaFact]
    public async Task SaveLoadSave_ChangedValues_WritesTheSameProjectFileExceptTheSaveTime()
    {
        await AssertProjectRoundTripAsync(
            saved => WriteValues(saved, _settings.Select(setting => (setting.Name, RoundTripValue(setting)))),
            vm => AssertValues(vm, _settings.Where(setting => !_recalculated.Contains(setting.Name))
                .Select(setting => (setting.Name, RoundTripValue(setting)))));
    }

    // The project file does not store the manual sizing inputs, so the round trip keeps their start values.
    private static object RoundTripValue(Setting setting) =>
        setting.Name switch
        {
            nameof(FreeformHelperViewModel.PendingColumnWidth) => 0m,
            nameof(FreeformHelperViewModel.PendingRowHeight) => 0m,
            nameof(FreeformHelperViewModel.IsPendingColumnWidthMixed) => false,
            nameof(FreeformHelperViewModel.IsPendingRowHeightMixed) => false,
            nameof(FreeformHelperViewModel.ManualRowsRange) => "",
            nameof(FreeformHelperViewModel.ManualColsRange) => "",
            _ => setting.NewValue,
        };
}
