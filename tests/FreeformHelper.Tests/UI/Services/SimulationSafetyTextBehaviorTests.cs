// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests.UI.Services;

public sealed class SimulationSafetyTextBehaviorTests
{
    [Fact]
    public void EmptyAudit_ShowsNoPhysicalEvidenceOrExportBadgeInsteadOfASafeResult()
    {
        using var culture = TestCulture.UseEnUs();
        var audit = SimulationSafetyAuditService.Analyze(Array.Empty<NotchApplySimulationDiffCell>());

        Assert.Equal("Physical audit: no simulation cells.", SimulationSafetyTextProjector.BuildPhysicalAuditSummaryText(audit));
        Assert.Equal("Simulation audit: not run", SimulationSafetyTextProjector.BuildExportBadgeText(audit));
        Assert.Equal("REG -", SimulationSafetyTextProjector.BuildViolationListText(audit));
        Assert.Equal("No diffs to review", SimulationSafetyTextProjector.BuildHighRiskDiffListText(audit, "No diffs to review"));
        Assert.Equal("High-risk diffs: -", SimulationSafetyTextProjector.BuildExportHighRiskText(audit));
    }

    [Theory]
    [InlineData(1, "REG 10 After 492.5, ... (+1)")]
    [InlineData(2, "REG 10 After 492.5, REG 11 After 480.25")]
    public void EmsViolations_BlockExportAndShowTheTotalBeyondThePreview(int previewCount, string expectedViolations)
    {
        using var culture = TestCulture.UseEnUs();
        var cells = new[]
        {
            new NotchApplySimulationDiffCell(10, 0, 0, 1, 72, 400d, 492.5d),
            new NotchApplySimulationDiffCell(11, 0, 1, 1, 73, 400d, 480.25d),
        };
        var audit = SimulationSafetyAuditService.Analyze(cells, violationPreviewCount: previewCount);
        const string summary = "Blocked: 2 pad(s) exceed EMS cap 480. Max After 492.5 at REG 10.";
        var highRiskText = string.Join(Environment.NewLine,
            "#1 REG 10 · IC 2 · FW Diff 72 · After 492.5 · +12.5 over cap",
            "#2 REG 11 · IC 2 · FW Diff 73 · After 480.25 · +0.25 over cap");

        Assert.Equal(
            "EMS risk: 2 pad(s) exceed cap 480. Max After 492.5 at REG 10.",
            SimulationSafetyTextProjector.BuildWorkspaceSummaryText(audit));
        Assert.Equal(expectedViolations, SimulationSafetyTextProjector.BuildViolationListText(audit));
        Assert.Equal("EMS risk 2", SimulationSafetyTextProjector.BuildExportBadgeText(audit));
        Assert.Equal(summary, SimulationSafetyTextProjector.BuildExportSummaryText(audit));
        Assert.Equal(highRiskText, SimulationSafetyTextProjector.BuildExportHighRiskText(audit));
        Assert.Equal(highRiskText, SimulationSafetyTextProjector.BuildHighRiskDiffListText(audit, "-"));
        Assert.Equal(
            summary + Environment.NewLine + Environment.NewLine + highRiskText,
            SimulationSafetyTextProjector.BuildExportBlockMessage(audit));
        Assert.Equal("EMS risk: +12.5 over cap", SimulationSafetyTextProjector.FormatCellEmsStatus(cells[0], audit.AfterCap));
        Assert.Equal("+12.5 over", SimulationSafetyTextProjector.FormatRiskMarginText(audit.HighRiskDiffs[0], audit.AfterCap));
        Assert.Equal("Export blocked (EMS risk)", SimulationSafetyTextProjector.BuildExportButtonText(true, "Export C"));
    }

    [Theory]
    [InlineData(false, "EMS OK", "Physical audit: global flow residual OK; net-flow OK; target coverage <= 120%.",
        "Safe for EMS cap 480: Max After 450.25 at REG 10.")]
    [InlineData(true, "audit warning", "Physical audit: global flow residual +0.25; net-flow residuals 1; target coverage risks 1.",
        "Safe for EMS cap 480: Max After 450.25 at REG 10. " +
        "Physical audit: global flow residual +0.25; net-flow residuals 1; target coverage risks 1.")]
    public void UnderCapExport_PreservesPhysicalWarningsWithoutAnEmsBlock(
        bool hasPhysicalRisks, string expectedStatus, string expectedPhysicalSummary, string expectedExportSummary)
    {
        using var culture = TestCulture.UseEnUs();
        var cell = new NotchApplySimulationDiffCell(10, 0, 0, 1, 72, 400d, 450.25d);
        var audit = SimulationSafetyAuditService.Analyze([cell]);
        if (hasPhysicalRisks)
        {
            audit = audit with
            {
                GlobalFlowAudit = new SimulationGlobalFlowAudit(true, 400d, 450.25d, 50.25d, 50d, 0.25d),
                TotalNetFlowResidualCount = 1,
                NetFlowResiduals =
                [
                    new SimulationNetFlowAuditDiff(10, 1, 72, 50.25d, 50d, 0d, 50d, 0.25d,
                        SimulationPhysicalAuditClassification.NetFlowSuspicious),
                ],
                TotalTargetCoverageRiskCount = 1,
                TargetCoverageRisks =
                [
                    new SimulationTargetCoverageAuditDiff(10, 1, 72, 125d, 5d, 450.25d,
                        SimulationPhysicalAuditClassification.GeometryExpected),
                ],
            };
        }

        Assert.Equal("EMS OK: Max After 450.25 <= cap 480.", SimulationSafetyTextProjector.BuildWorkspaceSummaryText(audit));
        Assert.Equal(expectedPhysicalSummary, SimulationSafetyTextProjector.BuildPhysicalAuditSummaryText(audit));
        Assert.Equal(expectedExportSummary, SimulationSafetyTextProjector.BuildExportSummaryText(audit));
        Assert.Equal(expectedStatus + " · Max After 450.25", SimulationSafetyTextProjector.BuildExportBadgeText(audit));
        Assert.Equal("EMS OK: 29.75 margin", SimulationSafetyTextProjector.FormatCellEmsStatus(cell, audit.AfterCap));
        Assert.Equal(
            "29.75 margin",
            SimulationSafetyTextProjector.FormatRiskMarginText(Assert.Single(audit.HighRiskDiffs), audit.AfterCap));
        Assert.Equal("Export C", SimulationSafetyTextProjector.BuildExportButtonText(audit.HasViolations, "Export C"));
    }
}
