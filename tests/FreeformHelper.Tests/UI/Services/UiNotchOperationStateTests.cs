// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UiNotchOperationStateTests
{
    [Theory]
    [InlineData(true, 0.0, "Idle.")]
    [InlineData(false, 0.25, "Idle.")]
    [InlineData(false, 0.0, "Working")]
    public void SetCurrent_ChangedSnapshot_RaisesOneChangeWithReplacedValue(bool busy, double progress, string text)
    {
        var state = new UiNotchOperationState();
        var changes = new List<UiNotchOperationState.Snapshot>();
        state.Changed += () => changes.Add(state.Current);
        var value = new UiNotchOperationState.Snapshot(busy, progress, text);

        state.SetCurrent(value);

        Assert.Equal(value, Assert.Single(changes));
        Assert.Equal(value, state.Current);
    }

    [Theory]
    [InlineData(false, 0.0, "Idle.")]
    [InlineData(true, 0.25, "Working")]
    [InlineData(false, double.NaN, "Idle.")]
    public void SetCurrent_EqualSnapshot_DoesNotRaiseChange(bool busy, double progress, string text)
    {
        var state = new UiNotchOperationState();
        var value = new UiNotchOperationState.Snapshot(busy, progress, text);
        state.SetCurrent(value);
        var changes = new List<UiNotchOperationState.Snapshot>();
        state.Changed += () => changes.Add(state.Current);

        state.SetCurrent(value with { Text = new string(text.ToCharArray()) });

        Assert.Empty(changes);
        Assert.Equal(value, state.Current);
    }

    [Theory]
    [InlineData(0, false, 0.25, "Working")]
    [InlineData(1, true, 0.5, "Working")]
    [InlineData(2, true, 0.25, "Done")]
    public void SetComponent_ChangedValue_PreservesOtherComponents(int component, bool busy, double progress, string text)
    {
        var state = new UiNotchOperationState();
        state.SetCurrent(new(true, 0.25, "Working"));

        SetComponent(state, component, false, 0.5, "Done");

        Assert.Equal(new UiNotchOperationState.Snapshot(busy, progress, text), state.Current);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 0.0)]
    [InlineData(1, double.NaN)]
    [InlineData(2, 0.0)]
    public void SetComponent_EqualValue_DoesNotRaiseChange(int component, double progress)
    {
        var state = new UiNotchOperationState();
        state.SetProgress(progress);
        var changes = new List<UiNotchOperationState.Snapshot>();
        state.Changed += () => changes.Add(state.Current);

        SetComponent(state, component, false, progress, new string("Idle.".ToCharArray()));

        Assert.Empty(changes);
    }

    [Fact]
    public void BeginBusyScope_Idle_SetsBusyAndPreservesProgressAndText()
    {
        var state = new UiNotchOperationState();
        state.SetCurrent(new(false, 0.25, "Working"));

        state.BeginBusyScope(state.SetBusy);

        Assert.Equal(new UiNotchOperationState.Snapshot(true, 0.25, "Working"), state.Current);
    }

    [Fact]
    public void BeginBusyScope_AlreadyBusy_DoesNotRaiseAnotherChange()
    {
        var state = new UiNotchOperationState();
        state.BeginBusyScope(state.SetBusy);
        var changes = new List<UiNotchOperationState.Snapshot>();
        state.Changed += () => changes.Add(state.Current);

        state.BeginBusyScope(state.SetBusy);

        Assert.Empty(changes);
        Assert.True(state.Current.IsBusy);
    }

    [Fact]
    public void EndBusyScope_NestedOrOverlappingScopes_KeepsBusyUntilLastEnd()
    {
        var state = new UiNotchOperationState();
        state.BeginBusyScope(state.SetBusy);
        state.BeginBusyScope(state.SetBusy);

        state.EndBusyScope(state.SetBusy);
        Assert.True(state.Current.IsBusy);
        state.EndBusyScope(state.SetBusy);

        Assert.False(state.Current.IsBusy);
    }

    [Fact]
    public void EndBusyScope_WithoutBegin_LeavesBusyFalseAndCounterNegative()
    {
        var state = new UiNotchOperationState();

        state.EndBusyScope(state.SetBusy);
        Assert.False(state.Current.IsBusy);
        state.BeginBusyScope(state.SetBusy);
        state.BeginBusyScope(state.SetBusy);
        state.EndBusyScope(state.SetBusy);

        Assert.False(state.Current.IsBusy); // -1, 0, 1, 0: an unmatched end is not clamped.
    }

    [AvaloniaTheory]
    [InlineData(true, 0.0, nameof(FreeformHelperViewModel.IsNotchExporting))]
    [InlineData(false, 0.25, nameof(FreeformHelperViewModel.NotchExportProgress))]
    public void NotchForwards_ChangedBusyOrProgress_NotifiesOldNamesInOriginalOrder(bool busy, double progress, string name)
    {
        var vm = new FreeformHelperViewModel();
        var changes = new List<(string Phase, string? Name, UiNotchOperationState.Snapshot Value)>();
        vm.PropertyChanging += (_, e) => changes.Add(("changing", e.PropertyName, ReadSnapshot(vm)));
        vm.PropertyChanged += (_, e) => changes.Add(("changed", e.PropertyName, ReadSnapshot(vm)));

        vm.IsNotchExporting = busy;
        vm.NotchExportProgress = progress;

        var before = new UiNotchOperationState.Snapshot(false, 0.0, "Idle.");
        var after = new UiNotchOperationState.Snapshot(busy, progress, "Idle.");
        Assert.Equal(new[] { ("changing", (string?)name, before),
            ("changed", nameof(vm.ShouldShowNotchExportProgress), after), ("changed", name, after) }, changes);
    }

    [AvaloniaFact]
    public void NotchExportProgressText_ChangedText_NotifiesOnlyOldNameBeforeAndAfterReplacement()
    {
        var vm = new FreeformHelperViewModel();
        var changes = new List<(string Phase, string? Name, string Text)>();
        vm.PropertyChanging += (_, e) => changes.Add(("changing", e.PropertyName, vm.NotchExportProgressText));
        vm.PropertyChanged += (_, e) => changes.Add(("changed", e.PropertyName, vm.NotchExportProgressText));

        vm.NotchExportProgressText = "Working";

        Assert.Equal(new[] { ("changing", (string?)nameof(vm.NotchExportProgressText), "Idle."),
            ("changed", nameof(vm.NotchExportProgressText), "Working") }, changes);
    }

    [AvaloniaTheory]
    [InlineData(false, 0.0, "Idle.")]
    [InlineData(true, 0.25, "Working")]
    [InlineData(false, double.NaN, "Idle.")]
    public void NotchForwards_EqualComponents_DoNotRaiseNotifications(bool busy, double progress, string text)
    {
        var vm = new FreeformHelperViewModel { IsNotchExporting = busy, NotchExportProgress = progress, NotchExportProgressText = text };
        var changes = new List<string?>();
        vm.PropertyChanging += (_, e) => changes.Add(e.PropertyName);
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.IsNotchExporting = busy;
        vm.NotchExportProgress = progress;
        vm.NotchExportProgressText = new string(text.ToCharArray());

        Assert.Empty(changes);
    }

    private static UiNotchOperationState.Snapshot ReadSnapshot(FreeformHelperViewModel vm) =>
        new(vm.IsNotchExporting, vm.NotchExportProgress, vm.NotchExportProgressText);

    private static void SetComponent(UiNotchOperationState state, int component, bool busy, double progress, string text)
    {
        Action write = component switch
        {
            0 => () => state.SetBusy(busy),
            1 => () => state.SetProgress(progress),
            2 => () => state.SetText(text),
            _ => throw new ArgumentOutOfRangeException(nameof(component)),
        };
        write();
    }
}
