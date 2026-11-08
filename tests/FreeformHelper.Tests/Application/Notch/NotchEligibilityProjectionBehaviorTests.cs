// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchEligibilityProjectionBehaviorTests
{
    private static readonly NotchAlgorithmVersion[] _v22Only = [NotchAlgorithmVersion.V22];

    [Fact]
    public void EvaluateEligibility_LegacyMixedFreeforms_UsesLargestOverlapForGateButCountsOnlyCompatibleRows()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, 10, 10, diffIndexSelector: (_, col) => col);
        var cad = TestGeometryFactory.CreateCadPad(7, "PAD", 5, 0, 20, 10);
        foreach (var pad in grid.Pads)
        {
            pad.MatchedCadPadId = cad.Id;
            pad.MatchScore = 1;
        }

        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[1].Freeform = FreeformType.XYWay;
        var settings = CreateSettings(NotchComputationMode.LegacyRegularAnchor);
        settings.Notch.ThresholdPercentV22 = 60;
        var generator = new NotchTableGenerator();

        var eligibility = generator.EvaluateCadRowEligibility(cad, grid, settings);
        var table = generator.Generate(new CadPadSet([cad]), grid, settings);

        Assert.True(eligibility.HasRows);
        Assert.Equal(_v22Only, eligibility.EligibleVersions);
        Assert.Equal(1, eligibility.EstimatedRowCount);
        Assert.Equal(1, eligibility.AnchorRegularPadId);
        Assert.Equal("Legacy mode with 2 matched freeform regular(s).", eligibility.Reason);
        var row = Assert.Single(table.Rows);
        Assert.Equal(NotchAlgorithmVersion.V22, row.Version);
        Assert.Equal(cad.Id, row.CadPadId);
        Assert.Equal(0, row.RegularPadIndex);
    }

    private static ProjectSettings CreateSettings(NotchComputationMode mode) => new()
    {
        Notch = new NotchSettings
        {
            ComputationMode = mode,
            EnabledVersions = [NotchAlgorithmVersion.V22],
            LinkVersionThresholds = false,
            ThresholdPercentV22 = 0,
        },
    };
}
