// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using FreeformHelper.UI;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CanvasStrokeFillStateCharacterizationTests
{
    public static TheoryData<string> PropertyNames => new()
    {
        nameof(FreeformHelperViewModel.CadLineWidth),
        nameof(FreeformHelperViewModel.CadLineOpacity),
        nameof(FreeformHelperViewModel.CadFillOpacity),
        nameof(FreeformHelperViewModel.RegularLineWidth),
        nameof(FreeformHelperViewModel.HighlightStrokeWidthAdjust),
        nameof(FreeformHelperViewModel.RegularLineOpacity),
        nameof(FreeformHelperViewModel.RegularFillOpacity),
        nameof(FreeformHelperViewModel.RegularSelectedFillOpacity),
    };

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [MemberData(nameof(PropertyNames))]
    public void AssignmentPublishesOldThenNewValueThroughRoot(string propertyName)
    {
        using var scope = new ViewModelScope();
        var vm = scope.ViewModel;
        var preference = Access(vm, propertyName);
        using var observation = new Observation(vm, propertyName, preference.Get);
        Assert.Equal(preference.Default, preference.Get());

        preference.Set(0.6m);

        Assert.Equal(ExpectedAssignment(propertyName, preference, 0.6m), observation.Entries);
        Assert.Equal(preference.TracksUndo, vm.CanUndo);
        Assert.Equal(preference.MarksDirty, vm.HasUnsavedChanges);
        Assert.Equal(preference.MarksDirty, vm.AppGeneralPersistenceForTests.PersistTimer?.IsEnabled ?? false);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [MemberData(nameof(PropertyNames))]
    public void EqualValuePublishesNothing(string propertyName)
    {
        using var scope = new ViewModelScope();
        var vm = scope.ViewModel;
        var preference = Access(vm, propertyName);
        using var observation = new Observation(vm, propertyName, preference.Get);

        preference.Set(preference.Get());

        Assert.Empty(observation.Entries);
        Assert.False(vm.CanUndo);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.AppGeneralPersistenceForTests.PersistTimer?.IsEnabled ?? false);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.CadLineWidth), -3)]
    [InlineData(nameof(FreeformHelperViewModel.RegularLineWidth), -3)]
    [InlineData(nameof(FreeformHelperViewModel.CadLineOpacity), 2.5)]
    [InlineData(nameof(FreeformHelperViewModel.CadFillOpacity), -0.5)]
    [InlineData(nameof(FreeformHelperViewModel.RegularLineOpacity), -0.5)]
    [InlineData(nameof(FreeformHelperViewModel.RegularFillOpacity), 2.5)]
    public void OrdinarySetterKeepsRawOutOfRangeValue(string propertyName, double requested)
    {
        using var scope = new ViewModelScope();
        var preference = Access(scope.ViewModel, propertyName);
        using var observation = new Observation(scope.ViewModel, propertyName, preference.Get);

        preference.Set((decimal)requested);

        Assert.Equal((decimal)requested, preference.Get());
        Assert.Equal(ExpectedAssignment(propertyName, preference, (decimal)requested), observation.Entries);
        Assert.Equal(preference.TracksUndo, scope.ViewModel.CanUndo);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.HighlightStrokeWidthAdjust), -3, -2)]
    [InlineData(nameof(FreeformHelperViewModel.HighlightStrokeWidthAdjust), 5, 4)]
    [InlineData(nameof(FreeformHelperViewModel.RegularSelectedFillOpacity), -0.5, 0)]
    [InlineData(nameof(FreeformHelperViewModel.RegularSelectedFillOpacity), 1.5, 1)]
    public void ClampReentersAfterRequestedValueIsStored(string propertyName, double requested, double corrected)
    {
        using var scope = new ViewModelScope();
        var preference = Access(scope.ViewModel, propertyName);
        using var observation = new Observation(scope.ViewModel, propertyName, preference.Get);

        preference.Set((decimal)requested);

        var expected = new List<Notification>
        {
            new("changed", nameof(FreeformHelperViewModel.CanUndo), preference.Default),
            new("changing", propertyName, preference.Default),
            new("changing", propertyName, (decimal)requested),
            new("changed", propertyName, (decimal)corrected),
        };
        AppendDirtyNotifications(expected, preference, (decimal)corrected);
        expected.Add(new("changed", propertyName, (decimal)corrected));
        Assert.Equal(expected, observation.Entries);
        Assert.Equal((decimal)corrected, preference.Get());
        Assert.True(scope.ViewModel.CanUndo);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.CadLineWidth))]
    [InlineData(nameof(FreeformHelperViewModel.RegularLineWidth))]
    [InlineData(nameof(FreeformHelperViewModel.HighlightStrokeWidthAdjust))]
    [InlineData(nameof(FreeformHelperViewModel.RegularSelectedFillOpacity))]
    public void OneUndoRestoresValueBeforeRequestIncludingClamp(string propertyName)
    {
        using var scope = new ViewModelScope();
        var vm = scope.ViewModel;
        var preference = Access(vm, propertyName);
        preference.Set(9m);
        var beforeUndo = preference.Get();
        using var observation = new Observation(vm, propertyName, preference.Get);

        vm.Undo();

        Assert.Equal(preference.Default, preference.Get());
        Assert.False(vm.CanUndo);
        Assert.Equal(new Notification[]
        {
            new("changing", propertyName, beforeUndo),
            new("changed", propertyName, preference.Default),
            new("changed", nameof(vm.CanUndo), preference.Default),
        }, observation.Entries);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [MemberData(nameof(PropertyNames))]
    public async Task LoadingKeepsRawValueAndNotificationsWithoutSideEffectsAsync(string propertyName)
    {
        using var scope = new ViewModelScope();
        await scope.Files.SaveAsync(savedVm => savedVm.ShowCad = false);
        var vm = scope.ViewModel;
        var preference = Access(vm, propertyName);
        Notification[] trace = [];
        using var edit = LoadingModeTestScope.OnNextChange(vm, nameof(vm.ShowCad), () =>
        {
            using var observation = new Observation(vm, propertyName, preference.Get);
            preference.Set(-3m);
            trace = observation.Entries.ToArray();
            Assert.Equal(-3m, preference.Get());
            Assert.False(vm.CanUndo);
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.AppGeneralPersistenceForTests.PersistTimer?.IsEnabled ?? false);
        });

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.True(edit.WasInvoked);
        Assert.Equal(new Notification[]
        {
            new("changing", propertyName, preference.Default),
            new("changed", propertyName, -3m),
        }, trace);
        Assert.False(vm.CanUndo);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.CadLineOpacity))]
    [InlineData(nameof(FreeformHelperViewModel.CadFillOpacity))]
    [InlineData(nameof(FreeformHelperViewModel.RegularLineOpacity))]
    [InlineData(nameof(FreeformHelperViewModel.RegularFillOpacity))]
    public async Task LoadedProjectDefersPreferenceWriteUntilSaveAsync(string propertyName)
    {
        using var scope = new ViewModelScope();
        await scope.Files.SaveAsync();
        var vm = scope.ViewModel;
        await vm.LoadProjectCommand.ExecuteAsync(null);
        var preference = Access(vm, propertyName);

        preference.Set(0.6m);

        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.AppGeneralPersistenceForTests.HasDeferredChanges);
        Assert.False(vm.AppGeneralPersistenceForTests.PersistTimer?.IsEnabled ?? false);
        Assert.True(await vm.SaveProjectAsync());
        Assert.False(vm.AppGeneralPersistenceForTests.HasDeferredChanges);
    }

    private static List<Notification> ExpectedAssignment(string propertyName, Preference preference, decimal value)
    {
        var expected = new List<Notification>();
        if (preference.TracksUndo)
        {
            expected.Add(new("changed", nameof(FreeformHelperViewModel.CanUndo), preference.Default));
        }
        expected.Add(new("changing", propertyName, preference.Default));
        if (preference.Invalidates)
        {
            expected.Add(new("invalidate", propertyName, value));
        }
        expected.Add(new("changed", propertyName, value));
        AppendDirtyNotifications(expected, preference, value);
        return expected;
    }

    private static void AppendDirtyNotifications(List<Notification> expected, Preference preference, decimal value)
    {
        if (preference.MarksDirty)
        {
            expected.Add(new("changing", nameof(FreeformHelperViewModel.HasUnsavedChanges), value));
            expected.Add(new("changed", nameof(FreeformHelperViewModel.HasUnsavedChanges), value));
        }
    }

    private static Preference Access(FreeformHelperViewModel vm, string name) => name switch
    {
        nameof(vm.CadLineWidth) => new(() => vm.CadLineWidth, v => vm.CadLineWidth = v, 0.2m, true, false, false),
        nameof(vm.CadLineOpacity) => new(() => vm.CadLineOpacity, v => vm.CadLineOpacity = v, 1m, false, true, true),
        nameof(vm.CadFillOpacity) => new(() => vm.CadFillOpacity, v => vm.CadFillOpacity = v, 1m, false, true, true),
        nameof(vm.RegularLineWidth) => new(() => vm.RegularLineWidth, v => vm.RegularLineWidth = v, 0.2m, true, false, false),
        nameof(vm.HighlightStrokeWidthAdjust) => new(
            () => vm.HighlightStrokeWidthAdjust, v => vm.HighlightStrokeWidthAdjust = v, -1m, true, false, true),
        nameof(vm.RegularLineOpacity) => new(() => vm.RegularLineOpacity, v => vm.RegularLineOpacity = v, 1m, false, true, true),
        nameof(vm.RegularFillOpacity) => new(() => vm.RegularFillOpacity, v => vm.RegularFillOpacity = v, 1m, false, true, true),
        nameof(vm.RegularSelectedFillOpacity) => new(
            () => vm.RegularSelectedFillOpacity, v => vm.RegularSelectedFillOpacity = v, 0.24m, true, false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private sealed record Preference(
        Func<decimal> Get, Action<decimal> Set, decimal Default, bool TracksUndo, bool Invalidates, bool MarksDirty);

    private sealed record Notification(string Phase, string? Name, decimal Value);

    private sealed class ViewModelScope : IDisposable
    {
        public LoadingModeTestScope Files { get; } = new();
        public FreeformHelperViewModel ViewModel { get; }

        public ViewModelScope()
        {
            ViewModel = Files.CreateViewModel();
            ViewModel.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }

        public void Dispose()
        {
            ViewModel.AppGeneralPersistenceForTests.PersistTimer?.Stop();
            Files.Dispose();
        }
    }

    private sealed class Observation : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;
        private readonly string _name;
        private readonly Func<decimal> _get;
        private readonly ICanvasHost? _previousHost;

        public List<Notification> Entries { get; } = [];

        public Observation(FreeformHelperViewModel vm, string name, Func<decimal> get)
        {
            _vm = vm;
            _name = name;
            _get = get;
            _previousHost = vm.CanvasHost;
            vm.CanvasHost = new InvalidationProbe(() => Entries.Add(new("invalidate", _name, _get())));
            vm.PropertyChanging += OnChanging;
            vm.PropertyChanged += OnChanged;
        }

        private void OnChanging(object? sender, PropertyChangingEventArgs e) => Record("changing", e.PropertyName);
        private void OnChanged(object? sender, PropertyChangedEventArgs e) => Record("changed", e.PropertyName);

        private void Record(string phase, string? name)
        {
            if (name == _name || name is nameof(_vm.CanUndo) or nameof(_vm.HasUnsavedChanges))
            {
                Entries.Add(new(phase, name, _get()));
            }
        }

        public void Dispose()
        {
            _vm.PropertyChanging -= OnChanging;
            _vm.PropertyChanged -= OnChanged;
            _vm.CanvasHost = _previousHost;
        }
    }

    private sealed class InvalidationProbe(Action invalidate) : ICanvasHost
    {
        public void Invalidate() => invalidate();
        public void FitToContent() { }
        public void ResetView() { }
        public void ClearSelection() { }
        public void SetSelection(IReadOnlyCollection<int> cadPadIds, IReadOnlyCollection<int> regularPadIndices) { }
        public void FocusSelection() { }
        public void FocusSelectionWithMinZoom(double minZoom) { }
        public PadCanvasSelectionPerfSnapshot GetSelectionPerfSnapshot() => default;
        public PadCanvasVisibleDrawPerfSnapshot GetVisibleDrawPerfSnapshot() => default;
    }
}
