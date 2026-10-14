// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

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
        if (_recalculated.Contains(setting.Name))
        {
            // The grid recalculates these settings, so the setting can change more than once.
            Assert.Contains("changing:" + setting.Name, sequence);
            Assert.Contains("changed:" + setting.Name, sequence);
        }
        else
        {
            Assert.Equal(1, sequence.Count(entry => entry == "changing:" + setting.Name));
            Assert.Equal(1, sequence.Count(entry => entry == "changed:" + setting.Name));
        }

        Assert.True(sequence.IndexOf("changing:" + setting.Name) < sequence.IndexOf("changed:" + setting.Name));
        var after = ReadAll(vm);
        foreach (var other in _settings.Select(item => item.Name).Where(other => other != setting.Name && !_recalculated.Contains(other)))
        {
            Assert.Equal(before[other], after[other]);
        }

        // The listed settings are recalculated by the grid after a change, so the new value does not stay.
        if (!_recalculated.Contains(setting.Name))
        {
            Assert.Equal(setting.NewValue, after[setting.Name]);
        }
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
    public void Set_NewValue_MarksTheProjectAsChanged(int index)
    {
        var setting = _settings[index];
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);

        Write(vm, setting.Name, setting.NewValue);

        Assert.True(vm.HasUnsavedChanges);
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
            .Where(setting => !_recalculated.Contains(setting.Name) && !Equals(RoundTripValue(setting), Read(vm, setting.Name)))
            .Select(setting => setting.Name + "=" + Read(vm, setting.Name))
            .ToList();
        Assert.Equal("", string.Join(",", mismatches));
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(await vm.SaveProjectAsync());

        var second = await ReadWithoutSavedAtAsync(secondPath);
        Assert.Equal(first, second);
    }

    private static object RoundTripValue(Setting setting) => setting.NewValue;

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
