// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchAllocationBoundaryBehaviorTests
{
    [Fact]
    public void Generate_ManyOffGridPads_CompletesThrottledProgressAndKeepsOnlyTheOverlappingCad()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([7], cellWidth: 10, cellHeight: 10);
        // 201 is the smallest profile count that enables progress throttling; the valid pad is at a skipped update.
        var pads = Enumerable.Range(1, 201)
            .Select(id => id == 3
                ? TestGeometryFactory.CreateCadPad(id, "Panel", 0, 0, 10, 10)
                : TestGeometryFactory.CreateCadPad(id, "Outside", 30 + id, 0, 30.5 + id, 10))
            .ToArray();
        grid.Pads[0].MatchedCadPadId = 3;
        grid.Pads[0].MatchScore = 1;
        grid.Pads[0].Freeform = FreeformType.XWay;
        var reports = new ConcurrentQueue<NotchGenerationProgress>();

        var table = new NotchTableGenerator().Generate(new CadPadSet(pads), grid, CreateSettings(), new QueuedGenerationProgress(reports));

        var row = Assert.Single(table.Rows);
        Assert.Equal(3, row.CadPadId);
        Assert.Equal(0, row.RegularPadIndex);
        Assert.Equal(new NotchV22Node(7, 100, 65535, 0, 65535, 0, 0), row.V22Node);
        var profiles = reports.Where(report => report.PhaseStep == 1).ToArray();
        Assert.Equal(0, profiles[0].ProcessedCount);
        Assert.Equal(201, profiles[^1].ProcessedCount);
        Assert.All(profiles, report => Assert.Equal(201, report.TotalCount));
        Assert.DoesNotContain(profiles, report => report.CurrentCadPadId == 3);
        Assert.True(profiles.Length < pads.Length);
        var merged = reports.Last(report => report.PhaseStep == 3);
        Assert.Equal(201, merged.ProcessedCount);
        Assert.Equal(1, merged.GeneratedRowCount);
        Assert.Equal(4, reports.Last().PhaseStep);
        Assert.Equal(1, reports.Last().GeneratedRowCount);
    }

    [Fact]
    public void Generate_OnlyOffGridCad_ReturnsEmptyV21ProjectionAndCompletesEveryPhase()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, 10, 10);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(42, "Outside", 30, 0, 40, 10)]);
        var settings = CreateSettings();
        settings.Notch.EnabledVersions = [NotchAlgorithmVersion.V21];
        var reports = new ConcurrentQueue<NotchGenerationProgress>();

        var table = new NotchTableGenerator().Generate(cad, grid, settings, new QueuedGenerationProgress(reports));

        Assert.Empty(table.Rows);
        Assert.False(table.ToFullCoverageAudit.HasExpectations);
        Assert.False(table.ToFullCoverageAudit.HasMissingCoverage);
        for (var phase = 1; phase <= 4; phase++)
        {
            var completed = reports.Last(report => report.PhaseStep == phase);
            Assert.Equal(completed.TotalCount, completed.ProcessedCount);
            Assert.Equal(0, completed.GeneratedRowCount);
        }
    }

    private static ProjectSettings CreateSettings() => new()
    {
        Notch = new NotchSettings
        {
            ComputationMode = NotchComputationMode.CadAllocation,
            EnabledVersions = [NotchAlgorithmVersion.V22],
            EnableToFull = false,
            ThresholdQ7 = 0,
            LinkVersionThresholds = false,
            ThresholdPercentV22 = 0,
        },
    };

    private sealed class QueuedGenerationProgress(ConcurrentQueue<NotchGenerationProgress> reports) : IProgress<NotchGenerationProgress>
    {
        public void Report(NotchGenerationProgress value) => reports.Enqueue(value);
    }
}
