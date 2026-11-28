// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
[Trait("Category", "Characterization")]
public sealed class SettingsStateNotificationCharacterizationTests
{
    [Fact]
    public void AreaBucketChangePublishesUndoThenOldAndNewValuesBeforeDirty()
    {
        var vm = new FreeformHelperViewModel();
        var original = vm.MaxAreaBuckets;
        var trace = new List<(string Phase, string? Name, decimal Value, bool CanUndo, bool IsDirty)>();
        vm.PropertyChanging += (sender, args) =>
        {
            if (args.PropertyName is nameof(vm.MaxAreaBuckets) or nameof(vm.HasUnsavedChanges))
            {
                Assert.Same(vm, sender);
                trace.Add(("changing", args.PropertyName, vm.MaxAreaBuckets, vm.CanUndo, vm.HasUnsavedChanges));
            }
        };
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName is nameof(vm.MaxAreaBuckets) or nameof(vm.CanUndo) or nameof(vm.HasUnsavedChanges))
            {
                Assert.Same(vm, sender);
                trace.Add(("changed", args.PropertyName, vm.MaxAreaBuckets, vm.CanUndo, vm.HasUnsavedChanges));
            }
        };

        vm.MaxAreaBuckets = original + 1m;

        Assert.Equal(
        [
            ("changed", nameof(vm.CanUndo), original, true, false),
            ("changing", nameof(vm.MaxAreaBuckets), original, true, false),
            ("changed", nameof(vm.MaxAreaBuckets), original + 1m, true, false),
            ("changing", nameof(vm.HasUnsavedChanges), original + 1m, true, false),
            ("changed", nameof(vm.HasUnsavedChanges), original + 1m, true, true),
        ], trace);
    }

    [Fact]
    public void PreferredLayerTrimReentersBeforePublishingTheNormalizedValue()
    {
        var vm = new FreeformHelperViewModel();
        var original = vm.CoordinatePreferredAaOutlineLayerName;
        var trace = new List<(string Phase, string Value, bool IsDirty)>();
        vm.PropertyChanging += (sender, args) =>
        {
            if (args.PropertyName == nameof(vm.CoordinatePreferredAaOutlineLayerName))
            {
                Assert.Same(vm, sender);
                trace.Add(("changing", vm.CoordinatePreferredAaOutlineLayerName, vm.HasUnsavedChanges));
            }
        };
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(vm.CoordinatePreferredAaOutlineLayerName))
            {
                Assert.Same(vm, sender);
                trace.Add(("changed", vm.CoordinatePreferredAaOutlineLayerName, vm.HasUnsavedChanges));
            }
        };

        vm.CoordinatePreferredAaOutlineLayerName = "  AA ";

        Assert.Equal(
        [
            ("changing", original, false),
            ("changing", "  AA ", false),
            ("changed", "AA", false),
            ("changed", "AA", true),
        ], trace);
        Assert.False(vm.CanUndo);
    }
}
