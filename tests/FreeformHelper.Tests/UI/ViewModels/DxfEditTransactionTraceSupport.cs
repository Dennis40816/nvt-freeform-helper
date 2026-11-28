// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.DxfEditTraceNames;

namespace FreeformHelper.Tests;

/// <summary>Property names that the edit transaction characterization tests record.</summary>
internal static class DxfEditTraceNames
{
    internal const string Hidden = nameof(FreeformHelperViewModel.HiddenDxfEditBadgeTooltip);
    internal const string Combined = nameof(FreeformHelperViewModel.CombinedDxfEditBadgeTooltip);
    internal const string Duplicate = nameof(FreeformHelperViewModel.DuplicateDxfEditBadgeTooltip);
    internal const string Moved = nameof(FreeformHelperViewModel.MovedDxfEditBadgeTooltip);
    internal const string Rotated = nameof(FreeformHelperViewModel.RotatedDxfEditBadgeTooltip);
    internal const string Selection = nameof(FreeformHelperViewModel.SelectedCadPadIds);
    internal const string Pads = nameof(FreeformHelperViewModel.CadPads);
    internal const string Dirty = nameof(FreeformHelperViewModel.HasUnsavedChanges);
    internal const string Undo = nameof(FreeformHelperViewModel.CanUndo);
    internal const string Aggregate = nameof(FreeformHelperViewModel.HasAnyCadEdits);
    internal const string Status = nameof(FreeformHelperViewModel.StatusText);
}

internal sealed record DxfEditEvent(string Name, bool Changing, object Value);

internal sealed class DxfEditTrace : IDisposable
{
    private readonly FreeformHelperViewModel _vm;
    private readonly string? _statusPrefix;
    public List<DxfEditEvent> Events { get; } = [];

    public DxfEditTrace(FreeformHelperViewModel vm, string? statusPrefix = null)
    {
        _vm = vm;
        _statusPrefix = statusPrefix;
        vm.PropertyChanging += OnChanging;
        vm.PropertyChanged += OnChanged;
    }

    private void OnChanging(object? sender, PropertyChangingEventArgs args) => Record(args.PropertyName, true);
    private void OnChanged(object? sender, PropertyChangedEventArgs args) => Record(args.PropertyName, false);

    private void Record(string? name, bool changing)
    {
        object? value = name switch
        {
            nameof(_vm.DeletedCadPadCount) => _vm.DeletedCadPadCount,
            nameof(_vm.CombinedCadPadCount) => _vm.CombinedCadPadCount,
            nameof(_vm.RelayeredCadPadCount) => _vm.RelayeredCadPadCount,
            nameof(_vm.RotatedCadPadCount) => _vm.RotatedCadPadCount,
            nameof(_vm.HasDeletedCadPads) => _vm.HasDeletedCadPads,
            nameof(_vm.HasCombinedCadPads) => _vm.HasCombinedCadPads,
            nameof(_vm.HasRelayeredCadPads) => _vm.HasRelayeredCadPads,
            nameof(_vm.HasRotatedCadPads) => _vm.HasRotatedCadPads,
            Aggregate => _vm.HasAnyCadEdits,
            Hidden or Combined or Duplicate or Moved or Rotated => _vm.HasAnyCadEdits,
            Selection => _vm.SelectedCadPadIds.Count,
            Pads => _vm.CadPads.Count,
            Dirty => _vm.HasUnsavedChanges,
            Undo => _vm.CanUndo,
            Status when _statusPrefix is not null && _vm.StatusText.StartsWith(_statusPrefix, StringComparison.Ordinal) =>
                _vm.StatusText,
            _ => null,
        };
        if (value is not null) Events.Add(new DxfEditEvent(name!, changing, value));
    }

    public void Dispose()
    {
        _vm.PropertyChanging -= OnChanging;
        _vm.PropertyChanged -= OnChanged;
    }
}

