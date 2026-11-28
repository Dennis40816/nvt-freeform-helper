// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDisabledVersionsBehaviorTests
{
    [Theory]
    [InlineData(NotchComputationMode.CadAllocation)]
    [InlineData(NotchComputationMode.LegacyRegularAnchor)]
    public void Generate_ClearedVersions_ReturnsNoRowsAndReportsZeroWorkUntilAVersionIsEnabled(
        NotchComputationMode mode)
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid) = CreateMatchedCell();
        var settings = CreateSettings(mode);
        settings.Notch.EnabledVersions.Clear();
        var reports = new List<NotchGenerationProgress>();
        var generator = new NotchTableGenerator();

        var table = generator.Generate(cad, grid, settings, new DisabledVersionProgress(reports));
        var withoutProgress = generator.Generate(cad, grid, settings);

        Assert.Empty(table.Rows);
        Assert.False(table.ToFullCoverageAudit.HasExpectations);
        Assert.Empty(withoutProgress.Rows);
        Assert.Equal(new NotchGenerationProgress(0, 0, 0, null, null), Assert.Single(reports));
        Assert.Empty(settings.Notch.EnabledVersions);

        settings.Notch.EnabledVersions.Add(NotchAlgorithmVersion.V21);

        var resumedRow = Assert.Single(generator.Generate(cad, grid, settings).Rows);
        Assert.Equal(NotchAlgorithmVersion.V21, resumedRow.Version);
        Assert.Equal(cad.Pads[0].Id, resumedRow.CadPadId);
        Assert.Equal(grid.Pads[0].DiffIndex, resumedRow.DiffIndex);
    }

    [Theory]
    [InlineData(NotchComputationMode.CadAllocation)]
    [InlineData(NotchComputationMode.LegacyRegularAnchor)]
    public void EvaluateCadRowEligibility_ClearedVersions_ExplainsMissingRowsWithoutSelectingAnAnchor(
        NotchComputationMode mode)
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid) = CreateMatchedCell();
        var settings = CreateSettings(mode);
        settings.Notch.EnabledVersions.Clear();

        var eligibility = new NotchTableGenerator().EvaluateCadRowEligibility(cad.Pads[0], grid, settings);

        Assert.Equal(cad.Pads[0].Id, eligibility.CadPadId);
        Assert.False(eligibility.HasRows);
        Assert.Empty(eligibility.EligibleVersions);
        Assert.Equal(0, eligibility.EstimatedRowCount);
        Assert.Null(eligibility.AnchorRegularPadId);
        Assert.Equal("No notch version enabled.", eligibility.Reason);
        Assert.Empty(settings.Notch.EnabledVersions);
    }

    private static (CadPadSet Cad, RegularGrid Grid) CreateMatchedCell()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, 10, 10);
        var cad = new CadPad(42, "Signal", "PAD", grid.Pads[0].Polygon);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Id;
        grid.Pads[0].MatchScore = 1d;
        return (new CadPadSet([cad]), grid);
    }

    private static ProjectSettings CreateSettings(NotchComputationMode mode) => new()
    {
        Notch = new NotchSettings
        {
            ComputationMode = mode,
            EnabledVersions = [NotchAlgorithmVersion.V21],
        },
    };

    private sealed class DisabledVersionProgress(List<NotchGenerationProgress> reports) : IProgress<NotchGenerationProgress>
    {
        public void Report(NotchGenerationProgress value) => reports.Add(value);
    }
}
