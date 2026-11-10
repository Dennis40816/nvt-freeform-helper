// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the six DXF edit panel inputs of the main view model: the start values, the change notifications,
/// the unsaved flag, the undo entry and a project file that stays identical, except for its save time,
/// after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class DxfEditPanelInputPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.DxfEditTargetLayerName), "L1", false, false),
        new(nameof(FreeformHelperViewModel.SelectedDxfEditRotationScopeOption), new FreeformHelperViewModel().DxfEditRotationScopeOptions[1], false, false),
        new(nameof(FreeformHelperViewModel.DxfEditRotationLayerName), "L2", false, false),
        new(nameof(FreeformHelperViewModel.DxfEditNewLayerName), "N1", false, false),
        new(nameof(FreeformHelperViewModel.DxfEditRotationDegrees), 45m, false, false),
        new(nameof(FreeformHelperViewModel.IsDxfEditAdvancedPanelExpanded), true, false, false),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal("", vm.DxfEditTargetLayerName);
        Assert.Equal(vm.DxfEditRotationScopeOptions[0], vm.SelectedDxfEditRotationScopeOption);
        Assert.Equal("", vm.DxfEditRotationLayerName);
        Assert.Equal("", vm.DxfEditNewLayerName);
        Assert.Equal(90m, vm.DxfEditRotationDegrees);
        Assert.False(vm.IsDxfEditAdvancedPanelExpanded);
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

    // The project file does not store the DXF edit panel inputs, so the round trip keeps their start values.
    private static object RoundTripValue(Setting setting) =>
        setting.Name switch
        {
            nameof(FreeformHelperViewModel.DxfEditTargetLayerName) => "",
            nameof(FreeformHelperViewModel.SelectedDxfEditRotationScopeOption) => new FreeformHelperViewModel().DxfEditRotationScopeOptions[0],
            nameof(FreeformHelperViewModel.DxfEditRotationLayerName) => "",
            nameof(FreeformHelperViewModel.DxfEditNewLayerName) => "",
            nameof(FreeformHelperViewModel.DxfEditRotationDegrees) => 90m,
            nameof(FreeformHelperViewModel.IsDxfEditAdvancedPanelExpanded) => false,
            _ => setting.NewValue,
        };
}
