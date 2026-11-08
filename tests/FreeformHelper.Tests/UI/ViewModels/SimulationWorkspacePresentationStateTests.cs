// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspacePresentationStateTests
{
    [Fact]
    public void EmptyGrid_ShowsCanvasAndSafetyPlaceholdersWithoutInventingResults()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace([]);

        Assert.Empty(vm.SimulationOverlayItems);
        Assert.False(vm.HasSimulationData);
        Assert.True(vm.HasCanvasHint);
        Assert.Equal("No projected regular pads under All regular pads.", vm.CanvasHintText);
        Assert.Equal("Mode ready · All regular pads", vm.CanvasStatusSummaryText);
        Assert.Equal("No CSV loaded.", vm.CsvInspectorSummaryText);
        Assert.Equal("Max After -", vm.SimulationMaxAfterText);
        Assert.Equal("0", vm.SimulationSafetyViolationBadgeText);
        Assert.Equal("No simulation cells.", vm.SimulationHighRiskDiffListText);
        Assert.Equal("REG -", vm.SimulationSafetyViolationListText);
        Assert.Empty(vm.SimulationHighRiskDiffs);
        Assert.Equal("IC/diff -", vm.DuplicateDiffResolutionSampleText);
        Assert.False(vm.HasCopperPathReplay);
        Assert.Equal("Replay rows: not run.", vm.CopperPathReplayArtifactSummaryText);
    }

    [Fact]
    public void CopperInstruction_WhenSourceChanges_NotifiesBindingsAndDescribesTheActiveInteraction()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace([10]);
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        Assert.Equal("Select Copper to drive Before by movable CAD overlap area.", vm.CopperInstructionText);

        vm.SelectedInputSourceOption = vm.SourceOptions.Single(
            static option => option.Value == SimulationInputSourceMode.Copper);

        Assert.Contains(nameof(vm.CopperInstructionText), changedProperties);
        Assert.Equal(
            "Move the mouse over the AA canvas to update signal in real time. " +
            "Copper mode runs EMS, net-flow, and geometry audit through the same simulation gate.",
            vm.CopperInstructionText);
        changedProperties.Clear();

        vm.SelectedInputSourceOption = vm.SourceOptions.Single(
            static option => option.Value == SimulationInputSourceMode.Manual);

        Assert.Contains(nameof(vm.CopperInstructionText), changedProperties);
        Assert.Equal("Select Copper to drive Before by movable CAD overlap area.", vm.CopperInstructionText);
    }

    private static SimulationWorkspaceViewModel CreateWorkspace(IReadOnlyList<int> diffIndices)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(diffIndices);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);
    }
}
