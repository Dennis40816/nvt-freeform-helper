// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class AlgorithmAuditReportingTests
{
    [Theory]
    [InlineData(120, false)]
    [InlineData(90, true)]
    public void SimulationAuditUsesTheSessionCoverageCap(int cap, bool expectedRisk)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1);
        var table = new NotchTable([new NotchTableRow(0, 0, 0, null, new NotchV22Node(0, 100, 65535, 0, 65535, 0, 0), "audit")]);
        var session = new SimulationWorkspaceSession(grid, table, 65535, 1, new HashSet<int> { 0 }, TargetCoverageCapPercent: cap);
        var scenario = SimulationWorkspaceUseCase.BuildManualScenario(session, NotchAlgorithmVersion.V22, 400, new Dictionary<int, double>());

        var audit = SimulationWorkspaceUseCase.BuildScenarioSnapshot(session, scenario).SafetyAudit;

        Assert.Equal(cap, audit.TargetCoveragePercentCap);
        Assert.Equal(expectedRisk, audit.HasTargetCoverageRisks);
        Assert.Equal(480, audit.AfterCap);
    }

    [Theory]
    [InlineData(110, 32, false)]
    [InlineData(10, 256, true)]
    public void V21ProjectionReportsClampingWithoutChangingNormalizedValues(int combine, int q7, bool expectedClamp)
    {
        var diagnostics = new List<string>();
        var row = new NotchTableRow(NotchAlgorithmVersion.V21, 0, 0, 0, 10, [0, 100, combine, 1, 1, q7, 65535, 0, 0], "audit");

        var projection = NotchV21FirmwareProjector.Project(new[] { row }, 65535, 1, NotchComputationMode.CadAllocation, diagnostics);

        Assert.Equal(Math.Min(255, q7), Assert.Single(Assert.Single(projection.SourceRows).Legs).RatioQ7);
        Assert.Equal(expectedClamp, diagnostics.Count > 0);
    }

    [Fact]
    public void V21ProjectionReportsBothRetainedAndQ7Clamping()
    {
        var diagnostics = new List<string>();
        var row = new NotchTableRow(NotchAlgorithmVersion.V21, 0, 0, 0, 10, [0, 100, 10, 1, 1, 256, 65535, 0, 0], "audit");

        NotchV21FirmwareProjector.Project(new[] { row }, 65535, 1, NotchComputationMode.CadAllocation, diagnostics);

        Assert.Contains(diagnostics, message => message.Contains("retained percent", StringComparison.Ordinal));
        Assert.Contains(diagnostics, message => message.Contains("Q7 256", StringComparison.Ordinal));
    }
}
