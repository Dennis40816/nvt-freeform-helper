using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableGeneratorTests
{
    [Fact]
    public void ProjectCadAllocationResolvedBatch_ReusesVersionNeutralCandidatesAcrossFinalRequests()
    {
        const string expectedV22 =
            "V22|IC0|DIFF0|REG0|CAD1|0,100,65535,0,65535,0,0|CAD=1 R=100% F=100% C=100% NT";
        const string expectedV21 =
            "V21|IC0|DIFF0|REG0|CAD1|0,100,100,65535,0,0,65535,0,0|CAD=1 R=100% F=100% C=100% NT";

        AssertProjectionScenario(
            CreateCadWithBounds(-5, -5, 15, 15),
            linkVersionThresholds: false,
            thresholdQ7: 64,
            thresholdPercentV22: 20.0,
            expectedV21Rows: [],
            expectedV22Rows: [expectedV22],
            expectedCombinedRows: [expectedV22, expectedV21]);
        AssertProjectionScenario(
            CreateCadWithBounds(-5, -5, 15, 15),
            linkVersionThresholds: false,
            thresholdQ7: 32,
            thresholdPercentV22: 50.0,
            expectedV21Rows: [expectedV21],
            expectedV22Rows: [],
            expectedCombinedRows: []);
        AssertProjectionScenario(
            CreateCadWithBounds(-5, -5, 15, 15),
            linkVersionThresholds: true,
            thresholdQ7: 32,
            thresholdPercentV22: 0.0,
            expectedV21Rows: [expectedV21],
            expectedV22Rows: [expectedV22],
            expectedCombinedRows: [expectedV22, expectedV21]);
        AssertProjectionScenario(
            CreateCadWithBounds(0, 0, 30, 10),
            linkVersionThresholds: true,
            thresholdQ7: 43,
            thresholdPercentV22: 0.0,
            expectedV21Rows: [expectedV21],
            expectedV22Rows: [],
            expectedCombinedRows: []);
    }

    [Fact]
    public void ResolveCadAllocationBatchWithSelectedResult_ReusesOneWarmSparseResult()
    {
        var scenario = CreateWarmSparseScenario();

        AssertWarmResultIsSharedAndProjectsLikeFreshGenerate(scenario);
        AssertMismatchedWarmIdentitiesAreRejected(scenario);

        scenario.Grid.Pads[1].MatchScore += 0.01;
        Assert.Throws<ArgumentException>(() => ResolveWarmSparseBatch(scenario, scenario.Request));
    }

    [Fact]
    public void Generate_CadAllocationMode_WhenSubMicroPolygonChangeFlipsAllocation_UsesCurrentGeometry()
    {
        var grid = CreateSubMicroGrid();
        var (leftAnchor, rightAnchor) = CreateSubMicroAnchors(grid);
        var settings = CreateSubMicroSettings();

        AssertRefreshesMemo(grid, settings, leftAnchor, rightAnchor, _expectedSubMicroRightAnchorRows);
        AssertRefreshesMemo(grid, settings, rightAnchor, leftAnchor, _expectedSubMicroLeftAnchorRows);
        AssertEquivalentVertexOrderingsShareOnePayload(grid, settings, leftAnchor);

        AssertVersionProjection(grid, settings, leftAnchor, rightAnchor, NotchAlgorithmVersion.V21, _expectedSubMicroRightAnchorRows[1]);
        AssertVersionProjection(grid, settings, leftAnchor, rightAnchor, NotchAlgorithmVersion.V22, _expectedSubMicroRightAnchorRows[0]);
    }

    private static void AssertProjectionScenario(
        CadPadSet cad,
        bool linkVersionThresholds,
        int thresholdQ7,
        double thresholdPercentV22,
        string[] expectedV21Rows,
        string[] expectedV22Rows,
        string[] expectedCombinedRows)
    {
        var grid = CreateGrid();
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Pads[0].Id;
        grid.Pads[0].MatchScore = 1.0;
        var generator = new NotchTableGenerator();

        var resolutionProgress = new List<NotchGenerationProgress>();
        var batch = generator.ResolveCadAllocationBatch(
            cad,
            grid,
            CreateSettings(NotchAlgorithmVersion.V21),
            new CallbackProgress<NotchGenerationProgress>(resolutionProgress.Add));
        var v21 = NotchTableGenerator.ProjectCadAllocationResolvedBatch(
            batch,
            CreateSettings(NotchAlgorithmVersion.V21),
            includeResolutionTimings: true);
        var warmProgress = new List<NotchGenerationProgress>();
        var v22 = NotchTableGenerator.ProjectCadAllocationResolvedBatch(
            batch,
            CreateSettings(NotchAlgorithmVersion.V22),
            new CallbackProgress<NotchGenerationProgress>(warmProgress.Add));
        var combined = NotchTableGenerator.ProjectCadAllocationResolvedBatch(
            batch,
            CreateSettings(NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22));
        var reversed = NotchTableGenerator.ProjectCadAllocationResolvedBatch(
            batch,
            CreateSettings(NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21));

        Assert.Equal(1, v21.GenerationPhaseTimings.CandidateBreakdown?.CandidateCount);
        Assert.Equal(expectedV21Rows, v21.Rows.Select(BuildRowSnapshot));
        Assert.Equal(expectedV22Rows, v22.Rows.Select(BuildRowSnapshot));
        Assert.Equal(expectedCombinedRows, combined.Rows.Select(BuildRowSnapshot));
        Assert.Equal(expectedCombinedRows, reversed.Rows.Select(BuildRowSnapshot));
        var warmPhases = warmProgress
            .Select(static snapshot => $"{snapshot.PhaseStep}/{snapshot.PhaseStepCount}:{snapshot.Phase}")
            .Distinct();
        Assert.Equal(
            ["1/4:Build allocation profiles", "2/4:Build canonical candidates", "3/4:Merge candidates by diff"],
            resolutionProgress
                .Select(static snapshot => $"{snapshot.PhaseStep}/{snapshot.PhaseStepCount}:{snapshot.Phase}")
                .Distinct());
        Assert.Equal("4/4:Finalize canonical exports", Assert.Single(warmPhases));
        Assert.Equal(NotchCandidatePhaseTimings.Empty, v22.GenerationPhaseTimings.CandidateBreakdown);

        ProjectSettings CreateSettings(params NotchAlgorithmVersion[] enabledVersions) => new()
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = new HashSet<NotchAlgorithmVersion>(enabledVersions),
                LinkVersionThresholds = linkVersionThresholds,
                ThresholdQ7 = thresholdQ7,
                ThresholdPercentV22 = thresholdPercentV22,
            },
        };
    }

    private sealed record WarmSparseScenario(
        RegularGrid Grid,
        CadPadSet Cad,
        ProjectSettings Settings,
        HashSet<int> ActiveRegularPadIds,
        CadPad Target,
        RegularPad Anchor,
        NotchV22ResolvedResult WarmResolved,
        NotchTableGenerator.CadAllocationSparseResultRequest Request,
        NotchTableGenerator Generator);

    private static WarmSparseScenario CreateWarmSparseScenario()
    {
        var grid = CreateGrid(rows: 1, cols: 2);
        var cad = CreateCadWithBounds(0, 0, 15, 10);
        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Pads[0].Id;
            regular.MatchScore = 1.0;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = true,
                MultiOwnerStrictOverlapPercent = 50.0,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        };
        var target = cad.Pads[0];
        var anchor = grid.Pads[0];
        var activeRegularPadIds = grid.Pads
            .Select(static regular => regular.RegularPadId)
            .ToHashSet();
        var compensation = NotchV22CompensationService.Compute(
            target,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: cad.Pads,
            activeRegularPadIds: activeRegularPadIds,
            strictOverlapRatioOverride: 0.5);
        var warmResolved = NotchV22ResolvedResultService.Build(
            target,
            compensation,
            strictOverlapRatio: 0.5,
            anchorIcIndex: anchor.IcIndex,
            anchorDiffIndex: anchor.DiffIndex,
            allocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage,
            identity: NotchV22ResolvedResultService.CreateIdentity(
                target,
                grid,
                cad.Pads,
                activeRegularPadIds,
                settings.Notch.CompensationModel,
                enableToRegular: true,
                enableToFull: true,
                enableToFullRuleEngine: settings.Notch.EnableToFullRuleEngine,
                enableToFullRuleTrace: settings.Notch.EnableToFullRuleTrace,
                enableBoundaryVirtualAreaCap: settings.Notch.EnableBoundaryVirtualAreaCap,
                boundaryVirtualAreaCapRatio: settings.Notch.BoundaryVirtualAreaCapRatio,
                strictOverlapRatio: 0.5,
                anchorIcIndex: anchor.IcIndex,
                anchorDiffIndex: anchor.DiffIndex,
                allocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage));
        var request = new NotchTableGenerator.CadAllocationSparseResultRequest(
            target.Id,
            anchor.IcIndex,
            anchor.DiffIndex,
            warmResolved);
        return new WarmSparseScenario(
            grid, cad, settings, activeRegularPadIds, target, anchor, warmResolved, request, new NotchTableGenerator());
    }

    private static NotchTableGenerator.CadAllocationResolvedBatch ResolveWarmSparseBatch(
        WarmSparseScenario scenario,
        NotchTableGenerator.CadAllocationSparseResultRequest request) =>
        scenario.Generator.ResolveCadAllocationBatch(
            scenario.Cad,
            scenario.Grid,
            scenario.Settings,
            activeRegularPadIds: scenario.ActiveRegularPadIds,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [scenario.Target.Id] = scenario.Anchor.DiffIndex },
            selectedSparseResultRequest: request);

    private static void AssertWarmResultIsSharedAndProjectsLikeFreshGenerate(WarmSparseScenario scenario)
    {
        var (grid, cad, settings, activeRegularPadIds, target, anchor, warmResolved, request, _) = scenario;
        var batch = ResolveWarmSparseBatch(scenario, request);

        var sharedResult = Assert.IsType<NotchTableGenerator.CadAllocationSparseResult>(batch.SelectedSparseResult);
        Assert.Equal((target.Id, anchor.IcIndex, anchor.DiffIndex),
            (sharedResult.CadPadId, sharedResult.AnchorIcIndex, sharedResult.AnchorDiffIndex));
        Assert.Same(warmResolved, sharedResult.ResolvedResult);
        var projected = NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings);
        var fresh = new NotchTableGenerator().Generate(
            cad,
            grid,
            settings,
            activeRegularPadIds: activeRegularPadIds,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int>
            {
                [target.Id] = anchor.DiffIndex,
            });
        Assert.Equal(fresh.Rows.Select(BuildRowSnapshot), projected.Rows.Select(BuildRowSnapshot));
    }

    private static void AssertMismatchedWarmIdentitiesAreRejected(WarmSparseScenario scenario)
    {
        var warmResolved = scenario.WarmResolved;
        foreach (var mismatchedIdentity in new[]
                 {
                     warmResolved.Identity! with { CadSignature = warmResolved.Identity.CadSignature + "|other" },
                     warmResolved.Identity! with { GridSignature = warmResolved.Identity.GridSignature + "|other" },
                     warmResolved.Identity! with { CadPoolSignature = warmResolved.Identity.CadPoolSignature + "|collision" },
                     warmResolved.Identity! with { ActiveRegularPadIdsSignature = "collision" },
                     warmResolved.Identity! with { CompensationModel = NotchCompensationModel.ConservativeNoGain },
                     warmResolved.Identity! with { AnchorDiffIndex = scenario.Anchor.DiffIndex + 1 },
                 })
        {
            var mismatched = warmResolved with { Identity = mismatchedIdentity };
            Assert.Throws<ArgumentException>(() => ResolveWarmSparseBatch(
                scenario,
                scenario.Request with { ReusableResult = mismatched }));
        }
    }

    private static RegularGrid CreateSubMicroGrid()
    {
        var grid = CreateGrid(rows: 1, cols: 2, cellWidth: 5, cellHeight: 10);
        grid.Pads[0].DiffIndex = 10;
        grid.Pads[1].DiffIndex = 20;
        foreach (var regular in grid.Pads)
        {
            regular.IcIndex = 0;
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = 7;
            regular.MatchScore = 1.0;
        }

        return grid;
    }

    private static (CadPad Left, CadPad Right) CreateSubMicroAnchors(RegularGrid grid)
    {
        var leftAnchor = new CadPad(7, "LeftAnchor", "PAD", new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(5.0000004, 5),
            new Point2(0, 10),
        }));
        var rightAnchor = new CadPad(7, "RightAnchor", "PAD", new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(4.9999996, 5),
            new Point2(0, 10),
        }));
        Assert.Equal(75.0, leftAnchor.Area, 6);
        Assert.Equal(75.0, rightAnchor.Area, 6);
        Assert.Equal(leftAnchor.Bounds, rightAnchor.Bounds);
        Assert.Equal(
            CadPadGeometrySignature.Build(leftAnchor.Polygon),
            CadPadGeometrySignature.Build(rightAnchor.Polygon));
        Assert.Equal(37.500001, Polygon2.IntersectionAreaWithRect(leftAnchor.Polygon, grid.Pads[0].Bounds), 6);
        Assert.Equal(37.499999, Polygon2.IntersectionAreaWithRect(leftAnchor.Polygon, grid.Pads[1].Bounds), 6);
        Assert.Equal(37.499999, Polygon2.IntersectionAreaWithRect(rightAnchor.Polygon, grid.Pads[0].Bounds), 6);
        Assert.Equal(37.500001, Polygon2.IntersectionAreaWithRect(rightAnchor.Polygon, grid.Pads[1].Bounds), 6);
        return (leftAnchor, rightAnchor);
    }

    private static ProjectSettings CreateSubMicroSettings() => new()
    {
        Notch = new NotchSettings
        {
            ComputationMode = NotchComputationMode.CadAllocation,
            CompensationModel = NotchCompensationModel.CurrentGain,
            EnableToRegular = true,
            EnableToFull = false,
            NullValue = 65535,
            ThresholdQ7 = 0,
            ThresholdPercentV22 = 0,
            EnabledVersions = new HashSet<NotchAlgorithmVersion>
            {
                NotchAlgorithmVersion.V21,
                NotchAlgorithmVersion.V22,
            },
        },
    };

    private static void AssertEquivalentVertexOrderingsShareOnePayload(
        RegularGrid grid,
        ProjectSettings settings,
        CadPad leftAnchor)
    {
        var vertices = leftAnchor.Polygon.Vertices;
        var cyclic = new CadPad(
            7,
            "Cyclic",
            "PAD",
            new Polygon2(vertices.Skip(2).Concat(vertices.Take(2))));
        var reversed = new CadPad(7, "Reversed", "PAD", new Polygon2(vertices.Reverse()));
        var canonicalGenerator = new NotchTableGenerator();
        var canonical = BuildPayloadSnapshots(
            canonicalGenerator.Generate(new CadPadSet(new[] { leftAnchor }), grid, settings));
        var cyclicActual = BuildPayloadSnapshots(
            canonicalGenerator.Generate(new CadPadSet(new[] { cyclic }), grid, settings));
        var reversedActual = BuildPayloadSnapshots(
            canonicalGenerator.Generate(new CadPadSet(new[] { reversed }), grid, settings));

        Assert.Equal(_expectedSubMicroLeftAnchorRows, canonical);
        Assert.Equal(canonical, cyclicActual);
        Assert.Equal(canonical, reversedActual);
    }

    private static void AssertRefreshesMemo(
        RegularGrid grid,
        ProjectSettings settings,
        CadPad warm,
        CadPad current,
        string[] expectedCurrent)
    {
        var generator = new NotchTableGenerator();
        var warmRows = BuildPayloadSnapshots(generator.Generate(new CadPadSet(new[] { warm }), grid, settings));
        var actual = BuildPayloadSnapshots(generator.Generate(new CadPadSet(new[] { current }), grid, settings));
        var fresh = BuildPayloadSnapshots(
            new NotchTableGenerator().Generate(new CadPadSet(new[] { current }), grid, settings));

        Assert.Equal(expectedCurrent, actual);
        Assert.Equal(fresh, actual);
        Assert.False(warmRows.SequenceEqual(actual));
    }

    private static void AssertVersionProjection(
        RegularGrid grid,
        ProjectSettings settings,
        CadPad leftAnchor,
        CadPad rightAnchor,
        NotchAlgorithmVersion version,
        string expectedCurrent)
    {
        settings.Notch.EnabledVersions = new HashSet<NotchAlgorithmVersion> { version };
        var generator = new NotchTableGenerator();
        _ = generator.Generate(new CadPadSet(new[] { leftAnchor }), grid, settings);
        var actual = Assert.Single(BuildPayloadSnapshots(
            generator.Generate(new CadPadSet(new[] { rightAnchor }), grid, settings)));

        Assert.Equal(expectedCurrent, actual);
    }
}
