// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfIndexShiftBoundaryCoverageTests
{
    [AvaloniaFact]
    public async Task DiffShiftSummary_SelectionAndDelta_DescribeTargetsAndRequireANonzeroIntegerAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        await vm.MatchCommand.ExecuteAsync(null);
        var ids = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        vm.SetCadOutputFwDiffIndexOverride(ids[0], 11);
        vm.SetCadOutputFwDiffIndexOverride(ids[1], 10);
        vm.ApplyCanvasSelection([], []);
        vm.HasUnsavedChanges = false;

        Assert.Equal("Diff shift: select CAD output pads in AA first.", vm.Step4DiffShiftSelectionSummary);
        Assert.False(vm.CanOffsetSelectedCadOutputFwDiffIndices);
        vm.OffsetSelectedCadOutputFwDiffIndicesCommand.Execute(null);
        Assert.Equal("CAD Output FW Diff offset: select CAD output pads first.", vm.StatusText);
        Assert.False(vm.HasUnsavedChanges);

        vm.ApplyCanvasSelection(ids, []);
        vm.Step4Index.ShiftDelta = 0m;
        Assert.False(vm.CanOffsetSelectedCadOutputFwDiffIndices);
        vm.Step4Index.ShiftDelta = 0.5m;
        Assert.False(vm.CanOffsetSelectedCadOutputFwDiffIndices);
        vm.Step4Index.ShiftDelta = -2m;

        Assert.True(vm.CanOffsetSelectedCadOutputFwDiffIndices);
        Assert.Equal(
            "Diff shift target: 2 CAD pad(s), current range 10-11, offset -2.",
            vm.Step4DiffShiftSelectionSummary);
        Assert.Equal(11, vm.GetCadOutputFwDiffIndexOverride(ids[0]));
        Assert.Equal(10, vm.GetCadOutputFwDiffIndexOverride(ids[1]));
    }

    [AvaloniaFact]
    public async Task DiffShift_ConflictingPlanPreservesOverrides_ValidShiftUndoRestoresPreviousOverridesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await ManualRangeStatusCoverageTests.OpenGridAsync(fixture);
        await vm.MatchCommand.ExecuteAsync(null);
        var ids = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        vm.SetCadOutputFwDiffIndexOverride(ids[0], 10);
        vm.SetCadOutputFwDiffIndexOverride(ids[1], 11);
        vm.ApplyCanvasSelection([ids[0]], []);
        vm.Step4Index.ShiftDelta = 1m;
        vm.HasUnsavedChanges = false;
        var links = vm.MatchLinksForCanvas.ToArray();
        Assert.NotEmpty(links);
        var expectedStatus =
            $"CAD Output FW Diff offset blocked: duplicate diff after shift at IC 1, diff 11 -> CAD {ids[0]}, {ids[1]}.";

        vm.OffsetSelectedCadOutputFwDiffIndicesCommand.Execute(null);

        Assert.Equal(expectedStatus, vm.StatusText);
        Assert.Equal(10, vm.GetCadOutputFwDiffIndexOverride(ids[0]));
        Assert.Equal(11, vm.GetCadOutputFwDiffIndexOverride(ids[1]));
        Assert.Equal(links, vm.MatchLinksForCanvas);
        Assert.False(vm.HasUnsavedChanges);

        vm.Step4Index.ShiftDelta = 2m;
        vm.OffsetSelectedCadOutputFwDiffIndicesCommand.Execute(null);

        Assert.Equal("Shifted 1 CAD pad(s) by 2. Range 10-10 -> 12-12.", vm.StatusText);
        Assert.Equal(12, vm.GetCadOutputFwDiffIndexOverride(ids[0]));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(ids[0], out var shifted));
        Assert.Equal(12, shifted);
        Assert.True(vm.HasUnsavedChanges);

        vm.Undo();

        Assert.Equal(10, vm.GetCadOutputFwDiffIndexOverride(ids[0]));
        Assert.Equal(11, vm.GetCadOutputFwDiffIndexOverride(ids[1]));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(ids[0], out var restored));
        Assert.Equal(10, restored);
    }
}
