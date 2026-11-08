// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UndoHistoryFeedbackBehaviorTests
{
    private static readonly bool[] _enabledThenDisabled = [true, false];

    [AvaloniaFact]
    public void Undo_AfterTwoDisplayEdits_ReportsEachActionAndStopsNotifyingWhenHistoryIsEmpty()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        var originalShowCad = vm.ShowCad;
        var originalShowRegular = vm.ShowRegular;
        vm.ShowCad = !originalShowCad;
        vm.ShowRegular = !originalShowRegular;
        var availabilityChanges = new List<bool>();
        var commandAvailabilityChanges = new List<bool>();
        vm.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(vm.CanUndo))
            {
                availabilityChanges.Add(vm.CanUndo);
            }
        };
        vm.UndoCommand.CanExecuteChanged += (_, _) => commandAvailabilityChanges.Add(vm.UndoCommand.CanExecute(null));

        vm.UndoCommand.Execute(null);

        Assert.Equal(originalShowRegular, vm.ShowRegular);
        Assert.Equal(!originalShowCad, vm.ShowCad);
        Assert.Equal("Undo: Toggle regular grid", vm.StatusText);
        Assert.True(vm.CanUndo);
        Assert.True(vm.UndoCommand.CanExecute(null));

        vm.UndoCommand.Execute(null);

        Assert.Equal(originalShowCad, vm.ShowCad);
        Assert.Equal(originalShowRegular, vm.ShowRegular);
        Assert.Equal("Undo: Toggle CAD layer", vm.StatusText);
        Assert.False(vm.CanUndo);
        Assert.False(vm.UndoCommand.CanExecute(null));
        Assert.Equal(_enabledThenDisabled, availabilityChanges);
        Assert.Equal(_enabledThenDisabled, commandAvailabilityChanges);

        vm.Undo();

        Assert.Equal("Undo: Toggle CAD layer", vm.StatusText);
        Assert.Equal(originalShowCad, vm.ShowCad);
        Assert.Equal(originalShowRegular, vm.ShowRegular);
        Assert.False(vm.CanUndo);
        Assert.Equal(_enabledThenDisabled, availabilityChanges);
        Assert.Equal(_enabledThenDisabled, commandAvailabilityChanges);
    }
}
