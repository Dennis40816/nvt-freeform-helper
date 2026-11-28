// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchFullTableCharacterizationTests
{
    public static TheoryData<string, NotchComputationMode, string> Scenarios
    {
        get
        {
            string[] inputs =
                ["multi-ic-ties", "grouped-chunks", "guard-continuation", "coverage-gaps", "guard-floor", "triangle-threshold"];
            NotchComputationMode[] modes = [NotchComputationMode.CadAllocation, NotchComputationMode.LegacyRegularAnchor];
            string[] requests = ["V21", "V22", "V22-V21"];
            var scenarios = new TheoryData<string, NotchComputationMode, string>();
            foreach (var input in inputs)
            {
                foreach (var mode in modes)
                {
                    foreach (var request in requests)
                    {
                        scenarios.Add(input, mode, request);
                    }
                }
            }

            return scenarios;
        }
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Generate_AllRowsAndCoverage_MatchCheckedInBaseline(string input, NotchComputationMode mode, string request)
    {
        using var culture = TestCulture.UseEnUs();
        var scenario = CreateScenario(input);
        scenario.Settings.Notch.ComputationMode = mode;
        scenario.Settings.Notch.EnabledVersions = Versions(request);
        var generator = new NotchTableGenerator();
        var fresh = Generate(generator, scenario);
        var snapshot = NotchFullTableBaseline.Serialize(fresh);

        NotchFullTableBaseline.Equal(snapshot, NotchFullTableBaseline.Serialize(Generate(generator, scenario)), "repeat Generate");
        if (mode == NotchComputationMode.CadAllocation)
        {
            AssertCadAllocationRoutes(scenario, snapshot);
            if (input == "coverage-gaps")
            {
                Assert.True(fresh.ToFullCoverageAudit.HasMissingCoverage);
                Assert.Contains(fresh.ToFullCoverageAudit.MissingGaps, static gap => gap.CadPadIds.Count > 1);
            }
        }

        NotchFullTableBaseline.Match($"{input}-{mode}-{request}.txt", snapshot);
    }

    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain, "V21")]
    [InlineData(NotchCompensationModel.CurrentGain, "V22")]
    [InlineData(NotchCompensationModel.CurrentGain, "V22-V21")]
    [InlineData(NotchCompensationModel.Disabled, "V21")]
    [InlineData(NotchCompensationModel.Disabled, "V22")]
    [InlineData(NotchCompensationModel.Disabled, "V22-V21")]
    public void Project_SignedSparseDiagnostics_MatchCheckedInBaseline(NotchCompensationModel model, string request)
    {
        using var culture = TestCulture.UseEnUs();
        var scenario = CreateGroupedChunks();
        scenario.Settings.Notch.CompensationModel = model;
        scenario.Settings.Notch.EnabledVersions = Versions(request);
        var seed = Resolve(new NotchTableGenerator(), scenario, scenario.Selection);
        var selected = Assert.IsType<NotchTableGenerator.CadAllocationSparseResult>(seed.SelectedSparseResult);
        var diagnostic = WithSignedTargets(selected.ResolvedResult);
        var sparseRequest = scenario.Selection with { ReusableResult = diagnostic };
        var generator = new NotchTableGenerator();
        var batch = Resolve(generator, scenario, sparseRequest);
        Assert.Same(diagnostic, batch.SelectedSparseResult?.ResolvedResult);
        var snapshot = NotchFullTableBaseline.Serialize(NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, scenario.Settings));
        var repeated = Resolve(generator, scenario, sparseRequest);
        NotchFullTableBaseline.Equal(snapshot,
            NotchFullTableBaseline.Serialize(NotchTableGenerator.ProjectCadAllocationResolvedBatch(repeated, scenario.Settings)),
            "repeat signed sparse projection");

        var boundary = model == NotchCompensationModel.CurrentGain ? "signed-split" : "signed-clamp";
        NotchFullTableBaseline.Match($"grouped-chunks-CadAllocation-{request}-{boundary}.txt", snapshot);
    }

    private static void AssertCadAllocationRoutes(Scenario scenario, string expected)
    {
        var coldBatch = Resolve(new NotchTableGenerator(), scenario);
        NotchFullTableBaseline.Equal(expected,
            NotchFullTableBaseline.Serialize(NotchTableGenerator.ProjectCadAllocationResolvedBatch(coldBatch, scenario.Settings)),
            "resolve then project");

        var generator = new NotchTableGenerator();
        var seed = Resolve(generator, scenario, scenario.Selection);
        var selected = Assert.IsType<NotchTableGenerator.CadAllocationSparseResult>(seed.SelectedSparseResult);
        NotchFullTableBaseline.Equal(expected,
            NotchFullTableBaseline.Serialize(NotchTableGenerator.ProjectCadAllocationResolvedBatch(seed, scenario.Settings)),
            "cold selected sparse projection");
        var warm = Resolve(generator, scenario, scenario.Selection with { ReusableResult = selected.ResolvedResult });
        Assert.Same(selected.ResolvedResult, warm.SelectedSparseResult?.ResolvedResult);
        NotchFullTableBaseline.Equal(expected,
            NotchFullTableBaseline.Serialize(NotchTableGenerator.ProjectCadAllocationResolvedBatch(warm, scenario.Settings)),
            "warm sparse projection");
    }

    private static NotchTable Generate(NotchTableGenerator generator, Scenario scenario) =>
        generator.Generate(scenario.Cad, scenario.Grid, scenario.Settings,
            activeRegularPadIds: scenario.ActiveRegularPadIds, cadOutputFwDiffIndexByCadId: scenario.OutputDiffs);

    private static NotchTableGenerator.CadAllocationResolvedBatch Resolve(
        NotchTableGenerator generator, Scenario scenario, NotchTableGenerator.CadAllocationSparseResultRequest? selection = null) =>
        generator.ResolveCadAllocationBatch(scenario.Cad, scenario.Grid, scenario.Settings,
            activeRegularPadIds: scenario.ActiveRegularPadIds, cadOutputFwDiffIndexByCadId: scenario.OutputDiffs,
            selectedSparseResultRequest: selection);

    private static HashSet<NotchAlgorithmVersion> Versions(string request) => request switch
    {
        "V21" => [NotchAlgorithmVersion.V21],
        "V22" => [NotchAlgorithmVersion.V22],
        "V22-V21" => [NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21],
        _ => throw new ArgumentOutOfRangeException(nameof(request)),
    };

    private static Scenario CreateScenario(string input) => input switch
    {
        "multi-ic-ties" => CreateMultiIcTies(),
        "grouped-chunks" => CreateGroupedChunks(),
        "guard-continuation" => CreateGuardContinuation(),
        "coverage-gaps" => CreateCoverageGaps(),
        "guard-floor" => CreateGuardFloor(),
        "triangle-threshold" => CreateTriangleThreshold(),
        _ => throw new ArgumentOutOfRangeException(nameof(input)),
    };

    private static ProjectSettings CreateSettings(bool toFull = false, bool guard = false, int cap = 120) => new()
    {
        Notch = new NotchSettings
        {
            ComputationMode = NotchComputationMode.CadAllocation,
            CompensationModel = NotchCompensationModel.CurrentGain,
            EnableToRegular = true,
            EnableToFull = toFull,
            EnableToFullRuleEngine = true,
            EnableToFullRuleTrace = false,
            EnableBoundaryVirtualAreaCap = true,
            BoundaryVirtualAreaCapRatio = 1.0,
            EnableTargetCoverageGuard = guard,
            TargetCoverageCapPercent = cap,
            MultiOwnerStrictOverlapPercent = 0.1,
            LinkVersionThresholds = false,
            ThresholdQ7 = 0,
            ThresholdPercentV22 = 0,
            NullValue = NotchEdgeCaseHarness.NullDiff,
            LenScale = 10,
        },
    };

    private static Scenario CreateMultiIcTies()
    {
        int[] diffs = [30, 10, 20, 0];
        var grid = TestGeometryFactory.CreateRegularGrid(2, 2, 10, 10,
            diffIndexSelector: (row, col) => diffs[(row * 2) + col], icIndexSelector: static (row, col) => row + col);
        var cad = new CadPadSet(
        [
            TestGeometryFactory.CreateCadPad(42, "PAD", 0, 0, 10, 6),
            TestGeometryFactory.CreateCadPad(90, "PAD", 100, 100, 110, 110),
            TestGeometryFactory.CreateCadPad(11, "PAD", 0, 0, 10, 6),
            TestGeometryFactory.CreateCadPad(80, "PAD", 5, 5, 5, 5),
            TestGeometryFactory.CreateCadPad(33, "PAD", 10, 10, 20, 16),
            TestGeometryFactory.CreateCadPad(22, "PAD", 0, 10, 10, 16),
            TestGeometryFactory.CreateCadPad(55, "PAD", 10, 0, 20, 6),
        ]);
        Match(grid.Pads[0], 999);
        Match(grid.Pads[1], 55, FreeformType.YWay);
        Match(grid.Pads[2], 22, FreeformType.XYWay);
        Match(grid.Pads[3], 33);
        return new Scenario(cad, grid, CreateSettings(), new(42, 0, 30));
    }

    private static Scenario CreateGroupedChunks()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 20, 20, 30], cellWidth: 10, cellHeight: 10);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, 40, 6)]);
        foreach (var pad in grid.Pads)
        {
            Match(pad, 100);
        }

        return new Scenario(cad, grid, CreateSettings(), new(100, 0, 10));
    }

    private static Scenario CreateGuardContinuation()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 2, 10, 10,
            diffIndexSelector: static (row, col) => (row * 2) + col);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 12, 12)]);
        Match(grid.Pads[0], 1);
        return new Scenario(cad, grid, CreateSettings(toFull: true, guard: true, cap: 90), new(1, 0, 0))
        {
            ActiveRegularPadIds = grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
        };
    }

    private static Scenario CreateCoverageGaps()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(3, 2, 10, 10,
            diffIndexSelector: static (row, col) => (row * 2) + col);
        var cad = new CadPadSet(
        [
            TestGeometryFactory.CreateCadPad(30, "PAD", 6, 0, 10, 22),
            TestGeometryFactory.CreateCadPad(10, "PAD", 0, 0, 12, 10),
            TestGeometryFactory.CreateCadPad(20, "PAD", 0, 0, 4, 22),
        ]);
        Match(grid.Pads[0], 10);
        return new Scenario(cad, grid, CreateSettings(toFull: true), new(10, 0, 0));
    }

    private static Scenario CreateGuardFloor()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([0, 1], cellWidth: 100, cellHeight: 10);
        var cad = new CadPadSet(
        [
            TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 101, 10),
            TestGeometryFactory.CreateCadPad(2, "PAD", 101, 0, 200, 10),
        ]);
        Match(grid.Pads[0], 1);
        Match(grid.Pads[1], 2);
        return new Scenario(cad, grid, CreateSettings(guard: true, cap: 50), new(1, 0, 0));
    }

    private static Scenario CreateTriangleThreshold()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 20], cellWidth: 10, cellHeight: 10);
        var cad = new CadPadSet([new CadPad(7, "Triangle", "PAD", new Polygon2(
            [new Point2(0, 0), new Point2(20, 0), new Point2(0, 10)]))]);
        Match(grid.Pads[0], 7);
        Match(grid.Pads[1], 7, FreeformType.YWay);
        var settings = CreateSettings();
        settings.Notch.ThresholdQ7 = 64;
        settings.Notch.ThresholdPercentV22 = 80;
        settings.Notch.NullValue = 54321;
        return new Scenario(cad, grid, settings, new(7, 0, 20))
        {
            OutputDiffs = new Dictionary<int, int> { [7] = 20 },
        };
    }

    private static void Match(RegularPad pad, int cadId, FreeformType freeform = FreeformType.XWay)
    {
        pad.MatchedCadPadId = cadId;
        pad.MatchScore = 1;
        pad.Freeform = freeform;
    }

    private static NotchV22ResolvedResult WithSignedTargets(NotchV22ResolvedResult resolved)
    {
        // Signed diagnostics are ABI inputs, not a claim that negative area is reachable from geometry.
        NotchV22TargetAllocation[] targets =
        [
            new(0, 30, 1, -2.5, -250, true, false, 1, 1, [], []),
            new(0, 20, 1, 1.01, 101, true, false, 1, 1, [], []),
            new(0, 40, 1, -1.01, -101, true, false, 1, 1, [], []),
            new(0, 50, 1, 0, 0, true, false, 0, 1, [], []),
        ];
        return resolved with
        {
            TargetAllocation = resolved.TargetAllocation with
            {
                Targets = targets,
                TargetCoverageProjection = new NotchV22TargetCoverageProjection(targets, 201, 2.01, false),
            },
        };
    }

    private sealed record Scenario(
        CadPadSet Cad,
        RegularGrid Grid,
        ProjectSettings Settings,
        NotchTableGenerator.CadAllocationSparseResultRequest Selection)
    {
        public IReadOnlySet<int>? ActiveRegularPadIds { get; init; }
        public IReadOnlyDictionary<int, int>? OutputDiffs { get; init; }
    }
}
