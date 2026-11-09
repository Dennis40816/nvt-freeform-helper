// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22GroupedTargetGenerationBehaviorTests
{
    [Fact]
    public void Generate_GroupedTargetAboveOneHundredPercent_PreservesEveryChunkAcrossContinuationRows()
    {
        using var culture = TestCulture.UseEnUs();
        int[] diffIndices = [10, 20, 20, 30];
        var grid = TestGeometryFactory.CreateRegularGrid(
            rows: 1, cols: 4, cellWidth: 10, cellHeight: 10, diffIndexSelector: (_, col) => diffIndices[col]);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, 40, 6)]);
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
                EnableToRegular = true,
                EnableToFull = false,
                EnableTargetCoverageGuard = false,
                EnabledVersions = [NotchAlgorithmVersion.V22],
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 0,
            },
        };

        var table = new NotchTableGenerator().Generate(cad, grid, settings);

        Assert.Collection(
            table.Rows,
            main => Assert.Equal(new NotchV22Node(10, 240, 20, 100, 30, 60, 0), main.V22Node),
            continuation => Assert.Equal(new NotchV22Node(10, 100, 20, 20, 65535, 0, 1), continuation.V22Node));
        Assert.All(table.Rows, row =>
        {
            Assert.Equal(NotchAlgorithmVersion.V22, row.Version);
            Assert.Equal(0, row.IcIndex);
            Assert.Equal(10, row.DiffIndex);
            Assert.Equal(0, row.RegularPadIndex);
            Assert.Equal(100, row.CadPadId);
        });
    }
}
