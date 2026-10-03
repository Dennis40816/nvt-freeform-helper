using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests.UI.Services;

public sealed class SimulationSafetyOverviewProjectorTests
{
    [Theory]
    [InlineData(false, true, true, "unsupported")]
    [InlineData(true, true, true, "EMS risk")]
    [InlineData(true, false, true, "audit warning")]
    [InlineData(true, false, false, "EMS OK")]
    public void BuildReplayStatusText_UsesSharedPriority(
        bool isSupported,
        bool hasEmsViolations,
        bool hasPhysicalAuditRisks,
        string expected)
    {
        Assert.Equal(
            expected,
            SimulationSafetyTextProjector.BuildReplayStatusText(
                isSupported,
                hasEmsViolations,
                hasPhysicalAuditRisks));
    }

    [Fact]
    public void Project_WhenAuditIsMissing_ReturnsRunSimulationPrompt()
    {
        var projection = SimulationSafetyOverviewProjector.Project(null, isStale: false, buildFailureText: null);

        Assert.False(projection.HasAudit);
        Assert.False(projection.HasRisk);
        Assert.Equal("Simulation not run", projection.StatusText);
        Assert.Contains("Run Simulation", projection.SummaryText);

        var unavailable = SimulationSafetyOverviewProjector.Project(
            null,
            isStale: false,
            buildFailureText: "Simulation blocked.");
        Assert.Equal("Simulation unavailable", unavailable.StatusText);
        Assert.Equal("Simulation blocked.", unavailable.SummaryText);
        Assert.True(unavailable.NeedsAttention);
    }

    [Theory]
    [InlineData(true, "512")]
    [InlineData(false, "480")]
    public void Project_WhenAuditHasNoCells_UsesSuppliedCapOtherwiseDefault(
        bool supplyAudit,
        string expectedCapText)
    {
        var audit = BuildNoCellsAudit(supplyAudit);

        var projection = SimulationSafetyOverviewProjector.Project(
            audit,
            isStale: false,
            buildFailureText: null);

        Assert.False(projection.HasAudit);
        Assert.False(projection.HasRisk);
        Assert.False(projection.NeedsAttention);
        Assert.Equal("Simulation not run", projection.StatusText);
        Assert.Equal(expectedCapText, projection.EmsCapText);
        Assert.Equal("-", projection.MaxAfterText);
        Assert.Equal("-", projection.ViolationCountText);
        Assert.Equal("Run Simulation to audit current notch output before FW handoff.", projection.SummaryText);
        Assert.Equal("Top risk diffs: -", projection.HighRiskDiffsText);
    }

    [Theory]
    [InlineData(true, "512")]
    [InlineData(false, "480")]
    public void BuildExportSummaryText_WhenAuditHasNoCells_UsesSuppliedCapOtherwiseDefault(
        bool supplyAudit,
        string expectedCapText)
    {
        var audit = BuildNoCellsAudit(supplyAudit);

        var summary = SimulationSafetyTextProjector.BuildExportSummaryText(audit);

        Assert.Equal(
            "Simulation audit is not available in the current workspace. " +
            $"Open Simulation to review After <= {expectedCapText} before FW handoff.",
            summary);
    }

    [Fact]
    public void Project_WhenPhysicalAuditWarnsWithoutEms_UsesWarningStatusAndEvidence()
    {
        var audit = BuildAuditWithGlobalFlowResidual(afterValue: 450d);

        var status = SimulationSafetyTextProjector.BuildStatusText(audit);
        var physicalSummary = SimulationSafetyTextProjector.BuildPhysicalAuditSummaryText(audit);
        var overview = SimulationSafetyOverviewProjector.Project(audit, isStale: false, buildFailureText: null);

        Assert.True(audit.HasPhysicalAuditRisks);
        Assert.False(audit.HasViolations);
        Assert.Equal(
            "Physical audit: global flow residual +50; net-flow OK; target coverage <= 120%.",
            physicalSummary);
        Assert.Equal("audit warning", status);
        Assert.True(overview.HasAudit);
        Assert.False(overview.HasRisk);
        Assert.True(overview.NeedsAttention);
        Assert.Equal(status, overview.StatusText);
        Assert.Equal(
            "Current Simulation is under EMS cap. Max After 450. " +
            "Physical audit: global flow residual +50; net-flow OK; target coverage <= 120%.",
            overview.SummaryText);
    }

    [Fact]
    public void BuildStatusText_WhenEmsAndPhysicalRisksCoexist_PrioritizesEms()
    {
        var audit = BuildAuditWithGlobalFlowResidual(afterValue: 500d);

        Assert.True(audit.HasViolations);
        Assert.True(audit.HasPhysicalAuditRisks);
        Assert.Equal("EMS risk", SimulationSafetyTextProjector.BuildStatusText(audit));
    }

