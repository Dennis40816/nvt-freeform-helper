// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchRowGenerationCharacterizationTests
{
    [Theory]
    [InlineData(104, 100)]
    [InlineData(90, 90)]
    public void ContinuationPreservesTheGuardedAnchorBudget(int cap, int retained)
    {
        var (cad, grid, settings) = NotchRowExplanationFixture.Create(cap);

        var table = new NotchTableGenerator().Generate(cad, grid, settings);

        Assert.Equal(2, table.Rows.Count);
        var main = Assert.IsType<NotchV22Node>(table.Rows[0].V22Node);
        var continuation = Assert.IsType<NotchV22Node>(table.Rows[1].V22Node);
        Assert.Equal(0, main.Flags);
        Assert.Equal(1, continuation.Flags);
        Assert.Equal(100, continuation.CombinePercent);
        Assert.Equal(retained, main.CombinePercent - main.TargetRatioPercent1 - main.TargetRatioPercent2);
        Assert.Equal(8, continuation.TargetRatioPercent1);
    }
}

internal static class NotchRowExplanationFixture
{
    public static (CadPadSet Cad, RegularGrid Grid, ProjectSettings Settings) Create(int cap = 90)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(2, 2, 10, 10,
            diffIndexSelector: static (row, col) => (row * 2) + col,
            icIndexSelector: static (_, _) => 0);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = 1;
        grid.Pads[0].MatchScore = 1.0;
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 12, 12)]);
        var settings = NotchHolePanelFixture.CreateSettings(capsEnabled: true);
        settings.Notch.TargetCoverageCapPercent = cap;
        return (cad, grid, settings);
    }
}
