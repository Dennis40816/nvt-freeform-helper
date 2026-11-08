// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests.UI.Services;

public sealed class NotchSparseResultUseCaseBehaviorTests
{
    [Theory]
    [InlineData("grid-rebuilt")]
    [InlineData("selection-cleared")]
    [InlineData("selection-multiple")]
    [InlineData("cad-removed")]
    [InlineData("ic-unassigned")]
    [InlineData("diff-unassigned")]
    public void CreateRequest_IncompleteSelectionOrWorkflowDoesNotResolveAReusableResult(string change)
    {
        var scenario = CreateScenario();
        IReadOnlySet<int> selected = new HashSet<int> { 7 };
        var currentGrid = scenario.Grid;
        var cad = scenario.Cad;
        var workflow = scenario.Workflow;
        switch (change)
        {
            case "grid-rebuilt":
                currentGrid = CreateGrid();
                break;
            case "selection-cleared":
                selected = new HashSet<int>();
                break;
            case "selection-multiple":
                selected = new HashSet<int> { 7, 8 };
                break;
            case "cad-removed":
                cad = new CadPadSet(Array.Empty<CadPad>());
                break;
            case "ic-unassigned":
                workflow = workflow with { CadIcIndexByCadId = new Dictionary<int, int>() };
                break;
            case "diff-unassigned":
                workflow = workflow with { CadOutputFwDiffIndexByCadId = new Dictionary<int, int>() };
                break;
        }

        var request = NotchSparseResultUseCase.CreateRequest(
            selected, cad, currentGrid, scenario.Grid, workflow,
            _ => throw new InvalidOperationException("An incomplete request must not resolve a reusable result."));

        Assert.Null(request);
    }

    [Theory]
    [InlineData("grid-rebuilt")]
    [InlineData("selection-cleared")]
    [InlineData("selection-multiple")]
    [InlineData("selection-replaced")]
    [InlineData("batch-without-selection")]
    [InlineData("request-cad-changed")]
    [InlineData("request-ic-changed")]
    [InlineData("request-diff-changed")]
    [InlineData("ic-unassigned")]
    [InlineData("ic-reassigned")]
    [InlineData("diff-unassigned")]
    [InlineData("diff-reassigned")]
    public void GetCurrentResult_PromotesTheSelectedResultButRejectsItAfterItsContextChanges(string change)
    {
        var scenario = CreateScenario();
        IReadOnlySet<int> selected = new HashSet<int> { 7 };
        var request = Assert.IsType<NotchTableGenerator.CadAllocationSparseResultRequest>(
            NotchSparseResultUseCase.CreateRequest(
                selected, scenario.Cad, scenario.Grid, scenario.Grid, scenario.Workflow, _ => null));
        var batch = ResolveBatch(scenario, request);
        var captured = Assert.IsType<NotchTableGenerator.CadAllocationSparseResult>(batch.SelectedSparseResult);
        Assert.Equal((7, 0, 0), (request.CadPadId, request.AnchorIcIndex, request.AnchorDiffIndex));
        Assert.Same(captured.ResolvedResult, NotchSparseResultUseCase.GetCurrentResult(
            batch, request, selected, scenario.Grid, scenario.Grid, scenario.Workflow));
        var warmRequest = Assert.IsType<NotchTableGenerator.CadAllocationSparseResultRequest>(
            NotchSparseResultUseCase.CreateRequest(
                selected, scenario.Cad, scenario.Grid, scenario.Grid, scenario.Workflow,
                pad => pad.Id == 7 ? captured.ResolvedResult : null));
        Assert.Same(captured.ResolvedResult, warmRequest.ReusableResult);

        var currentGrid = scenario.Grid;
        var workflow = scenario.Workflow;
        switch (change)
        {
            case "grid-rebuilt":
                currentGrid = CreateGrid();
                break;
            case "selection-cleared":
                selected = new HashSet<int>();
                break;
            case "selection-multiple":
                selected = new HashSet<int> { 7, 8 };
                break;
            case "selection-replaced":
                selected = new HashSet<int> { 8 };
                break;
            case "batch-without-selection":
                batch = ResolveBatch(scenario, null);
                break;
            case "request-cad-changed":
                request = request with { CadPadId = 8 };
                selected = new HashSet<int> { 8 };
                workflow = workflow with
                {
                    CadIcIndexByCadId = new Dictionary<int, int> { [8] = 0 },
                    CadOutputFwDiffIndexByCadId = new Dictionary<int, int> { [8] = 0 },
                };
                break;
            case "request-ic-changed":
                request = request with { AnchorIcIndex = 1 };
                workflow = ChangeAnchor(workflow, "ic-reassigned");
                break;
            case "request-diff-changed":
                request = request with { AnchorDiffIndex = 1 };
                workflow = ChangeAnchor(workflow, "diff-reassigned");
                break;
            default:
                workflow = ChangeAnchor(workflow, change);
                break;
        }

        Assert.Null(NotchSparseResultUseCase.GetCurrentResult(
            batch, request, selected, currentGrid, scenario.Grid, workflow));
    }

    private static WorkflowDataSnapshot ChangeAnchor(WorkflowDataSnapshot workflow, string change) => change switch
    {
        "ic-unassigned" => workflow with { CadIcIndexByCadId = new Dictionary<int, int>() },
        "ic-reassigned" => workflow with { CadIcIndexByCadId = new Dictionary<int, int> { [7] = 1 } },
        "diff-unassigned" => workflow with { CadOutputFwDiffIndexByCadId = new Dictionary<int, int>() },
        "diff-reassigned" => workflow with { CadOutputFwDiffIndexByCadId = new Dictionary<int, int> { [7] = 1 } },
        _ => throw new ArgumentOutOfRangeException(nameof(change)),
    };

    private static NotchTableGenerator.CadAllocationResolvedBatch ResolveBatch(
        Scenario scenario, NotchTableGenerator.CadAllocationSparseResultRequest? request) =>
        new NotchTableGenerator().ResolveCadAllocationBatch(
            scenario.Cad, scenario.Grid, scenario.Settings,
            cadOutputFwDiffIndexByCadId: scenario.Workflow.CadOutputFwDiffIndexByCadId,
            selectedSparseResultRequest: request);

    private static Scenario CreateScenario()
    {
        var grid = CreateGrid();
        var pad = TestGeometryFactory.CreateCadPad(7, "PAD", 0, 0, 15, 10);
        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = pad.Id;
            regular.MatchScore = 1d;
        }

        var workflow = WorkflowDataSnapshot.Empty with
        {
            CadIcIndexByCadId = new Dictionary<int, int> { [7] = 0 },
            CadOutputFwDiffIndexByCadId = new Dictionary<int, int> { [7] = 0 },
        };
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = [NotchAlgorithmVersion.V22],
                EnableToFull = false,
            },
        };
        return new Scenario(grid, new CadPadSet([pad]), workflow, settings);
    }

    private static RegularGrid CreateGrid() =>
        TestGeometryFactory.CreateRegularGrid(1, 2, cellWidth: 10, cellHeight: 10,
            diffIndexSelector: static (_, col) => col);

    private sealed record Scenario(RegularGrid Grid, CadPadSet Cad, WorkflowDataSnapshot Workflow, ProjectSettings Settings);
}
