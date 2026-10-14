// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the five layer source selection settings of the main view model: the start values, the change notifications,
/// the unsaved flag, the undo entry and a project file that stays identical, except for its save time,
/// after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class LayerSourceSelectionPropertyTests
{
    private sealed record Setting(string Name, object NewValue, bool HasUndo, bool MarksUnsaved);

    private static readonly Setting[] _settings =
    [
        new(nameof(FreeformHelperViewModel.HasCadLayers), true, false, false),
        new(nameof(FreeformHelperViewModel.SelectedBoundLayerOption), new FreeformHelperViewModel.BoundLayerOption("LAYER_A", "LAYER_A"), false, false),
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
        var before = ReadAll(vm);
        var raised = new ConcurrentQueue<string>();
        vm.PropertyChanging += (_, args) => raised.Enqueue("changing:" + args.PropertyName);
        vm.PropertyChanged += (_, args) => raised.Enqueue("changed:" + args.PropertyName);

        Write(vm, setting.Name, setting.NewValue);

        var sequence = raised.ToList();
        Assert.Equal(1, sequence.Count(entry => entry == "changing:" + setting.Name));
        Assert.Equal(1, sequence.Count(entry => entry == "changed:" + setting.Name));

        Assert.True(sequence.IndexOf("changing:" + setting.Name) < sequence.IndexOf("changed:" + setting.Name));
        var after = ReadAll(vm);
        foreach (var other in _settings.Select(item => item.Name).Where(other => other != setting.Name))
        {
            Assert.Equal(before[other], after[other]);
        }

        Assert.Equal(setting.NewValue, after[setting.Name]);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_SameValue_RaisesNothing(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        var raised = new ConcurrentQueue<string?>();
        vm.PropertyChanging += (_, args) => raised.Enqueue(args.PropertyName);
        vm.PropertyChanged += (_, args) => raised.Enqueue(args.PropertyName);

        Write(vm, setting.Name, Read(vm, setting.Name));

        Assert.Empty(raised);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_NewValue_SetsTheUnsavedFlagAsBefore(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);

        Write(vm, setting.Name, setting.NewValue);

        Assert.Equal(setting.MarksUnsaved, vm.HasUnsavedChanges);
    }

    [Theory]
    [MemberData(nameof(SettingIndexes))]
    public void Set_NewValue_AddsAnUndoEntryOnlyWhereTheSettingHasUndo(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        var start = Read(vm, setting.Name);
        Assert.False(vm.CanUndo);

        Write(vm, setting.Name, setting.NewValue);

        Assert.Equal(setting.HasUndo, vm.CanUndo);
        if (!setting.HasUndo)
        {
            return;
        }

        vm.Undo();

        Assert.Equal(start, Read(vm, setting.Name));
    }

    [AvaloniaFact]
    public async Task SaveLoadSave_ChangedValues_WritesTheSameProjectFileExceptTheSaveTime()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(saved =>
        {
            foreach (var setting in _settings)
            {
                Write(saved, setting.Name, RoundTripValue(setting));
            }
        });
        var first = await ReadWithoutSavedAtAsync(files.PathFor("project.json"));
        var vm = files.CreateViewModel();
        var secondPath = files.PathFor("project-second.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(secondPath);

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));
        Assert.True(result.IsLoaded);
        var mismatches = _settings
            .Where(setting => !Equals(RoundTripValue(setting), Read(vm, setting.Name)))
            .Select(setting => setting.Name + "=" + Read(vm, setting.Name))
            .ToList();
        Assert.Equal("", string.Join(",", mismatches));
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(await vm.SaveProjectAsync());

        var second = await ReadWithoutSavedAtAsync(secondPath);
        Assert.Equal(first, second);
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

    // The save time is the only member that differs between two saves of the same state.
    private static async Task<string> ReadWithoutSavedAtAsync(string projectFilePath)
    {
        await using var stream = File.OpenRead(projectFilePath);
        var root = (await JsonNode.ParseAsync(stream))!.AsObject();
        var key = root.Select(member => member.Key).Single(name => string.Equals(name, "SavedAt", StringComparison.OrdinalIgnoreCase));
        root.Remove(key);
        return root.ToJsonString();
    }

    private static Dictionary<string, object> ReadAll(FreeformHelperViewModel vm) =>
        _settings.ToDictionary(setting => setting.Name, setting => Read(vm, setting.Name));

    private static object Read(FreeformHelperViewModel vm, string name) =>
        typeof(FreeformHelperViewModel).GetProperty(name)!.GetValue(vm)!;

    private static void Write(FreeformHelperViewModel vm, string name, object value) =>
        typeof(FreeformHelperViewModel).GetProperty(name)!.SetValue(vm, value);
}
