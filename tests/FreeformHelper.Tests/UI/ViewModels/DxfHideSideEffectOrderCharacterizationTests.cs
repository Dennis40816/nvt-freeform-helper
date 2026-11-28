// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Interaction;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfHideSideEffectOrderCharacterizationTests
{
    private static readonly string[] _expectedOrder = ["selection", "filter", "rebuild", "undo"];

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hide_ClearsSelectionBeforeFilteringAndRebuildingThenRegistersUndoAsync(bool recalcBounds)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = await DxfCharacterizationWorkspace.OpenAsync(files, recalcBounds);
        var hiddenId = Assert.Single(vm.CadPads, static pad => pad.Layer == "alpha").Id;
        var remainingId = Assert.Single(vm.CadPads, static pad => pad.Layer == "beta").Id;
        await SelectCadPadsAsync(vm, [hiddenId]);
        Assert.False(vm.CanUndo);
        var phases = new List<string>();
        var observations = new List<(int[] Selected, int[] Visible, bool CanUndo)>();
        void Record(string phase)
        {
            if (phases.Contains(phase, StringComparer.Ordinal)) return;
            phases.Add(phase);
            observations.Add((vm.SelectedCadPadIds.ToArray(), vm.CadPads.Select(static pad => pad.Id).Order().ToArray(), vm.CanUndo));
        }

        void SelectionChanged(object? _, SelectionChangedEventArgs __) => Record("selection");
        void PropertyChanged(object? _, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(vm.CadPads)) Record("filter");
            if (args.PropertyName == nameof(vm.IsModalLoadingSpinnerVisible) && vm.IsModalLoadingSpinnerVisible) Record("rebuild");
            if (args.PropertyName == nameof(vm.CanUndo)) Record("undo");
        }

        var originalIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        vm.InteractionState.SelectionChanged += SelectionChanged;
        vm.PropertyChanged += PropertyChanged;
        try
        {
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            vm.InteractionState.SelectionChanged -= SelectionChanged;
            vm.PropertyChanged -= PropertyChanged;
        }

        Assert.Equal(_expectedOrder, phases);
        Assert.All(observations, static observation => Assert.Empty(observation.Selected));
        Assert.Equal(originalIds, observations[0].Visible);
        Assert.False(observations[0].CanUndo);
        Assert.Equal(remainingId, Assert.Single(observations[1].Visible));
        Assert.False(observations[1].CanUndo);
        Assert.Equal(remainingId, Assert.Single(observations[2].Visible));
        Assert.False(observations[2].CanUndo);
        Assert.Equal(remainingId, Assert.Single(observations[3].Visible));
        Assert.True(observations[3].CanUndo);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }
}
