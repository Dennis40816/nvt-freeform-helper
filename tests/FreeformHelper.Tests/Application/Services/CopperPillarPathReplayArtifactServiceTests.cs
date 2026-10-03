using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CopperPillarPathReplayArtifactServiceTests
{
    private static readonly string[] SampleDiagnostics = ["sample diagnostic"];

    [Fact]
    public void ReplayResult_ProjectsStructuredSupportAndPhysicalRisk()
    {
        var step = new CopperPillarPathReplayStep(
            StepIndex: 0,
            CenterX: 1d,
            CenterY: 2d,
            MaxAfterValue: 100d,
            EmsViolationCount: 0,
            WorstRegularPadId: null,
            WorstIcIndex: null,
            WorstDiffIndex: null,
            NetFlowResidualCount: 0,
            TargetCoverageRiskCount: 0,
            StatusText: "unsupported",
            Diagnostics: Array.Empty<string>())
        {
            IsSupported = false,
            HasGlobalFlowResidual = true,
        };
        var result = new CopperPillarPathReplayResult([step]);

        Assert.True(step.HasPhysicalAuditRisks);
        Assert.True(result.HasUnsupportedSteps);
        Assert.True(result.HasPhysicalAuditRisks);
    }

    [Fact]
    public void BuildSnapshot_ProjectsReplayStepsIntoStableArtifactRows()
    {
        var result = new CopperPillarPathReplayResult(new[]
        {
            new CopperPillarPathReplayStep(
                StepIndex: 0,
                CenterX: 1.25d,
                CenterY: 2.5d,
                MaxAfterValue: 405d,
                EmsViolationCount: 1,
                WorstRegularPadId: 7,
                WorstIcIndex: 0,
                WorstDiffIndex: 12,
                NetFlowResidualCount: 2,
                TargetCoverageRiskCount: 3,
                StatusText: "EMS risk",
                Diagnostics: SampleDiagnostics),
        });

        Assert.False(result.HasUnsupportedSteps);
        var snapshot = CopperPillarPathReplayArtifactService.BuildSnapshot(result);

        Assert.Equal(1, snapshot.StepCount);
        Assert.Equal(1, snapshot.TotalEmsViolationCount);
        Assert.Equal(2, snapshot.TotalNetFlowResidualCount);
        Assert.Equal(3, snapshot.TotalTargetCoverageRiskCount);
        Assert.Equal(0, snapshot.WorstStepIndex);
        var row = Assert.Single(snapshot.Rows);
        Assert.Equal("#1", row.StepText);
        Assert.Equal("(1.25, 2.5)", row.CenterText);
        Assert.Contains("REG 7", row.WorstText, StringComparison.Ordinal);
        Assert.Contains("net-flow 2", row.AuditText, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportCsvAndClipboard_UseDeterministicColumns()
    {
        var snapshot = CopperPillarPathReplayArtifactService.BuildSnapshot(new CopperPillarPathReplayResult(new[]
        {
            new CopperPillarPathReplayStep(
                StepIndex: 0,
                CenterX: 1d,
                CenterY: 2d,
                MaxAfterValue: 399.5d,
                EmsViolationCount: 0,
                WorstRegularPadId: null,
                WorstIcIndex: null,
                WorstDiffIndex: null,
                NetFlowResidualCount: 0,
                TargetCoverageRiskCount: 0,
                StatusText: "EMS OK",
                Diagnostics: Array.Empty<string>()),
        }));

        var csv = CopperPillarPathReplayArtifactService.ExportCsv(snapshot);
        var json = CopperPillarPathReplayArtifactService.ExportJson(snapshot);
        var clipboard = CopperPillarPathReplayArtifactService.ExportClipboardTable(snapshot.Rows);

        Assert.StartsWith("step_index,center_x,center_y,max_after", csv, StringComparison.Ordinal);
        Assert.Contains("\"399.5\"", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("hasGlobalFlowResidual", json, StringComparison.Ordinal);
        Assert.StartsWith("Step\tCenter\tMax After", clipboard, StringComparison.Ordinal);
        Assert.Contains("#1\t(1, 2)\tMax After 399.5", clipboard, StringComparison.Ordinal);
    }
}
