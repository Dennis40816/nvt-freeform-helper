// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CopperPillarReplayArtifactBehaviorTests
{
    [Fact]
    public void BuildSnapshot_PartialWorstIdentity_FormatsClipboardAndEscapesCsvWithoutInventingIndices()
    {
        using var culture = TestCulture.UseEnUs();
        var first = CreateStep(2) with
        {
            CenterX = 1.25,
            CenterY = -2.5,
            MaxAfterValue = 399.5,
            WorstRegularPadId = 7,
            WorstDiffIndex = 12,
            StatusText = "audit, \"warning\"",
            Diagnostics = ["first\nsecond", "quoted \"detail\""],
        };
        var second = CreateStep(0) with
        {
            MaxAfterValue = 399.5,
            WorstRegularPadId = 8,
            WorstIcIndex = 1,
        };

        var snapshot = CopperPillarPathReplayArtifactService.BuildSnapshot(new CopperPillarPathReplayResult([first, second]));
        var clipboard = CopperPillarPathReplayArtifactService.ExportClipboardTable(snapshot.Rows);
        var csv = CopperPillarPathReplayArtifactService.ExportCsv(snapshot);

        Assert.Equal(0, snapshot.WorstStepIndex);
        Assert.Equal("REG 7 · IC - · FW Diff 12", snapshot.Rows[0].WorstText);
        Assert.Equal("REG 8 · IC 2 · FW Diff -", snapshot.Rows[1].WorstText);
        Assert.Contains("#3\t(1.25, -2.5)\tMax After 399.5\tREG 7 · IC - · FW Diff 12", clipboard, StringComparison.Ordinal);
        Assert.Contains("EMS 0 · net-flow 0 · coverage 0", clipboard, StringComparison.Ordinal);
        Assert.Contains("first\nsecond | quoted \"detail\"", clipboard, StringComparison.Ordinal);
        Assert.Contains("\"7\",\"\",\"12\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"8\",\"1\",\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"audit, \"\"warning\"\"\",\"first\nsecond | quoted \"\"detail\"\"\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSnapshot_IndependentAuditRisks_PreservesEachRiskWithoutTreatingEmsAsPhysicalRisk()
    {
        var result = new CopperPillarPathReplayResult(
        [
            CreateStep(0) with { HasGlobalFlowResidual = true },
            CreateStep(1) with { NetFlowResidualCount = 2 },
            CreateStep(2) with { TargetCoverageRiskCount = 3 },
            CreateStep(3) with { EmsViolationCount = 1 },
            CreateStep(4),
        ]);

        var snapshot = CopperPillarPathReplayArtifactService.BuildSnapshot(result);

        Assert.Equal(5, snapshot.StepCount);
        Assert.Equal(1, snapshot.TotalEmsViolationCount);
        Assert.Equal(2, snapshot.TotalNetFlowResidualCount);
        Assert.Equal(3, snapshot.TotalTargetCoverageRiskCount);
        Assert.Collection(snapshot.Rows,
            row =>
            {
                Assert.True(row.HasGlobalFlowResidual);
                Assert.True(row.HasPhysicalAuditRisks);
                Assert.False(row.HasEmsViolations);
            },
            row =>
            {
                Assert.False(row.HasGlobalFlowResidual);
                Assert.True(row.HasPhysicalAuditRisks);
                Assert.False(row.HasEmsViolations);
            },
            row =>
            {
                Assert.False(row.HasGlobalFlowResidual);
                Assert.True(row.HasPhysicalAuditRisks);
                Assert.False(row.HasEmsViolations);
            },
            row =>
            {
                Assert.False(row.HasPhysicalAuditRisks);
                Assert.True(row.HasEmsViolations);
            },
            row =>
            {
                Assert.False(row.HasPhysicalAuditRisks);
                Assert.False(row.HasEmsViolations);
            });
    }

    [Fact]
    public void BuildSnapshot_EmptyReplay_ExportsHeadersAndHasNoWorstStepOrAuditTotals()
    {
        using var culture = TestCulture.UseEnUs();

        var snapshot = CopperPillarPathReplayArtifactService.BuildSnapshot(CopperPillarPathReplayResult.Empty);
        var csv = CopperPillarPathReplayArtifactService.ExportCsv(snapshot);
        var clipboard = CopperPillarPathReplayArtifactService.ExportClipboardTable(snapshot.Rows);

        Assert.Empty(snapshot.Rows);
        Assert.Equal(0, snapshot.StepCount);
        Assert.Equal(0, snapshot.TotalEmsViolationCount);
        Assert.Equal(0, snapshot.TotalNetFlowResidualCount);
        Assert.Equal(0, snapshot.TotalTargetCoverageRiskCount);
        Assert.Null(snapshot.WorstStepIndex);
        Assert.Equal(
            "step_index,center_x,center_y,max_after,ems_violations,worst_regular_pad,worst_ic,worst_diff," +
            "net_flow_residuals,target_coverage_risks,status,diagnostics" + Environment.NewLine,
            csv);
        Assert.Equal("Step\tCenter\tMax After\tWorst\tAudit\tStatus\tDiagnostics" + Environment.NewLine, clipboard);
    }

    private static CopperPillarPathReplayStep CreateStep(int index) => new(
        StepIndex: index,
        CenterX: 0,
        CenterY: 0,
        MaxAfterValue: 100,
        EmsViolationCount: 0,
        WorstRegularPadId: null,
        WorstIcIndex: null,
        WorstDiffIndex: null,
        NetFlowResidualCount: 0,
        TargetCoverageRiskCount: 0,
        StatusText: "EMS OK",
        Diagnostics: []);
}
