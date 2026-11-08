// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableGeneratorTests
{
    [Fact]
    public void GenerateV22CoverageGuardZeroCapPreservesTheUnguardedTransfers()
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid, settings) = CreateV22CoverageGuardScenario(capPercent: 0);
        var generator = new NotchTableGenerator();

        var zeroCap = Assert.Single(generator.Generate(cad, grid, settings).Rows);
        settings.Notch.EnableTargetCoverageGuard = false;
        var unguarded = Assert.Single(generator.Generate(cad, grid, settings).Rows);

        Assert.Equal(new NotchV22Node(0, 150, 1, 50, 65535, 0, 0), zeroCap.V22Node);
        Assert.Equal(unguarded.V22Node, zeroCap.V22Node);
        Assert.Equal(unguarded.Comment, zeroCap.Comment);
    }

    [Fact]
    public void GenerateV22CoverageGuardRemappedSourceWithNoRetainedAreaScalesOnlyTheOverCapTarget()
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid, settings) = CreateV22CoverageGuardScenario(capPercent: 60, columns: 3);
        settings.Notch.EnabledVersions = [NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22];
        var outputDiffs = new Dictionary<int, int> { [cad.Pads[0].Id] = 2 };

        var table = new NotchTableGenerator().Generate(
            cad, grid, settings, cadOutputFwDiffIndexByCadId: outputDiffs);

        var v22 = Assert.Single(table.Rows, row => row.Version == NotchAlgorithmVersion.V22);
        var v21 = Assert.Single(table.Rows, row => row.Version == NotchAlgorithmVersion.V21);
        Assert.Equal(0, v22.RegularPadIndex);
        Assert.Equal(2, v22.DiffIndex);
        Assert.Equal(new NotchV22Node(2, 110, 0, 60, 1, 50, 0), v22.V22Node);
        Assert.Equal([2, 110, 110, 0, 1, 77, 1, 1, 64], v21.Values);
    }

    [Fact]
    public void GenerateV22CoverageGuardSharedTargetFloorsFractionalCoverageAndRemovesZeroTransfer()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = CreateGrid(cols: 2, cellWidth: 100);
        var cad = new CadPadSet(
        [
            TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 101, 10),
            TestGeometryFactory.CreateCadPad(2, "PAD", 101, 0, 200, 10),
        ]);
        MatchV22CoverageGuardPad(grid.Pads[0], cadPadId: 1);
        MatchV22CoverageGuardPad(grid.Pads[1], cadPadId: 2);
        var settings = CreateV22CoverageGuardSettings(capPercent: 50);
        var generator = new NotchTableGenerator();
        settings.Notch.EnableTargetCoverageGuard = false;
        var unguarded = generator.Generate(cad, grid, settings);
        Assert.Equal(new NotchV22Node(0, 101, 1, 1, 65535, 0, 0), unguarded.Rows[0].V22Node);
        Assert.Equal(new NotchV22Node(1, 99, 65535, 0, 65535, 0, 0), unguarded.Rows[1].V22Node);
        settings.Notch.EnableTargetCoverageGuard = true;

        var guarded = generator.Generate(cad, grid, settings);

        Assert.Collection(
            guarded.Rows,
            row => Assert.Equal(new NotchV22Node(0, 50, 65535, 0, 65535, 0, 0), row.V22Node),
            row => Assert.Equal(new NotchV22Node(1, 49, 65535, 0, 65535, 0, 0), row.V22Node));
    }

    private static (CadPadSet Cad, RegularGrid Grid, ProjectSettings Settings) CreateV22CoverageGuardScenario(
        int capPercent, int columns = 2)
    {
        var grid = CreateGrid(cols: columns);
        var cad = CreateCadWithBounds(0, 0, 15, 10);
        foreach (var pad in grid.Pads.Take(2))
        {
            MatchV22CoverageGuardPad(pad, cad.Pads[0].Id);
        }

        return (cad, grid, CreateV22CoverageGuardSettings(capPercent));
    }

    private static void MatchV22CoverageGuardPad(RegularPad pad, int cadPadId)
    {
        pad.Freeform = FreeformType.XWay;
        pad.MatchedCadPadId = cadPadId;
        pad.MatchScore = 1.0;
    }

    private static ProjectSettings CreateV22CoverageGuardSettings(int capPercent) => new()
    {
        Notch = new NotchSettings
        {
            ComputationMode = NotchComputationMode.CadAllocation,
            CompensationModel = NotchCompensationModel.CurrentGain,
            EnableToRegular = true,
            EnableToFull = false,
            EnableTargetCoverageGuard = true,
            TargetCoverageCapPercent = capPercent,
            EnabledVersions = [NotchAlgorithmVersion.V22],
        },
    };
}
