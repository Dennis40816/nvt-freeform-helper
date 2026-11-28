// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
[Trait("Category", "Characterization")]
public sealed class FwAssignmentTransitionCharacterizationTests
{
    [Fact]
    public void StartClampPublishesNestedRootNotificationsAndAddsOnlyOneUndoEntry()
    {
        var vm = new FreeformHelperViewModel();
        var trace = new List<(string Event, decimal Value)>();
        vm.PropertyChanging += (sender, args) =>
        {
            if (args.PropertyName == nameof(vm.CadOutputFwDiffIndexStart))
            {
                Assert.Same(vm, sender);
                trace.Add(("changing", vm.CadOutputFwDiffIndexStart));
            }
        };
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(vm.CadOutputFwDiffIndexStart))
            {
                Assert.Same(vm, sender);
                trace.Add(("changed", vm.CadOutputFwDiffIndexStart));
            }
        };
        Assert.False(vm.CanUndo);

        vm.CadOutputFwDiffIndexStart = -5m;

        Assert.Equal([("changing", 0m), ("changing", -5m), ("changed", 0m), ("changed", 0m)], trace);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.Equal(0m, vm.CadOutputFwDiffIndexStart);
        Assert.False(vm.CanUndo);
    }
}
