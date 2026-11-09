// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportTextTests
{
    [Fact]
    public void Stage2SummaryCountsDistinctPadsAndOrdersVersions()
    {
        var table = CreateMixedTable();

        var result = NotchExportText.BuildStage2RowSummary("Firmware C", table, new(0, 0, string.Empty), 5);

        Assert.Equal(
            "Firmware C export [2/5]: rows=3 | cad=2 | reg=2 | versions:v2.1=1, v2.2=2 | toFullCoverage=n/a",
            result);
    }

    [Fact]
    public void Stage2SummaryAppendsCrossIcMismatchCounts()
    {
        var table = CreateMixedTable();

        var result = NotchExportText.BuildStage2RowSummary("Firmware C", table, new(1, 2, "CAD101(IC:1/2)"), 5);

        Assert.Equal(
            "Firmware C export [2/5]: rows=3 | cad=2 | reg=2 | versions:v2.1=1, v2.2=2 | toFullCoverage=n/a | crossIC=1 cad/2 reg",
            result);
    }

    [Fact]
    public void VersionBreakdownForEmptyTableHasNoVersions()
    {
        Assert.Equal("versions=-", NotchExportText.BuildVersionBreakdownText(new NotchTable([])));
    }

    [Fact]
    public void VersionBreakdownOrdersMixedVersionsByNumber()
    {
        Assert.Equal("versions:v2.1=1, v2.2=2", NotchExportText.BuildVersionBreakdownText(CreateMixedTable()));
    }

    [Fact]
    public void ToFullCoverageWithoutExpectationsIsNotApplicable()
    {
        Assert.Equal("toFullCoverage=n/a", NotchExportText.BuildToFullCoverageStatusText(NotchToFullCoverageAudit.Empty));
    }

    [Fact]
    public void ToFullCoverageWithMissingTargetsShowsCoveredAndExpectedCounts()
    {
        var audit = new NotchToFullCoverageAudit(1, 3, 2, [new(0, 10, 11, [101])]);

        Assert.Equal("toFullCoverage=miss 2/3", NotchExportText.BuildToFullCoverageStatusText(audit));
    }

    [Fact]
    public void ToFullCoverageWithAllTargetsCoveredIsOk()
    {
        var audit = new NotchToFullCoverageAudit(1, 3, 3, []);

        Assert.Equal("toFullCoverage=ok 3/3", NotchExportText.BuildToFullCoverageStatusText(audit));
    }

    [Theory]
    [InlineData(null, 1, 4, "Generate: Scan CAD 1/4, rows=3")]
    [InlineData(" ", 1, 4, "Generate: Scan CAD 1/4, rows=3")]
    [InlineData("Project rows", 8, 4, "Generate: Project rows 4/4, rows=3")]
    [InlineData("Scan CAD", 8, 0, "Generate: Scan CAD 0/0, rows=3")]
    public void Step5ProgressUsesDefaultPhaseAndClampsCounts(string? phase, int processed, int total, string expected)
    {
        var progress = new NotchGenerationProgress(processed, total, 3, null, null, phase);

        Assert.Equal(expected, NotchExportText.BuildStep5GenerationProgressText("Generate", progress));
    }

    [Theory]
    [InlineData(0, 4, 1, 1, 0.0)]
    [InlineData(4, 4, 1, 1, 1.0)]
    [InlineData(2, 4, 2, 3, 0.5)]
    [InlineData(0, 0, 0, 0, 0.0)]
    [InlineData(1, 0, 0, 0, 1.0)]
    public void PhaseFractionHandlesSingleAndMultiplePhasesAndZeroTotals(
        int processed, int total, int phaseStep, int phaseStepCount, double expected)
    {
        var progress = new NotchGenerationProgress(processed, total, 0, null, null, null, phaseStep, phaseStepCount);

        Assert.Equal(expected, NotchExportText.ComputeGenerationPhaseFraction(progress));
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, "C", "notch_v2.1")]
    [InlineData(NotchAlgorithmVersion.V22, ".c", "notch_v2.2")]
    [InlineData(NotchAlgorithmVersion.V22, "csv", "requested")]
    public void SuggestedBaseNameUsesSingleVersionOnlyForC(NotchAlgorithmVersion version, string extension, string expected)
    {
        var table = new NotchTable([CreateRow(version, 10, 101)]);

        Assert.Equal(expected, NotchExportText.ResolveSuggestedNotchExportBaseName("requested", extension, table));
    }

    [Fact]
    public void SuggestedBaseNameKeepsRequestedNameForMixedVersions()
    {
        Assert.Equal("requested", NotchExportText.ResolveSuggestedNotchExportBaseName("requested", "c", CreateMixedTable()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SuggestedBaseNameRejectsMissingRequestedName(string? requestedBaseName)
    {
        var table = new NotchTable([]);

        if (requestedBaseName is null)
        {
            Assert.Throws<ArgumentNullException>(nameof(requestedBaseName),
                () => NotchExportText.ResolveSuggestedNotchExportBaseName(requestedBaseName!, "c", table));
            return;
        }

        Assert.Throws<ArgumentException>(nameof(requestedBaseName),
            () => NotchExportText.ResolveSuggestedNotchExportBaseName(requestedBaseName, "c", table));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SuggestedBaseNameRejectsMissingExtension(string? extension)
    {
        var table = new NotchTable([]);

        if (extension is null)
        {
            Assert.Throws<ArgumentNullException>(nameof(extension),
                () => NotchExportText.ResolveSuggestedNotchExportBaseName("requested", extension!, table));
            return;
        }

        Assert.Throws<ArgumentException>(nameof(extension),
            () => NotchExportText.ResolveSuggestedNotchExportBaseName("requested", extension, table));
    }

    [Fact]
    public void SuggestedBaseNameRejectsNullTable()
    {
        Assert.Throws<ArgumentNullException>("table",
            () => NotchExportText.ResolveSuggestedNotchExportBaseName("requested", "c", null!));
    }

    private static NotchTable CreateMixedTable() => new(
    [
        CreateRow(NotchAlgorithmVersion.V22, 10, 101),
        CreateRow(NotchAlgorithmVersion.V21, 10, 102),
        CreateRow(NotchAlgorithmVersion.V22, 11, 101),
    ]);

    private static NotchTableRow CreateRow(NotchAlgorithmVersion version, int regularPadIndex, int cadPadId) => new(
        version, 0, regularPadIndex, regularPadIndex, cadPadId,
        version == NotchAlgorithmVersion.V21
            ? [10, 100, 100, 11, 1, 12, 65535, 0, 0]
            : [11, 94, 12, 22, 65535, 0, 0],
        string.Empty);
}
