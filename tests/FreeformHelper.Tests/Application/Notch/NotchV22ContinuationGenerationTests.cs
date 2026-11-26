// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22ContinuationGenerationTests
{
    [Fact]
    public void TwoLegsPreserveTheExistingPayload()
    {
        var (cad, grid, settings) = CreateScenario(columns: 3);

        var table = new NotchTableGenerator().Generate(cad, grid, settings);

        var row = Assert.Single(table.Rows);
        Assert.Equal(new NotchV22Node(0, 186, 1, 62, 2, 62, 0), row.V22Node);
    }

    [Fact]
    public void V21PreservesTheExistingMainAndContinuationPayloads()
    {
        var (cad, grid, settings) = CreateScenario(columns: 4);
        settings.Notch.EnabledVersions = [NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22];
        var generator = new NotchTableGenerator();

        var combined = generator.Generate(cad, grid, settings);
        var v21Rows = combined.Rows.Where(static row => row.Version == NotchAlgorithmVersion.V21).ToArray();
        settings.Notch.EnabledVersions = [NotchAlgorithmVersion.V21];
        var v21Only = generator.Generate(cad, grid, settings);

        Assert.Collection(v21Rows,
            main => Assert.Equal([0, 248, 248, 1, 1, 79, 2, 1, 79], main.Values),
            continuation => Assert.Equal([0, 100, 100, 3, 1, 79, 65535, 0, 0], continuation.Values));
        Assert.Equal(v21Rows.Select(static row => row.Values), v21Only.Rows.Select(static row => row.Values));
        Assert.Equal(v21Rows.Select(static row => row.Comment), v21Only.Rows.Select(static row => row.Comment));
    }

    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain)]
    [InlineData(NotchCompensationModel.ConservativeNoGain)]
    public void ThreeLegsLeaveOnlyTheRoundedAnchorCoverageOnTheSource(NotchCompensationModel model)
    {
        var (cad, grid, settings) = CreateScenario(columns: 4);
        settings.Notch.CompensationModel = model;

        var table = new NotchTableGenerator().Generate(cad, grid, settings);

        Assert.Collection(table.Rows,
            main => Assert.Equal(new NotchV22Node(0, 186, 1, 62, 2, 62, 0), main.V22Node),
            continuation => Assert.Equal(new NotchV22Node(0, 100, 3, 62, 65535, 0, 1), continuation.V22Node));
        var mainNode = Assert.IsType<NotchV22Node>(table.Rows[0].V22Node);
        var firmwareRetained = Math.Max(0, mainNode.CombinePercent - mainNode.TargetRatioPercent1 - mainNode.TargetRatioPercent2);
        // Every regular has 62.5% overlap, rounded to 62% before row encoding.
        Assert.Equal(62, firmwareRetained);
    }

    private static (CadPadSet Cad, RegularGrid Grid, ProjectSettings Settings) CreateScenario(int columns)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(
            1, columns, cellWidth: 10, cellHeight: 10, diffIndexSelector: static (_, col) => col);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, columns * 10, 6.25)]);
        foreach (var pad in grid.Pads)
        {
            pad.MatchedCadPadId = 100;
            pad.MatchScore = 1;
            pad.Freeform = FreeformType.XWay;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToFull = false,
                EnableTargetCoverageGuard = false,
                EnabledVersions = [NotchAlgorithmVersion.V22],
                LinkVersionThresholds = false,
                ThresholdQ7 = 0,
                ThresholdPercentV22 = 0,
            },
        };
        return (cad, grid, settings);
    }
}