/// <summary>Reads, writes and expected notifications of the edit panel inputs.</summary>
internal static class DxfEditInputs
{
    public static (Func<object> Read, Action Write) Input(FreeformHelperViewModel vm, string name) => name switch
    {
        nameof(vm.DxfEditTargetLayerName) => (() => vm.DxfEditTargetLayerName, () => vm.DxfEditTargetLayerName = "beta"),
        nameof(vm.DxfEditRotationLayerName) => (() => vm.DxfEditRotationLayerName, () => vm.DxfEditRotationLayerName = "beta"),
        nameof(vm.SelectedDxfEditRotationScopeOption) =>
            (() => vm.SelectedDxfEditRotationScopeOption, () => vm.SelectedDxfEditRotationScopeOption = vm.DxfEditRotationScopeOptions[1]),
        nameof(vm.DxfEditNewLayerName) => (() => vm.DxfEditNewLayerName, () => vm.DxfEditNewLayerName = "new"),
        nameof(vm.DxfEditRotationDegrees) => (() => vm.DxfEditRotationDegrees, () => vm.DxfEditRotationDegrees = 0m),
        nameof(vm.IsDxfEditAdvancedPanelExpanded) =>
            (() => vm.IsDxfEditAdvancedPanelExpanded, () => vm.IsDxfEditAdvancedPanelExpanded = true),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static IEnumerable<(string? Name, bool Changing)> InputNotifications(string name)
    {
        yield return (name, true);
        var hintAtEnd = name is nameof(FreeformHelperViewModel.DxfEditRotationLayerName) or nameof(FreeformHelperViewModel.SelectedDxfEditRotationScopeOption);
        if (name != nameof(FreeformHelperViewModel.IsDxfEditAdvancedPanelExpanded))
        {
            if (name == nameof(FreeformHelperViewModel.SelectedDxfEditRotationScopeOption))
                yield return (nameof(FreeformHelperViewModel.IsDxfEditRotationLayerScope), false);
            string[] callbacks =
            [
                nameof(FreeformHelperViewModel.HasSelectedCadPadsForDxfEdit),
                nameof(FreeformHelperViewModel.HasSelectedCombinedCadPadsForDxfEdit),
                nameof(FreeformHelperViewModel.HasDetectedDuplicateCadPads),
                nameof(FreeformHelperViewModel.CanMoveSelectedCadPadsToLayer),
                nameof(FreeformHelperViewModel.CanCreateLayerAndMoveSelectedCadPads),
                nameof(FreeformHelperViewModel.CanRotateSelectedCadPads),
                nameof(FreeformHelperViewModel.DxfRotationScopeHint),
                nameof(FreeformHelperViewModel.CanOffsetSelectedCadOutputFwDiffIndices),
                nameof(FreeformHelperViewModel.IsDxfEditRotationLayerScope),
                nameof(FreeformHelperViewModel.DxfEditSelectionSummary),
                nameof(FreeformHelperViewModel.Step4DiffShiftSelectionSummary),
            ];
            foreach (var callback in callbacks) yield return (callback, false);
            // The scope hint comes before the offset callback for every input. Two inputs publish it again after the final changed.
        }
        yield return (name, false);
        if (hintAtEnd)
            yield return (nameof(FreeformHelperViewModel.DxfRotationScopeHint), false);
    }
}

/// <summary>Assertions over the recorded property notifications.</summary>
internal static class DxfEditTraceAsserts
{
    public static void AssertFlagEvents(DxfEditTrace trace, string name, bool before, bool after)
    {
        var events = trace.Events.Where(item => item.Name == name).ToArray();
        if (before == after)
        {
            Assert.Empty(events);
            return;
        }
        Assert.Equal(new[] { (true, after), (false, after) },
            events.Select(item => (item.Changing, Assert.IsType<bool>(item.Value))));
    }

    public static void AssertCountEvents(DxfEditTrace trace, string name, int before, int after, bool stored)
    {
        var events = trace.Events.Where(item => item.Name == name).ToArray();
        if (before == after)
        {
            Assert.Empty(events);
            return;
        }
        // Computed counts already expose the mutation during Changing; stored counters still expose the previous value.
        Assert.Equal(new[] { (true, stored ? before : after), (false, after) },
            events.Select(item => (item.Changing, Assert.IsType<int>(item.Value))));
    }
}
