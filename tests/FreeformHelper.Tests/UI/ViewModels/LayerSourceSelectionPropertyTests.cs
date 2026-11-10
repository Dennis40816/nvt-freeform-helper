// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.ViewModelPropertyTestHarness;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the five layer source selection settings of the main view model: the start values, the change notifications,
/// the unsaved flag, the undo entry and a project file that stays identical, except for its save time,
/// after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class LayerSourceSelectionPropertyTests
{
    // MarksUnsaved is the value after the grid rebuild that the change starts has finished.
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.HasCadLayers), true, false, false),
        new(nameof(FreeformHelperViewModel.SelectedBoundLayerOption), new FreeformHelperViewModel.BoundLayerOption("LAYER_A", "LAYER_A"), false, true),
        new(nameof(FreeformHelperViewModel.BoundLayerOptionSummary), "changed summary", false, false),
        new(nameof(FreeformHelperViewModel.SelectedRegularSourceLayerOption), new FreeformHelperViewModel.RegularSourceLayerOption("LAYER_B", "LAYER_B"), false, false),
        new(nameof(FreeformHelperViewModel.DxfRegularSourceHint), "changed hint", false, false),
    ];

    public static TheoryData<int> SettingIndexes => new(Enumerable.Range(0, _settings.Length));

    [Fact]
    public void NewViewModel_HasTheLoadedDefaults()
    {
        var vm = new FreeformHelperViewModel();

        Assert.False(vm.HasCadLayers);
        Assert.Equal(new FreeformHelperViewModel.BoundLayerOption(null, "Auto (visible layers)"), vm.SelectedBoundLayerOption);
        Assert.Equal("Hidden bounds layer is still valid.", vm.BoundLayerOptionSummary);
        Assert.Equal(new FreeformHelperViewModel.RegularSourceLayerOption(null, "Select layer"), vm.SelectedRegularSourceLayerOption);
        Assert.Equal("", vm.DxfRegularSourceHint);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_NewValue_RaisesChangingBeforeChangedOnceAndKeepsTheOtherValues(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();

        AssertChangeNotifications(vm, setting.Name, setting.NewValue, _settings.Select(item => item.Name));
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
        // A bounds layer change marks the project unsaved after its background rebuild finishes.
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

    // The project file does not store the layer source selection, so the round trip keeps their start values.
    private static object RoundTripValue(Setting setting) =>
        setting.Name switch
        {
            nameof(FreeformHelperViewModel.HasCadLayers) => false,
            nameof(FreeformHelperViewModel.SelectedBoundLayerOption) => new FreeformHelperViewModel.BoundLayerOption(null, "Auto (visible layers)"),
            nameof(FreeformHelperViewModel.BoundLayerOptionSummary) => "Hidden bounds layer is still valid.",
            nameof(FreeformHelperViewModel.SelectedRegularSourceLayerOption) => new FreeformHelperViewModel.RegularSourceLayerOption(null, "Select layer"),
            nameof(FreeformHelperViewModel.DxfRegularSourceHint) => "",
            _ => setting.NewValue,
        };
}