    [Fact]
    public void Project_WhenAuditHasViolation_ReturnsRiskSummaryAndTopDiffs()
    {
        var audit = SimulationSafetyAuditService.Analyze(
            new[]
            {
                new NotchApplySimulationDiffCell(RegularPadId: 10, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 72, BeforeValue: 400d, AfterValue: 492d),
                new NotchApplySimulationDiffCell(RegularPadId: 11, RegularRow: 0, RegularCol: 1, IcIndex: 0, DiffIndex: 73, BeforeValue: 400d, AfterValue: 420d),
            });

        var projection = SimulationSafetyOverviewProjector.Project(audit, isStale: false, buildFailureText: null);

        Assert.True(projection.HasAudit);
        Assert.True(projection.HasRisk);
        Assert.True(projection.NeedsAttention);
        Assert.Equal("EMS risk", projection.StatusText);
        Assert.Equal("492", projection.MaxAfterText);
        Assert.Contains("REG 10/D72", projection.HighRiskDiffsText);
    }

    [Fact]
    public void Project_WhenAuditIsStale_MarksAttentionWithoutRisk()
    {
        var audit = SimulationSafetyAuditService.Analyze(
            new[]
            {
                new NotchApplySimulationDiffCell(RegularPadId: 10, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 72, BeforeValue: 400d, AfterValue: 420d),
            });

        var projection = SimulationSafetyOverviewProjector.Project(audit, isStale: true, buildFailureText: null);

        Assert.False(projection.HasRisk);
        Assert.True(projection.NeedsAttention);
        Assert.Equal("EMS OK (stale)", projection.StatusText);
        Assert.Contains("Refresh Simulation", projection.SummaryText);
    }

    [Fact]
    public void Project_WhenTopCellIsWithinEmsTolerance_UsesSafeZeroMargin()
    {
        const double afterCap = 1d;
        var audit = SimulationSafetyAuditService.Analyze(
        [
            new NotchApplySimulationDiffCell(
                RegularPadId: 10,
                RegularRow: 0,
                RegularCol: 0,
                IcIndex: 0,
                DiffIndex: 72,
                BeforeValue: 400d,
                AfterValue: afterCap + 1e-9)
        ], afterCap: afterCap);

        var projection = SimulationSafetyOverviewProjector.Project(
            audit,
            isStale: false,
            buildFailureText: null);
        var highRiskText = SimulationSafetyTextProjector.BuildHighRiskDiffListText(audit, emptyText: "-");

        Assert.False(projection.HasRisk);
        Assert.False(projection.NeedsAttention);
        Assert.Equal("EMS OK", projection.StatusText);
        Assert.Contains("Closest:", projection.HighRiskDiffsText, StringComparison.Ordinal);
        Assert.Contains("(0m)", projection.HighRiskDiffsText, StringComparison.Ordinal);
        Assert.DoesNotContain("+0", projection.HighRiskDiffsText, StringComparison.Ordinal);
        Assert.Equal(
            "0 margin",
            SimulationSafetyTextProjector.FormatRiskMarginText(Assert.Single(audit.HighRiskDiffs), afterCap));
        Assert.Contains("margin 0", highRiskText, StringComparison.Ordinal);
        Assert.DoesNotContain("over cap", highRiskText, StringComparison.OrdinalIgnoreCase);
    }

    private static SimulationSafetyAuditResult? BuildNoCellsAudit(bool supplyAudit) =>
        supplyAudit
            ? SimulationSafetyAuditService.Analyze(
                Array.Empty<NotchApplySimulationDiffCell>(),
                afterCap: 512d)
            : null;

    private static SimulationSafetyAuditResult BuildAuditWithGlobalFlowResidual(double afterValue)
    {
        const double beforeValue = 400d;
        const double residualValue = 50d;
        var deltaValue = afterValue - beforeValue;
        return SimulationSafetyAuditService.Analyze(
        [
            new NotchApplySimulationDiffCell(
                RegularPadId: 1,
                RegularRow: 0,
                RegularCol: 0,
                IcIndex: 0,
                DiffIndex: 10,
                BeforeValue: beforeValue,
                AfterValue: afterValue),
        ]) with
        {
            GlobalFlowAudit = new SimulationGlobalFlowAudit(
                HasActionFlowModel: true,
                BeforeTotal: beforeValue,
                AfterTotal: afterValue,
                DeltaTotal: deltaValue,
                ActionNetFlowTotal: deltaValue - residualValue,
                ResidualValue: residualValue),
        };
    }
}
