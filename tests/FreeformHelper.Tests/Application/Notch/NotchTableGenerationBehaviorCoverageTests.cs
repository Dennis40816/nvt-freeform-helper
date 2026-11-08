// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchTableGenerationBehaviorCoverageTests
{
    [Fact]
    public void CadAllocationBatch_AfterSwitchingToLegacyMode_IsIncompatibleAndRejectsBatchOperations()
    {
        var (cad, grid) = CreateMatchedCells(1);
        var settings = CreateAllocationSettings();
        var generator = new NotchTableGenerator();
        var batch = generator.ResolveCadAllocationBatch(cad, grid, settings);
        var legacySettings = CreateAllocationSettings();
        legacySettings.Notch.ComputationMode = NotchComputationMode.LegacyRegularAnchor;

        Assert.True(NotchTableGenerator.IsCadAllocationResolvedBatchCompatible(batch, settings));
        Assert.False(NotchTableGenerator.IsCadAllocationResolvedBatchCompatible(batch, legacySettings));
        Assert.False(NotchTableGenerator.HaveEquivalentCadAllocationResolutionSettings(settings, legacySettings));
        Assert.False(NotchTableGenerator.HaveEquivalentCadAllocationResolutionSettings(legacySettings, settings));
        Assert.Throws<ArgumentException>("settings", () => generator.ResolveCadAllocationBatch(cad, grid, legacySettings));
        Assert.Throws<ArgumentException>("settings", () => generator.GenerateCadAllocationResolvedBatch(cad, grid, legacySettings));
        Assert.Throws<ArgumentException>("settings", () => NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, legacySettings));
        Assert.Single(NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings).Rows);
    }

    [Theory]
    [InlineData(nameof(NotchSettings.EnableToRegular))]
    [InlineData(nameof(NotchSettings.EnableToFull))]
    [InlineData(nameof(NotchSettings.EnableToFullRuleEngine))]
    [InlineData(nameof(NotchSettings.EnableToFullRuleTrace))]
    [InlineData(nameof(NotchSettings.EnableBoundaryVirtualAreaCap))]
    public void CadAllocationBatch_ChangingAResolutionSwitch_InvalidatesTheCacheAndRejectsStaleProjection(string settingName)
    {
        var (cad, grid) = CreateMatchedCells(1);
        var original = CreateAllocationSettings();
        var changed = CreateAllocationSettings();
        var batch = new NotchTableGenerator().ResolveCadAllocationBatch(cad, grid, original);
        var originalFingerprint = NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(original);

        ToggleResolutionSwitch(changed.Notch, settingName);

        Assert.NotEqual(originalFingerprint, NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(changed));
        Assert.False(NotchTableGenerator.IsCadAllocationResolvedBatchCompatible(batch, changed));
        Assert.False(NotchTableGenerator.HaveEquivalentCadAllocationResolutionSettings(original, changed));
        Assert.Throws<ArgumentException>("settings", () => NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, changed));

        ToggleResolutionSwitch(changed.Notch, settingName);

        Assert.Equal(originalFingerprint, NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(changed));
        Assert.True(NotchTableGenerator.IsCadAllocationResolvedBatchCompatible(batch, changed));
        Assert.True(NotchTableGenerator.HaveEquivalentCadAllocationResolutionSettings(original, changed));
        Assert.Single(NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, changed).Rows);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void ProjectCadAllocationResolvedBatch_PositiveThreshold_RejectsAdmittedOverflowButAllowsFilteredOverflow(
        NotchAlgorithmVersion version)
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 1, cols: 3, cellWidth: 10, cellHeight: 10);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, 30, 10)]);
        foreach (var pad in grid.Pads)
        {
            pad.MatchedCadPadId = 100;
            pad.MatchScore = 1;
            pad.Freeform = FreeformType.XWay;
        }

        var settings = CreateAllocationSettings();
        settings.Notch.EnabledVersions = [version];
        settings.Notch.LinkVersionThresholds = false;
        settings.Notch.ThresholdQ7 = 64;
        settings.Notch.ThresholdPercentV22 = 50;
        var batch = new NotchTableGenerator().ResolveCadAllocationBatch(cad, grid, settings);

        var filtered = NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings);

        Assert.Empty(filtered.Rows);
        Assert.False(filtered.ToFullCoverageAudit.HasExpectations);
        settings.Notch.ThresholdQ7 = 32;
        settings.Notch.ThresholdPercentV22 = 25;

        var error = Assert.Throws<InvalidOperationException>(
            () => NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings));

        Assert.Contains("Combine ratio exceeds 255%", error.Message, StringComparison.Ordinal);
        Assert.Contains("raw=300%", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_LargeLegacyInput_ThrottlesProgressAndReportsEveryGeneratedRowAtCompletion()
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid) = CreateMatchedCells(201);
        var settings = CreateAllocationSettings();
        settings.Notch.ComputationMode = NotchComputationMode.LegacyRegularAnchor;
        settings.Notch.EnabledVersions = [NotchAlgorithmVersion.V21];
        var reports = new List<NotchGenerationProgress>();

        var table = new NotchTableGenerator().Generate(cad, grid, settings, new RecordingProgress(reports));

        Assert.Equal(grid.Pads.Count, table.Rows.Count);
        Assert.Equal(grid.Pads.Select(pad => pad.DiffIndex), table.Rows.Select(row => row.DiffIndex));
        Assert.All(table.Rows, row => Assert.Equal(row.RegularPadIndex + 1, row.CadPadId));
        Assert.Equal(new NotchGenerationProgress(0, 201, 0, null, null), reports[0]);
        Assert.Equal(new NotchGenerationProgress(201, 201, 201, null, null), reports[^1]);
        Assert.InRange(reports.Count, 3, 110);
        var updates = reports.Where(report => report.CurrentRegularPadId.HasValue).ToArray();
        Assert.NotEmpty(updates);
        Assert.Equal(1, updates[0].ProcessedCount);
        Assert.Equal(201, updates[^1].ProcessedCount);
        Assert.All(updates, report =>
        {
            Assert.Equal(201, report.TotalCount);
            Assert.Equal(report.ProcessedCount, report.GeneratedRowCount);
            Assert.Equal(report.ProcessedCount, report.CurrentCadPadId);
            Assert.Equal(report.ProcessedCount - 1, report.CurrentRegularPadId);
        });
        Assert.All(reports.Zip(reports.Skip(1)), pair => Assert.True(pair.First.ProcessedCount <= pair.Second.ProcessedCount));
    }

    private static (CadPadSet Cad, RegularGrid Grid) CreateMatchedCells(int count)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 1, cols: count, diffIndexSelector: (_, col) => col);
        var pads = new List<CadPad>(count);
        foreach (var pad in grid.Pads)
        {
            var cadPad = new CadPad(pad.RegularPadId + 1, "Cell", "PAD", pad.Polygon);
            pads.Add(cadPad);
            pad.MatchedCadPadId = cadPad.Id;
            pad.MatchScore = 1;
            pad.Freeform = FreeformType.XWay;
        }

        return (new CadPadSet(pads), grid);
    }

    private static ProjectSettings CreateAllocationSettings() => new()
    {
        Notch = new NotchSettings
        {
            ComputationMode = NotchComputationMode.CadAllocation,
            CompensationModel = NotchCompensationModel.CurrentGain,
            EnabledVersions = [NotchAlgorithmVersion.V22],
            EnableToRegular = true,
            EnableToFull = true,
            EnableToFullRuleEngine = true,
            EnableToFullRuleTrace = false,
            EnableBoundaryVirtualAreaCap = true,
        },
    };

    private static void ToggleResolutionSwitch(NotchSettings settings, string settingName)
    {
        switch (settingName)
        {
            case nameof(NotchSettings.EnableToRegular):
                settings.EnableToRegular = !settings.EnableToRegular;
                break;
            case nameof(NotchSettings.EnableToFull):
                settings.EnableToFull = !settings.EnableToFull;
                break;
            case nameof(NotchSettings.EnableToFullRuleEngine):
                settings.EnableToFullRuleEngine = !settings.EnableToFullRuleEngine;
                break;
            case nameof(NotchSettings.EnableToFullRuleTrace):
                settings.EnableToFullRuleTrace = !settings.EnableToFullRuleTrace;
                break;
            case nameof(NotchSettings.EnableBoundaryVirtualAreaCap):
                settings.EnableBoundaryVirtualAreaCap = !settings.EnableBoundaryVirtualAreaCap;
                break;
            default:
                throw new ArgumentException("Unknown resolution switch.", nameof(settingName));
        }
    }

    private sealed class RecordingProgress(List<NotchGenerationProgress> reports) : IProgress<NotchGenerationProgress>
    {
        public void Report(NotchGenerationProgress value) => reports.Add(value);
    }
}
