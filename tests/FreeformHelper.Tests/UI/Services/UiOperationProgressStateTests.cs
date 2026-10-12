// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UiOperationProgressStateTests
{
    [Theory]
    [InlineData(true, 0.0)]
    [InlineData(false, 0.25)]
    [InlineData(true, 0.25)]
    public void SetCurrent_ChangedSnapshot_RaisesOneChangeWithReplacedValue(bool busy, double progress)
    {
        var state = new UiOperationProgressState();
        var changes = new List<UiOperationProgressState.Snapshot>();
        state.Changed += () => changes.Add(state.Current);
        var value = new UiOperationProgressState.Snapshot(busy, progress);

        state.SetCurrent(value);

        Assert.Equal(value, Assert.Single(changes));
        Assert.Equal(value, state.Current);
    }

    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(true, 0.25)]
    [InlineData(false, double.NaN)]
    public void SetCurrent_EqualSnapshot_DoesNotRaiseChange(bool busy, double progress)
    {
        var state = new UiOperationProgressState();
        var value = new UiOperationProgressState.Snapshot(busy, progress);
        state.SetCurrent(value);
        var changes = new List<UiOperationProgressState.Snapshot>();
        state.Changed += () => changes.Add(state.Current);

        state.SetCurrent(value);

        Assert.Empty(changes);
        Assert.Equal(value, state.Current);
    }

    [Fact]
    public void SetBusy_ChangedBusy_PreservesProgress()
    {
        var state = new UiOperationProgressState();
        state.SetProgress(0.25);

        state.SetBusy(true);

        Assert.Equal(new UiOperationProgressState.Snapshot(true, 0.25), state.Current);
    }

    [Fact]
    public void SetBusy_EqualBusy_DoesNotRaiseChange()
    {
        var state = new UiOperationProgressState();
        var changes = new List<UiOperationProgressState.Snapshot>();
        state.Changed += () => changes.Add(state.Current);

        state.SetBusy(false);

        Assert.Empty(changes);
    }

    [Fact]
    public void SetProgress_ChangedProgress_PreservesBusy()
    {
        var state = new UiOperationProgressState();
        state.SetBusy(true);

        state.SetProgress(0.25);

        Assert.Equal(new UiOperationProgressState.Snapshot(true, 0.25), state.Current);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    public void SetProgress_EqualProgress_DoesNotRaiseChange(double progress)
    {
        var state = new UiOperationProgressState();
        state.SetProgress(progress);
        var changes = new List<UiOperationProgressState.Snapshot>();
        state.Changed += () => changes.Add(state.Current);

        state.SetProgress(progress);

        Assert.Empty(changes);
    }

    [AvaloniaTheory]
    [InlineData(true, 0.0, nameof(FreeformHelperViewModel.IsCheckingDxfOverlap))]
    [InlineData(false, 0.25, nameof(FreeformHelperViewModel.DxfOverlapProgress))]
    public void ProgressForwards_ChangedComponent_RaisesOnlyOldNameBeforeAndAfterReplacement(
        bool busy, double progress, string name)
    {
        var vm = new FreeformHelperViewModel();
        var changes = new List<(string Phase, string? Name, bool Busy, double Progress)>();
        vm.PropertyChanging += (_, e) => changes.Add(("changing", e.PropertyName, vm.IsCheckingDxfOverlap, vm.DxfOverlapProgress));
        vm.PropertyChanged += (_, e) => changes.Add(("changed", e.PropertyName, vm.IsCheckingDxfOverlap, vm.DxfOverlapProgress));

        vm.IsCheckingDxfOverlap = busy;
        vm.DxfOverlapProgress = progress;

        Assert.Equal(new[] { ("changing", (string?)name, false, 0.0), ("changed", (string?)name, busy, progress) }, changes);
    }

    [AvaloniaTheory]
    [InlineData(false, 0.0)]
    [InlineData(true, 0.25)]
    [InlineData(false, double.NaN)]
    public void ProgressForwards_EqualComponents_DoNotRaiseOldNames(bool busy, double progress)
    {
        var vm = new FreeformHelperViewModel { IsCheckingDxfOverlap = busy, DxfOverlapProgress = progress };
        var changes = new List<string?>();
        vm.PropertyChanging += (_, e) => changes.Add(e.PropertyName);
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.IsCheckingDxfOverlap = busy;
        vm.DxfOverlapProgress = progress;

        Assert.Empty(changes);
    }
}
