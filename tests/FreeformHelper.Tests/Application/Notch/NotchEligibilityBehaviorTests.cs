// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchEligibilityBehaviorTests
{
    [Theory]
    [InlineData(NotchComputationMode.CadAllocation, 20d, FreeformType.XWay, 7, "No regular overlap.")]
    [InlineData(NotchComputationMode.LegacyRegularAnchor, 0d, FreeformType.None, 7, "No matched freeform regular.")]
    [InlineData(NotchComputationMode.LegacyRegularAnchor, 0d, FreeformType.XWay, null, "No matched freeform regular.")]
    public void EvaluateEligibility_NoCandidates_ExplainsWhyNoRowsAreAvailable(
        NotchComputationMode mode, double cadMinX, FreeformType freeform, int? matchedCadId, string expectedReason)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, 10, 10);
        var cad = TestGeometryFactory.CreateCadPad(7, "PAD", cadMinX, 0, cadMinX + 10, 10);
        grid.Pads[0].Freeform = freeform;
        grid.Pads[0].MatchedCadPadId = matchedCadId;

        var result = new NotchTableGenerator().EvaluateCadRowEligibility(cad, grid, CreateSettings(mode));

        Assert.Equal(cad.Id, result.CadPadId);
        Assert.False(result.HasRows);
        Assert.Empty(result.EligibleVersions);
        Assert.Equal(0, result.EstimatedRowCount);
        Assert.Null(result.AnchorRegularPadId);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Theory]
    [InlineData(NotchComputationMode.CadAllocation, 1,
        "Anchor REG0, freeform=XWay.", "v2.1: below gate TH; v2.2: below gate TH")]
    [InlineData(NotchComputationMode.LegacyRegularAnchor, 2,
        "Legacy mode with 2 matched freeform regular(s).", "No eligible strategy after gate/can-handle checks.")]
    public void EvaluateEligibility_IndependentVersionGates_AdmitTheBoundaryAndCountRows(
        NotchComputationMode mode, int expectedRows, string eligibleReason, string blockedReason)
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, 10, 10, diffIndexSelector: (_, col) => col);
        var cad = TestGeometryFactory.CreateCadPad(7, "PAD", 0, 0, 20, 10);
        foreach (var pad in grid.Pads)
        {
            pad.Freeform = FreeformType.XWay;
            pad.MatchedCadPadId = cad.Id;
            pad.MatchScore = 1;
        }

        var settings = CreateSettings(mode);
        settings.Notch.ThresholdQ7 = 64;
        settings.Notch.ThresholdPercentV22 = 51;
        var generator = new NotchTableGenerator();

        var v21 = generator.EvaluateCadRowEligibility(cad, grid, settings);

        Assert.Equal(new[] { NotchAlgorithmVersion.V21 }, v21.EligibleVersions);
        Assert.True(v21.HasRows);
        Assert.Equal(expectedRows, v21.EstimatedRowCount);
        Assert.Equal(0, v21.AnchorRegularPadId);
        Assert.Equal(eligibleReason, v21.Reason);

        settings.Notch.ThresholdQ7 = 65;
        settings.Notch.ThresholdPercentV22 = 50;

        var v22 = generator.EvaluateCadRowEligibility(cad, grid, settings);

        Assert.Equal(new[] { NotchAlgorithmVersion.V22 }, v22.EligibleVersions);
        Assert.True(v22.HasRows);
        Assert.Equal(expectedRows, v22.EstimatedRowCount);
        Assert.Equal(0, v22.AnchorRegularPadId);
        Assert.Equal(eligibleReason, v22.Reason);

        settings.Notch.ThresholdPercentV22 = 51;

        var blocked = generator.EvaluateCadRowEligibility(cad, grid, settings);

        Assert.False(blocked.HasRows);
        Assert.Empty(blocked.EligibleVersions);
        Assert.Equal(0, blocked.EstimatedRowCount);
        Assert.Equal(0, blocked.AnchorRegularPadId);
        Assert.Equal(blockedReason, blocked.Reason);
    }

    [Fact]
    public void EvaluateEligibility_LegacyXyWithOnlyV22_ExplainsThatNoStrategyCanHandleIt()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, 10, 10);
        var cad = TestGeometryFactory.CreateCadPad(7, "PAD", 0, 0, 10, 10);
        grid.Pads[0].Freeform = FreeformType.XYWay;
        grid.Pads[0].MatchedCadPadId = cad.Id;
        grid.Pads[0].MatchScore = 1;
        var settings = CreateSettings(NotchComputationMode.LegacyRegularAnchor);
        settings.Notch.EnabledVersions = [NotchAlgorithmVersion.V22];

        var result = new NotchTableGenerator().EvaluateCadRowEligibility(cad, grid, settings);

        Assert.False(result.HasRows);
        Assert.Empty(result.EligibleVersions);
        Assert.Equal(0, result.EstimatedRowCount);
        Assert.Equal(0, result.AnchorRegularPadId);
        Assert.Equal("No eligible strategy after gate/can-handle checks.", result.Reason);
    }

    private static ProjectSettings CreateSettings(NotchComputationMode mode) => new()
    {
        Notch = new NotchSettings
        {
            ComputationMode = mode,
            EnabledVersions = [NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22],
            LinkVersionThresholds = false,
            ThresholdQ7 = 0,
            ThresholdPercentV22 = 0,
        },
    };
}
