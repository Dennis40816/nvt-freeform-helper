// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using System.Windows.Input;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkflowPanelStateCharacterizationTests
{
    public static TheoryData<string> PropertyNames => new()
    {
        nameof(FreeformHelperViewModel.IsConsoleExpanded),
        nameof(FreeformHelperViewModel.IsStep1Expanded),
        nameof(FreeformHelperViewModel.IsStep2Expanded),
        nameof(FreeformHelperViewModel.IsStep3Expanded),
        nameof(FreeformHelperViewModel.IsStep4Expanded),
        nameof(FreeformHelperViewModel.IsStep5Expanded),
        nameof(FreeformHelperViewModel.IsStep6Expanded),
    };

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void InitialExpansionKeepsConsoleAndStepOneOpen()
    {
        using var scope = new ViewModelScope();

        Assert.Equal("1100000", Flags(scope.ViewModel));
        Assert.False(scope.ViewModel.IsStep3ToFullDetailsExpanded);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.IsStep1Expanded), "1000010", "1100010", "1100000",
        "Export", "Match overlaps", nameof(FreeformHelperViewModel.IsStep5Expanded))]
    [InlineData(nameof(FreeformHelperViewModel.IsStep2Expanded), "1100000", "1110000", "1010000",
        "Match overlaps", "Detect freeform", nameof(FreeformHelperViewModel.IsStep1Expanded))]
    [InlineData(nameof(FreeformHelperViewModel.IsStep3Expanded), "1100000", "1101000", "1001000",
        "Match overlaps", "Export", nameof(FreeformHelperViewModel.IsStep1Expanded))]
    [InlineData(nameof(FreeformHelperViewModel.IsStep5Expanded), "1100000", "1100010", "1000010",
        "Match overlaps", "Export", nameof(FreeformHelperViewModel.IsStep1Expanded))]
    public async Task ExpansionSelectsWorkBeforeReentrantCollapseAndRootPublicationAsync(
        string propertyName, string before, string intermediate, string after,
        string oldAction, string newAction, string collapsedProperty)
    {
        using var scope = new ViewModelScope();
        var vm = await scope.OpenCadAsync();
        PrepareExpansion(vm, propertyName);
        using var observation = new Observation(vm);

        Access(vm, propertyName).Set(true);

        Assert.Equal(ExpansionTrace(vm, propertyName, collapsedProperty,
            before, intermediate, after, oldAction, newAction), observation.Entries);
        Assert.Equal(State(vm, after, newAction), Capture(vm));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExpansionClearsDiagnosticsAndPreviousWorkInNumericOrderAsync()
    {
        using var scope = new ViewModelScope();
        var vm = await scope.OpenCadAsync();
        vm.IsStep5Expanded = true;
        vm.IsStep4Expanded = true;
        vm.IsStep6Expanded = true;
        using var observation = new Observation(vm);

        vm.IsStep2Expanded = true;

        var expected = new List<Notification>
        {
            new("changing", nameof(vm.IsStep2Expanded), State(vm, "1000111", "Export")),
            new("changing", nameof(vm.IsStep4Expanded), State(vm, "1010111", "Detect freeform")),
            new("changed", nameof(vm.IsStep4Expanded), State(vm, "1010011", "Detect freeform")),
            new("changing", nameof(vm.IsStep5Expanded), State(vm, "1010011", "Detect freeform")),
            new("changed", nameof(vm.IsStep5Expanded), State(vm, "1010001", "Detect freeform")),
        };
        AppendPrimaryNotifications(expected, State(vm, "1010001", "Detect freeform"));
        expected.Add(new("changing", nameof(vm.IsStep6Expanded), State(vm, "1010001", "Detect freeform")));
        expected.Add(new("changed", nameof(vm.IsStep6Expanded), State(vm, "1010000", "Detect freeform")));
        expected.Add(new("changed", nameof(vm.IsStep2Expanded), State(vm, "1010000", "Detect freeform")));
        AppendPrimaryNotifications(expected, State(vm, "1010000", "Detect freeform"));
        Assert.Equal(expected, observation.Entries);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [MemberData(nameof(PropertyNames))]
    public async Task RepeatedAssignmentsPublishNothingBeforeAndAfterAChangeAsync(string propertyName)
    {
        using var scope = new ViewModelScope();
        var vm = await scope.OpenCadAsync();
        var property = Access(vm, propertyName);
        using var observation = new Observation(vm);
        var initial = Capture(vm);

        property.Set(property.Get());

        Assert.Empty(observation.Entries);
        Assert.Equal(initial, Capture(vm));
        property.Set(!property.Get());
        observation.Entries.Clear();
        var changed = Capture(vm);

        property.Set(property.Get());

        Assert.Empty(observation.Entries);
        Assert.Equal(changed, Capture(vm));
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.IsStep1Expanded), "1100000", "1000000", "Match overlaps")]
    [InlineData(nameof(FreeformHelperViewModel.IsStep2Expanded), "1010000", "1000000", "Detect freeform")]
    [InlineData(nameof(FreeformHelperViewModel.IsStep3Expanded), "1001000", "1000000", "Export")]
    [InlineData(nameof(FreeformHelperViewModel.IsStep5Expanded), "1000010", "1000000", "Export")]
    public async Task CollapsingWorkKeepsItsPrimaryActionAsync(string propertyName, string before, string after, string action)
    {
        using var scope = new ViewModelScope();
        var vm = await scope.OpenCadAsync();
        Access(vm, propertyName).Set(true);
        using var observation = new Observation(vm);

        Access(vm, propertyName).Set(false);

        var expected = new List<Notification>
        {
            new("changing", propertyName, State(vm, before, action)),
            new("changed", propertyName, State(vm, after, action)),
        };
        AppendPrimaryNotifications(expected, State(vm, after, action));
        Assert.Equal(expected, observation.Entries);
        Assert.Equal(State(vm, after, action), Capture(vm));
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.IsStep4Expanded), "1010100")]
    [InlineData(nameof(FreeformHelperViewModel.IsStep6Expanded), "1010001")]
    public async Task DiagnosticsKeepSelectedWorkAndPublishOnlyTheirOwnNotificationsAsync(string propertyName, string expanded)
    {
        using var scope = new ViewModelScope();
        var vm = await scope.OpenCadAsync();
        vm.IsStep2Expanded = true;
        using var observation = new Observation(vm);

        Access(vm, propertyName).Set(true);
        Access(vm, propertyName).Set(false);

        Assert.Equal(new Notification[]
        {
            new("changing", propertyName, State(vm, "1010000", "Detect freeform")),
            new("changed", propertyName, State(vm, expanded, "Detect freeform")),
            new("changing", propertyName, State(vm, expanded, "Detect freeform")),
            new("changed", propertyName, State(vm, "1010000", "Detect freeform")),
        }, observation.Entries);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ConsoleStaysIndependentWhenWorkExpansionReentersAsync()
    {
        using var scope = new ViewModelScope();
        var vm = await scope.OpenCadAsync();
        using var observation = new Observation(vm);

        vm.IsConsoleExpanded = false;
        vm.IsStep2Expanded = true;
        vm.IsConsoleExpanded = true;

        var expected = new List<Notification>
        {
            new("changing", nameof(vm.IsConsoleExpanded), State(vm, "1100000", "Match overlaps")),
            new("changed", nameof(vm.IsConsoleExpanded), State(vm, "0100000", "Match overlaps")),
        };
        expected.AddRange(ExpansionTrace(vm, nameof(vm.IsStep2Expanded), nameof(vm.IsStep1Expanded),
            "0100000", "0110000", "0010000", "Match overlaps", "Detect freeform"));
        expected.Add(new("changing", nameof(vm.IsConsoleExpanded), State(vm, "0010000", "Detect freeform")));
        expected.Add(new("changed", nameof(vm.IsConsoleExpanded), State(vm, "1010000", "Detect freeform")));
        Assert.Equal(expected, observation.Entries);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData("changing", nameof(FreeformHelperViewModel.IsStep2Expanded), 1, "1100000", "Match overlaps")]
    [InlineData("changing", nameof(FreeformHelperViewModel.IsStep1Expanded), 2, "1110000", "Detect freeform")]
    [InlineData("changed", nameof(FreeformHelperViewModel.IsStep1Expanded), 3, "1010000", "Detect freeform")]
    [InlineData("changed", nameof(FreeformHelperViewModel.WorkspacePrimaryActionText), 4, "1010000", "Detect freeform")]
    [InlineData("changed", nameof(FreeformHelperViewModel.IsStep2Expanded), 7, "1010000", "Detect freeform")]
    public async Task ObserverFailurePropagatesWithoutRollbackOrLaterNotificationsAsync(
        string phase, string propertyName, int eventCount, string flags, string action)
    {
        using var scope = new ViewModelScope();
        var vm = await scope.OpenCadAsync();
        using var observation = new Observation(vm, phase, propertyName);

        var error = Assert.Throws<InvalidOperationException>(() => vm.IsStep2Expanded = true);

        Assert.Same(observation.Failure, error);
        Assert.Equal(ExpansionTrace(vm, nameof(vm.IsStep2Expanded), nameof(vm.IsStep1Expanded),
            "1100000", "1110000", "1010000", "Match overlaps", "Detect freeform").Take(eventCount), observation.Entries);
        Assert.Equal(State(vm, flags, action), Capture(vm));
    }

    private static List<Notification> ExpansionTrace(
        FreeformHelperViewModel vm, string expandedProperty, string collapsedProperty,
        string before, string intermediate, string after, string oldAction, string newAction)
    {
        var expected = new List<Notification>
        {
            new("changing", expandedProperty, State(vm, before, oldAction)),
            new("changing", collapsedProperty, State(vm, intermediate, newAction)),
            new("changed", collapsedProperty, State(vm, after, newAction)),
        };
        AppendPrimaryNotifications(expected, State(vm, after, newAction));
        expected.Add(new("changed", expandedProperty, State(vm, after, newAction)));
        AppendPrimaryNotifications(expected, State(vm, after, newAction));
        return expected;
    }

    private static void AppendPrimaryNotifications(List<Notification> entries, Snapshot state)
    {
        entries.Add(new("changed", nameof(FreeformHelperViewModel.WorkspacePrimaryActionText), state));
        entries.Add(new("changed", nameof(FreeformHelperViewModel.WorkspacePrimaryActionCommand), state));
        entries.Add(new("changed", nameof(FreeformHelperViewModel.WorkspaceWorkHint), state));
    }

    private static Snapshot State(FreeformHelperViewModel vm, string flags, string action) => action switch
    {
        "Match overlaps" => new(flags, action, vm.MatchCommand, "Step 1 · Geometry match"),
        "Detect freeform" => new(flags, action, vm.AutoDetectFreeformsCommand, "Step 2 · Freeform tagging"),
        "Export" => new(flags, action, vm.ExportNotchCommand, "Step 3 preview → Step 5 · Notch export"),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private static Snapshot Capture(FreeformHelperViewModel vm)
        => new(Flags(vm), vm.WorkspacePrimaryActionText, vm.WorkspacePrimaryActionCommand, vm.WorkspaceWorkHint);

    // Flags are console, then steps 1 through 6. Every event captures the complete visible state.
    private static string Flags(FreeformHelperViewModel vm)
        => $"{Bit(vm.IsConsoleExpanded)}{Bit(vm.IsStep1Expanded)}{Bit(vm.IsStep2Expanded)}{Bit(vm.IsStep3Expanded)}" +
            $"{Bit(vm.IsStep4Expanded)}{Bit(vm.IsStep5Expanded)}{Bit(vm.IsStep6Expanded)}";

    private static char Bit(bool value) => value ? '1' : '0';

    private static void PrepareExpansion(FreeformHelperViewModel vm, string propertyName)
    {
        if (propertyName == nameof(vm.IsStep1Expanded)) vm.IsStep5Expanded = true;
    }

    private static PanelProperty Access(FreeformHelperViewModel vm, string name) => name switch
    {
        nameof(vm.IsConsoleExpanded) => new(() => vm.IsConsoleExpanded, value => vm.IsConsoleExpanded = value),
        nameof(vm.IsStep1Expanded) => new(() => vm.IsStep1Expanded, value => vm.IsStep1Expanded = value),
        nameof(vm.IsStep2Expanded) => new(() => vm.IsStep2Expanded, value => vm.IsStep2Expanded = value),
        nameof(vm.IsStep3Expanded) => new(() => vm.IsStep3Expanded, value => vm.IsStep3Expanded = value),
        nameof(vm.IsStep4Expanded) => new(() => vm.IsStep4Expanded, value => vm.IsStep4Expanded = value),
        nameof(vm.IsStep5Expanded) => new(() => vm.IsStep5Expanded, value => vm.IsStep5Expanded = value),
        nameof(vm.IsStep6Expanded) => new(() => vm.IsStep6Expanded, value => vm.IsStep6Expanded = value),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private sealed record PanelProperty(Func<bool> Get, Action<bool> Set);
    private sealed record Snapshot(string Flags, string ActionText, ICommand ActionCommand, string Hint);
    private sealed record Notification(string Phase, string? Name, Snapshot State);

    private sealed class ViewModelScope : IDisposable
    {
        private readonly LoadingModeTestScope _files = new();
        public FreeformHelperViewModel ViewModel { get; }

        public ViewModelScope()
        {
            ViewModel = _files.CreateViewModel();
            ViewModel.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }

        public async Task<FreeformHelperViewModel> OpenCadAsync()
        {
            await ViewModel.OpenDxfCommand.ExecuteAsync(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(ViewModel);
            ViewModel.AppGeneralPersistenceForTests.PersistTimer?.Stop();
            Assert.True(ViewModel.GetWorkflowStateSnapshot().HasCad);
            Assert.Equal("1100000", Flags(ViewModel));
            return ViewModel;
        }

        public void Dispose()
        {
            ViewModel.AppGeneralPersistenceForTests.PersistTimer?.Stop();
            _files.Dispose();
        }
    }

    private sealed class Observation : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;
        private readonly string? _throwPhase;
        private readonly string? _throwName;
        public List<Notification> Entries { get; } = [];
        public InvalidOperationException Failure { get; } = new("workflow panel observer failed");

        public Observation(FreeformHelperViewModel vm, string? throwPhase = null, string? throwName = null)
        {
            _vm = vm;
            _throwPhase = throwPhase;
            _throwName = throwName;
            vm.PropertyChanging += OnChanging;
            vm.PropertyChanged += OnChanged;
        }

        private void OnChanging(object? sender, PropertyChangingEventArgs e) => Record(sender, "changing", e.PropertyName);
        private void OnChanged(object? sender, PropertyChangedEventArgs e) => Record(sender, "changed", e.PropertyName);

        private void Record(object? sender, string phase, string? name)
        {
            Assert.Same(_vm, sender);
            Entries.Add(new(phase, name, Capture(_vm)));
            if (phase == _throwPhase && name == _throwName) throw Failure;
        }

        public void Dispose()
        {
            _vm.PropertyChanging -= OnChanging;
            _vm.PropertyChanged -= OnChanged;
        }
    }
}
