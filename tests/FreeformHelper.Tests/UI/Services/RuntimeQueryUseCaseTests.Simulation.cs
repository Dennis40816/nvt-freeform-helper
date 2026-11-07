using System.Globalization;
using System.Text.Json;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_QuerySimulation_WhenAfterIsWithinEmsTolerance_ReportsWorkspaceAndRegularAsSafe()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var workspace = BuildSingleCellEmsToleranceBoundaryWorkspace();
        shell.Simulation.CurrentWorkspace = workspace;

        var response = await new RuntimeQueryUseCase(shell).ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "simulation",
            Args: new Dictionary<string, string>
            {
                ["regular-id"] = "0"
            }));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        var safety = root.GetProperty("workspace").GetProperty("safety");
        Assert.False(safety.GetProperty("hasSimulationSafetyViolations").GetBoolean());
        Assert.Equal(0, safety.GetProperty("simulationSafetyViolationCount").GetInt32());
        Assert.False(root.GetProperty("regular").GetProperty("isEmsSafetyRisk").GetBoolean());
    }

    [Fact]
    public void SimulationWorkspace_WhenAfterIsWithinEmsTolerance_ProjectsSafeCellStatus()
    {
        var workspace = BuildSingleCellEmsToleranceBoundaryWorkspace();

        var highRisk = Assert.Single(workspace.SimulationHighRiskDiffs);
        Assert.False(highRisk.IsViolation);
        Assert.Equal("Near cap", highRisk.StatusText);
        Assert.DoesNotContain("over", highRisk.MarginText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EMS OK", workspace.BuildHoverTipText(0), StringComparison.Ordinal);
    }

    private static SimulationWorkspaceViewModel BuildSingleCellEmsToleranceBoundaryWorkspace()
    {
        const int regularPadId = 0;
        var grid = TestGeometryFactory.CreateRegularGrid(
            rows: 1,
            cols: 1,
            cellWidth: 10d,
            cellHeight: 10d,
            diffIndexSelector: static (_, _) => 10);
        var session = new SimulationWorkspaceSession(
            grid,
            new NotchTable(Array.Empty<NotchTableRow>()),
            NullDiffValue: 65535,
            SourceRevision: 1,
            ActiveRegularPadIds: new HashSet<int> { regularPadId });

        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = SimulationSafetyAuditService.DefaultEmsAfterCap + 0.5e-9
        };
    }


    [Fact]
    public async Task ExecuteAsync_QuerySimulation_ReturnsWorkspaceAndRegularSnapshot()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var session = BuildSimulationSession();
        shell.Simulation.CurrentWorkspace = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            session)
        {
            GlobalValue = 100d
        };

        var useCase = new RuntimeQueryUseCase(shell);
        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "simulation",
            Args: new Dictionary<string, string>
            {
                ["regular-id"] = "0"
            }));

        Assert.True(response.Ok);
        var root = SerializeToRootElement(response.Data);
        var workspace = root.GetProperty("workspace");
        Assert.True(workspace.GetProperty("hasWorkspace").GetBoolean());
        Assert.Equal("Source: Manual", workspace.GetProperty("sourceMode").GetString());
        var cadOutputFwDiffAssignmentDecision = workspace.GetProperty("cadOutputFwDiffAssignmentDecision");
        Assert.Equal(1, cadOutputFwDiffAssignmentDecision.GetProperty("decisionCount").GetInt32());
        Assert.Equal("csv-constrained", cadOutputFwDiffAssignmentDecision.GetProperty("mode").GetString());
        Assert.Equal(1, cadOutputFwDiffAssignmentDecision.GetProperty("reasonCounts").GetProperty("needs-review").GetInt32());

        var regular = root.GetProperty("regular");
        Assert.Equal(0, regular.GetProperty("regularPadId").GetInt32());
        Assert.Equal(100d, regular.GetProperty("beforeValue").GetDouble(), 6);
        Assert.True(regular.GetProperty("afterValue").GetDouble() > 0d);
        var legend = workspace.GetProperty("legend");
        Assert.Equal("AUTO color scale", legend.GetProperty("title").GetString());
        var diffValidation = workspace.GetProperty("diffValidation");
        Assert.False(diffValidation.GetProperty("hasDiffViolationPads").GetBoolean());
        Assert.Equal(0, diffValidation.GetProperty("diffViolationPadCount").GetInt32());
        var diffIdentity = workspace.GetProperty("diffIdentity");
        Assert.False(diffIdentity.GetProperty("hasDuplicateDiffResolutions").GetBoolean());
        Assert.Equal(0, diffIdentity.GetProperty("duplicateDiffResolutionCount").GetInt32());
        Assert.Equal("merge-sum-active-regular-pads", diffIdentity.GetProperty("duplicateDiffResolutionStrategyText").GetString());
    }


    [Fact]
    public async Task ExecuteAsync_QuerySimulation_WhenWorkspaceMissing_AutoBuildsWorkspace()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var session = BuildSimulationSession();
        var buildRequested = false;
        shell.Simulation.RequestBuildWorkspaceAsync = () =>
        {
            buildRequested = true;
            shell.Simulation.CurrentWorkspace = new SimulationWorkspaceViewModel(
                new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
                session)
            {
                GlobalValue = 100d
            };
            return Task.CompletedTask;
        };

        var useCase = new RuntimeQueryUseCase(shell);
        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "simulation",
            Args: new Dictionary<string, string>
            {
                ["regular-id"] = "0"
            }));

        Assert.True(buildRequested);
        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        var root = SerializeToRootElement(response.Data);
        Assert.Equal(0, root.GetProperty("regular").GetProperty("regularPadId").GetInt32());
    }


    [Fact]
    public void ShellViewModel_ExposesCurrentSimulationSafetyAuditForExportPipeline()
    {
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        shell.Simulation.CurrentWorkspace = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            BuildSimulationSession())
        {
            GlobalValue = 100d
        };

        var audit = shell.FreeformHelper.GetCurrentSimulationSafetyAudit?.Invoke();

        Assert.NotNull(audit);
        Assert.True(audit!.HasCells);
    }


    [Fact]
    public async Task ExecuteAsync_NotchReadersShareSingleResolvedPathAcrossOverlayAllocationQueryAndInspector()
    {
        var (shell, cad) = CreateSharedResolvedPathScenario();
        using var shellScope = shell;
        var vm = shell.FreeformHelper;
        var resolved = vm.GetCadV22ResolvedResult(cad.Id);
        var allocation = vm.GetCadV22TargetAllocationSummary(cad.Id);
        var overlay = vm.GetCadV22StageOverlays(cad.Id);
        var inspectorSnapshot = vm.BuildCadPadInspectorSnapshot(cad.Id);

        Assert.NotNull(resolved);
        Assert.NotNull(allocation);
        Assert.True(overlay.HasValue);
        var inspectorCad = Assert.IsType<CadPadInspectorSnapshot>(inspectorSnapshot?.Cad);
        var inspectorNotch = Assert.IsType<PadInspectorNotchSnapshot>(inspectorCad.Notch);

        var resolvedResult = resolved!;
        var allocationSummary = allocation!;
        var overlayValue = overlay.Value;
        var inspectorDisplay = NotchDisplayProjector.Build(inspectorNotch);

        AssertInspectorMatchesResolvedAndAllocation(resolvedResult, allocationSummary, inspectorNotch);

        var useCase = new RuntimeQueryUseCase(shell);
        var notchResponse = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "notch",
            Args: new Dictionary<string, string>
            {
                ["cad-id"] = cad.Id.ToString(CultureInfo.InvariantCulture)
            }));
        var stageResponse = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "notch-stage",
            Args: new Dictionary<string, string>
            {
                ["cad-id"] = cad.Id.ToString(CultureInfo.InvariantCulture)
            }));
        var padResponse = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "pad",
            Args: new Dictionary<string, string>
            {
                ["cad-id"] = cad.Id.ToString(CultureInfo.InvariantCulture)
            }));

        Assert.True(notchResponse.Ok);
        Assert.True(stageResponse.Ok);
        Assert.True(padResponse.Ok);

        var notchRoot = SerializeToRootElement(notchResponse.Data);
        AssertNotchRatiosPayload(notchRoot.GetProperty("ratios"), resolvedResult, inspectorDisplay);
        AssertNotchAllocationPayload(notchRoot.GetProperty("stage3Allocation"), allocationSummary, inspectorDisplay);
        AssertNotchToFullPolygonsPayload(notchRoot.GetProperty("toFullPolygons"), overlayValue.Stage1Seed, overlayValue.Stage3Final);

        var stageRoot = SerializeToRootElement(stageResponse.Data);
        Assert.Equal(overlayValue.IsToFullEnabled, stageRoot.GetProperty("isToFullEnabled").GetBoolean());
        var stages = stageRoot.GetProperty("stages");
        AssertStagePayloadMatchesPolygons(stages.GetProperty("stage1"), "seed", overlayValue.Stage1Seed);
        AssertStagePayloadMatchesPolygons(stages.GetProperty("stage2"), "candidate", overlayValue.Stage2Candidate);
        AssertStagePayloadMatchesPolygons(stages.GetProperty("stage3"), "final", overlayValue.Stage3Final);

        var padNotch = SerializeToRootElement(padResponse.Data).GetProperty("snapshot")
            .GetProperty("cad")
            .GetProperty("notch");
        AssertPadNotchPayload(padNotch, inspectorNotch, inspectorDisplay);
    }

    private static (ShellViewModel Shell, CadPad Cad) CreateSharedResolvedPathScenario()
    {
        var shell = new ShellViewModel { IsDevPageAvailable = true };
        var vm = shell.FreeformHelper;
        var cad = CreateCadPad(id: 40, minX: 0, minY: 0, maxX: 15, maxY: 5);
        var blocker = CreateCadPad(id: 41, minX: 12, minY: 0, maxX: 20, maxY: 5);
        var regularLeft = CreateRegularPad(regularPadId: 100, row: 0, col: 0, minX: 0, minY: 0, maxX: 10, maxY: 10, icIndex: 0, diffIndex: 10);
        var regularRight = CreateRegularPad(regularPadId: 101, row: 0, col: 1, minX: 10, minY: 0, maxX: 20, maxY: 10, icIndex: 0, diffIndex: 20);

        vm.CadPads.Add(cad);
        vm.CadPads.Add(blocker);
        vm.RegularPads.Add(regularLeft);
        vm.RegularPads.Add(regularRight);
        vm.EnableToRegular = true;
        vm.EnableToFull = true;

        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(new[] { cad, blocker });
        FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(
            rows: 1,
            cols: 2,
            xEdges: _validationGridXEdges,
            yEdges: _validationGridYEdges,
            pads: new[] { regularLeft, regularRight });

        var cadOutputFwDiffIndexByCadId = vm.CadIndexMaps.OutputFwDiffIndexByCadId;
        cadOutputFwDiffIndexByCadId.Clear();
        cadOutputFwDiffIndexByCadId[cad.Id] = 10;
        cadOutputFwDiffIndexByCadId[blocker.Id] = 20;

        var cadIcIndexByCadId = vm.CadIndexMaps.IcIndexByCadId;
        cadIcIndexByCadId.Clear();
        cadIcIndexByCadId[cad.Id] = 0;
        cadIcIndexByCadId[blocker.Id] = 0;

        return (shell, cad);
    }

    private static void AssertInspectorMatchesResolvedAndAllocation(
        NotchV22ResolvedResult resolvedResult,
        NotchV22TargetAllocationSummary allocationSummary,
        PadInspectorNotchSnapshot inspectorNotch)
    {
        Assert.Equal(resolvedResult.TargetAllocation.Stage3Area, allocationSummary.Stage3Area, 6);
        if (resolvedResult.Stage3FinalOutlinePolygons.Count > 0)
        {
            Assert.Equal(resolvedResult.Compensation.Stage3Area, resolvedResult.Stage3FinalOutlinePolygons.Sum(static polygon => polygon.Area()), 6);
        }
        Assert.Equal(resolvedResult.Compensation.ToRegularRatio, inspectorNotch.ToRegularRatio, 6);
        Assert.Equal(resolvedResult.Compensation.ToFullRatio, inspectorNotch.ToFullRatio, 6);
        Assert.Equal(resolvedResult.Compensation.CombinedRatio, inspectorNotch.CombinedRatio, 6);
        Assert.Equal(resolvedResult.Compensation.IsToFullEnabled, inspectorNotch.IsToFullEnabled);
        Assert.Equal(resolvedResult.Compensation.Stage3Area, inspectorNotch.Stage3Area, 6);
        Assert.Equal(allocationSummary.StrictAreaThreshold, inspectorNotch.StrictAreaThreshold, 6);
        Assert.Equal(allocationSummary.Targets.Count, inspectorNotch.Targets.Count);

        for (var i = 0; i < allocationSummary.Targets.Count; i++)
        {
            var expectedTarget = allocationSummary.Targets[i];
            var inspectorTarget = inspectorNotch.Targets[i];
            Assert.Equal(expectedTarget.IcIndex, inspectorTarget.IcIndex);
            Assert.Equal(expectedTarget.DiffIndex, inspectorTarget.DiffIndex);
            Assert.Equal(expectedTarget.EffectiveArea, inspectorTarget.EffectiveArea, 6);
            Assert.Equal(expectedTarget.Ratio, inspectorTarget.Ratio, 6);
            Assert.Equal(expectedTarget.RatioPercentRounded, inspectorTarget.RatioPercentRounded);
            Assert.Equal(expectedTarget.PassesStrictThreshold, inspectorTarget.PassesStrictThreshold);
            Assert.Equal(expectedTarget.IsAnchorDiff, inspectorTarget.IsAnchorDiff);
            Assert.Equal(expectedTarget.ToFullAppliedRegularCount, inspectorTarget.ToFullAppliedRegularCount);
            Assert.Equal(expectedTarget.RegularCount, inspectorTarget.RegularCount);
        }
    }

    private static void AssertNotchRatiosPayload(
        JsonElement ratios,
        NotchV22ResolvedResult resolvedResult,
        NotchDisplayProjection inspectorDisplay)
    {
        Assert.Equal(resolvedResult.Compensation.ToRegularRatio, ratios.GetProperty("toRegularRatio").GetDouble(), 6);
        Assert.Equal(resolvedResult.Compensation.ToFullRatio, ratios.GetProperty("toFullRatio").GetDouble(), 6);
        Assert.Equal(resolvedResult.Compensation.CombinedRatio, ratios.GetProperty("combinedRatio").GetDouble(), 6);
        Assert.Equal(resolvedResult.Compensation.Stage3Area, ratios.GetProperty("stage3Area").GetDouble(), 6);
        Assert.Equal(resolvedResult.Compensation.IsToFullEnabled, ratios.GetProperty("isToFullEnabled").GetBoolean());

        var queryNotchDisplay = ratios.GetProperty("display");
        Assert.Equal(inspectorDisplay.ToRegularRatioText, queryNotchDisplay.GetProperty("toRegularRatioText").GetString());
        Assert.Equal(inspectorDisplay.ToFullRatioText, queryNotchDisplay.GetProperty("toFullRatioText").GetString());
        Assert.Equal(inspectorDisplay.CombinedRatioText, queryNotchDisplay.GetProperty("combinedRatioText").GetString());
        Assert.Equal(inspectorDisplay.Stage3AreaText, queryNotchDisplay.GetProperty("stage3AreaText").GetString());
        Assert.Equal(inspectorDisplay.ToFullReasonShortText, queryNotchDisplay.GetProperty("toFullReasonShortText").GetString());
    }

    private static void AssertNotchAllocationPayload(
        JsonElement stage3Allocation,
        NotchV22TargetAllocationSummary allocationSummary,
        NotchDisplayProjection inspectorDisplay)
    {
        Assert.Equal(allocationSummary.CadArea, stage3Allocation.GetProperty("cadArea").GetDouble(), 6);
        Assert.Equal(allocationSummary.Stage3Area, stage3Allocation.GetProperty("stage3Area").GetDouble(), 6);
        Assert.Equal(allocationSummary.StrictAreaThreshold, stage3Allocation.GetProperty("strictAreaThreshold").GetDouble(), 6);
        Assert.Equal(allocationSummary.Targets.Count, stage3Allocation.GetProperty("targetCount").GetInt32());
        var returnedTargetCount = stage3Allocation.GetProperty("returnedTargets").GetInt32();
        Assert.Equal(returnedTargetCount, stage3Allocation.GetProperty("targets").GetArrayLength());
        Assert.Equal(
            Math.Max(0, allocationSummary.Targets.Count - returnedTargetCount),
            stage3Allocation.GetProperty("truncatedTargets").GetInt32());

        for (var i = 0; i < returnedTargetCount; i++)
        {
            AssertTargetPayloadMatches(stage3Allocation.GetProperty("targets")[i], allocationSummary.Targets[i]);
        }

        var queryAllocationDisplay = stage3Allocation.GetProperty("display");
        Assert.Equal(inspectorDisplay.TargetAllocationSummaryText, queryAllocationDisplay.GetProperty("targetAllocationSummaryText").GetString());
        Assert.Equal(inspectorDisplay.OwnerSummaryText, queryAllocationDisplay.GetProperty("ownerSummaryText").GetString());
    }

    private static void AssertNotchToFullPolygonsPayload(
        JsonElement toFullPolygons,
        IReadOnlyList<Polygon2> seed,
        IReadOnlyList<Polygon2> final)
    {
        Assert.Equal(seed.Count, toFullPolygons.GetProperty("seedCount").GetInt32());
        Assert.Equal(final.Count, toFullPolygons.GetProperty("finalCount").GetInt32());
        Assert.Equal(seed.Count, toFullPolygons.GetProperty("seedBounds").GetArrayLength());
        Assert.Equal(final.Count, toFullPolygons.GetProperty("finalBounds").GetArrayLength());
        for (var i = 0; i < seed.Count; i++)
        {
            AssertBoundsMatch(toFullPolygons.GetProperty("seedBounds")[i], seed[i]);
        }

        for (var i = 0; i < final.Count; i++)
        {
            AssertBoundsMatch(toFullPolygons.GetProperty("finalBounds")[i], final[i]);
        }
    }

    private static void AssertPadNotchPayload(
        JsonElement padNotch,
        PadInspectorNotchSnapshot inspectorNotch,
        NotchDisplayProjection inspectorDisplay)
    {
        Assert.Equal(inspectorNotch.ToRegularRatio, padNotch.GetProperty("toRegularRatio").GetDouble(), 6);
        Assert.Equal(inspectorNotch.ToFullRatio, padNotch.GetProperty("toFullRatio").GetDouble(), 6);
        Assert.Equal(inspectorNotch.CombinedRatio, padNotch.GetProperty("combinedRatio").GetDouble(), 6);
        Assert.Equal(inspectorNotch.IsToFullEnabled, padNotch.GetProperty("isToFullEnabled").GetBoolean());
        Assert.Equal(inspectorNotch.Stage3Area, padNotch.GetProperty("stage3Area").GetDouble(), 6);
        Assert.Equal(inspectorNotch.StrictAreaThreshold, padNotch.GetProperty("strictAreaThreshold").GetDouble(), 6);
        Assert.Equal(inspectorNotch.Targets.Count, padNotch.GetProperty("targets").GetArrayLength());

        var padDisplay = padNotch.GetProperty("display");
        Assert.Equal(inspectorDisplay.ToRegularRatioText, padDisplay.GetProperty("toRegularRatioText").GetString());
        Assert.Equal(inspectorDisplay.ToFullRatioText, padDisplay.GetProperty("toFullRatioText").GetString());
        Assert.Equal(inspectorDisplay.CombinedRatioText, padDisplay.GetProperty("combinedRatioText").GetString());
        Assert.Equal(inspectorDisplay.Stage3AreaText, padDisplay.GetProperty("stage3AreaText").GetString());
        Assert.Equal(inspectorDisplay.TargetAllocationSummaryText, padDisplay.GetProperty("targetAllocationSummaryText").GetString());
        Assert.Equal(inspectorDisplay.OwnerSummaryText, padDisplay.GetProperty("ownerSummaryText").GetString());
        Assert.Equal(inspectorDisplay.ToFullReasonShortText, padDisplay.GetProperty("toFullReasonShortText").GetString());
    }
}
