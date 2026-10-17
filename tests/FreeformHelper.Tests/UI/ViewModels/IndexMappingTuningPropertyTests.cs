// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins the seven index mapping number properties of the main view model: the start values, one
/// change notification (and one changing notification) per real change, no notification for the
/// same value, the unsaved flag, and a project file that stays identical, except for its save time, after a save, load and save.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class IndexMappingTuningPropertyTests
{
    private static readonly string[] _names =
    [
        nameof(FreeformHelperViewModel.MappingWeightIou),
        nameof(FreeformHelperViewModel.MappingWeightCentroidDistance),
        nameof(FreeformHelperViewModel.MappingWeightAreaRatio),
        nameof(FreeformHelperViewModel.MappingLowConfidenceThreshold),
        nameof(FreeformHelperViewModel.MappingAmbiguousMargin),
        nameof(FreeformHelperViewModel.MappingCandidatePaddingCells),
        nameof(FreeformHelperViewModel.MappingCandidateNumber),
    ];

    public static TheoryData<string> PropertyNames => new(_names);

    [Fact]
    public void NewViewModel_HasTheLoadedDefaultsOfTheProjectSettings()
    {
        var vm = new FreeformHelperViewModel();
        var defaults = new FreeformHelper.Application.Settings.IndexMappingSettings();

        Assert.Equal((decimal)defaults.WeightIou, vm.MappingWeightIou);
        Assert.Equal((decimal)defaults.WeightCentroidDistance, vm.MappingWeightCentroidDistance);
        Assert.Equal((decimal)defaults.WeightAreaRatio, vm.MappingWeightAreaRatio);
        Assert.Equal((decimal)defaults.LowConfidenceThreshold, vm.MappingLowConfidenceThreshold);
        Assert.Equal((decimal)defaults.AmbiguousMargin, vm.MappingAmbiguousMargin);
        Assert.Equal((decimal)defaults.CandidatePaddingCells, vm.MappingCandidatePaddingCells);
        Assert.Equal((decimal)defaults.GetEffectiveCandidateNumber(), vm.MappingCandidateNumber);
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void Set_NewValue_RaisesChangingAndChangedOnceAndKeepsTheOthers(string name)
    {
        var vm = new FreeformHelperViewModel();
        var before = ReadAll(vm);
        var raised = new ConcurrentQueue<string>();
        vm.PropertyChanging += (_, args) => raised.Enqueue("changing:" + args.PropertyName);
        vm.PropertyChanged += (_, args) => raised.Enqueue("changed:" + args.PropertyName);

        Write(vm, name, Read(vm, name) + 0.125m);

        Assert.Equal(1, raised.Count(entry => entry == "changing:" + name));
        Assert.Equal(1, raised.Count(entry => entry == "changed:" + name));
        Assert.True(raised.ToArray().ToList().IndexOf("changing:" + name) < raised.ToArray().ToList().IndexOf("changed:" + name));
        var after = ReadAll(vm);
        foreach (var other in _names.Where(other => other != name))
        {
            Assert.Equal(before[other], after[other]);
        }

        Assert.Equal(before[name] + 0.125m, after[name]);
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void Set_SameValue_RaisesNothing(string name)
    {
        var vm = new FreeformHelperViewModel();
        var raised = new ConcurrentQueue<string?>();
        vm.PropertyChanging += (_, args) => raised.Enqueue(args.PropertyName);
        vm.PropertyChanged += (_, args) => raised.Enqueue(args.PropertyName);

        Write(vm, name, Read(vm, name));

        Assert.Empty(raised);
    }

    [Theory]
    [MemberData(nameof(PropertyNames))]
    public void Set_NewValue_MarksTheProjectAsChanged(string name)
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);

        Write(vm, name, Read(vm, name) + 1m);

        Assert.True(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task SaveLoadSave_ChangedValues_WritesTheSameProjectFileExceptTheSaveTime()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(saved =>
        {
            saved.MappingWeightIou = 0.41m;
            saved.MappingWeightCentroidDistance = 0.32m;
            saved.MappingWeightAreaRatio = 0.27m;
            saved.MappingLowConfidenceThreshold = 0.55m;
            saved.MappingAmbiguousMargin = 0.07m;
            saved.MappingCandidateNumber = 17m;
        });
        var first = await ReadWithoutSavedAtAsync(files.PathFor("project.json"));
        var vm = files.CreateViewModel();
        var secondPath = files.PathFor("project-second.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(secondPath);

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));
        Assert.True(result.IsLoaded);
        Assert.Equal(0.41m, vm.MappingWeightIou);
        Assert.Equal(17m, vm.MappingCandidateNumber);
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(await vm.SaveProjectAsync());

        var second = await ReadWithoutSavedAtAsync(secondPath);
        Assert.Equal(first, second);
    }

    // The save time is the only member that differs between two saves of the same state.
    private static async Task<string> ReadWithoutSavedAtAsync(string projectFilePath)
    {
        await using var stream = File.OpenRead(projectFilePath);
        var root = (await JsonNode.ParseAsync(stream))!.AsObject();
        var key = root.Select(member => member.Key).Single(name => string.Equals(name, "SavedAt", StringComparison.OrdinalIgnoreCase));
        root.Remove(key);
        return root.ToJsonString();
    }

    private static Dictionary<string, decimal> ReadAll(FreeformHelperViewModel vm) =>
        _names.ToDictionary(name => name, name => Read(vm, name));

    private static decimal Read(FreeformHelperViewModel vm, string name) => name switch
    {
        nameof(FreeformHelperViewModel.MappingWeightIou) => vm.MappingWeightIou,
        nameof(FreeformHelperViewModel.MappingWeightCentroidDistance) => vm.MappingWeightCentroidDistance,
        nameof(FreeformHelperViewModel.MappingWeightAreaRatio) => vm.MappingWeightAreaRatio,
        nameof(FreeformHelperViewModel.MappingLowConfidenceThreshold) => vm.MappingLowConfidenceThreshold,
        nameof(FreeformHelperViewModel.MappingAmbiguousMargin) => vm.MappingAmbiguousMargin,
        nameof(FreeformHelperViewModel.MappingCandidatePaddingCells) => vm.MappingCandidatePaddingCells,
        nameof(FreeformHelperViewModel.MappingCandidateNumber) => vm.MappingCandidateNumber,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static void Write(FreeformHelperViewModel vm, string name, decimal value)
    {
        switch (name)
        {
            case nameof(FreeformHelperViewModel.MappingWeightIou):
                vm.MappingWeightIou = value;
                break;
            case nameof(FreeformHelperViewModel.MappingWeightCentroidDistance):
                vm.MappingWeightCentroidDistance = value;
                break;
            case nameof(FreeformHelperViewModel.MappingWeightAreaRatio):
                vm.MappingWeightAreaRatio = value;
                break;
            case nameof(FreeformHelperViewModel.MappingLowConfidenceThreshold):
                vm.MappingLowConfidenceThreshold = value;
                break;
            case nameof(FreeformHelperViewModel.MappingAmbiguousMargin):
                vm.MappingAmbiguousMargin = value;
                break;
            case nameof(FreeformHelperViewModel.MappingCandidatePaddingCells):
                vm.MappingCandidatePaddingCells = value;
                break;
            case nameof(FreeformHelperViewModel.MappingCandidateNumber):
                vm.MappingCandidateNumber = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
    }
}
