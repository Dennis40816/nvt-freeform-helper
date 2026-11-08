// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationSafetyAuditRiskCoverageTests
{
    [Fact]
    public void Analyze_OverCapCellWithResidualAndExcessCoverage_ClassifiesBothAsEmsRisk()
    {
        var cell = new NotchApplySimulationDiffCell(1, 0, 0, 0, 10, 400d, 500d);
        var action = new NotchApplySimulationAction(
            NotchAlgorithmVersion.V22,
            IcIndex: 0,
            AnchorDiffIndex: 10,
            RegularPadId: 1,
            CadPadId: null,
            SourceRowNumbers: [1],
            CombinePercent: 130,
            SourceRetainedPercent: 130,
            SourceBeforeValue: 400d,
            SourceAfterValue: 480d,
            Legs: []);

        var audit = SimulationSafetyAuditService.Analyze(BuildResult([cell], [action]));

        Assert.True(audit.HasViolations);
        Assert.Equal(20d, Assert.Single(audit.Violations).ExcessValue);
        Assert.Equal(1, audit.TotalNetFlowResidualCount);
        var residual = Assert.Single(audit.NetFlowResiduals);
        Assert.Equal(1, residual.RegularPadId);
        Assert.Equal(80d, residual.SourceDeltaValue);
        Assert.Equal(0d, residual.TargetDeltaValue);
        Assert.Equal(20d, residual.ResidualValue);
        Assert.Equal(SimulationPhysicalAuditClassification.EmsRisk, residual.Classification);
        Assert.Equal(1, audit.TotalTargetCoverageRiskCount);
        var coverage = Assert.Single(audit.TargetCoverageRisks);
        Assert.Equal(1, coverage.RegularPadId);
        Assert.Equal(130d, coverage.CoveragePercent);
        Assert.Equal(10d, coverage.CoverageExcessPercent);
        Assert.Equal(500d, coverage.MaxAfterValue);
        Assert.Equal(SimulationPhysicalAuditClassification.EmsRisk, coverage.Classification);
    }

    [Fact]
    public void Analyze_MissingTargetCell_KeepsCoverageRiskUnassignedAndDoesNotBorrowAnotherIcsCell()
    {
        NotchApplySimulationDiffCell[] cells =
        [
            new(1, 0, 0, 0, 10, 100d, 20d),
            new(2, 0, 1, 0, 12, 100d, 20d),
            new(3, 0, 2, 1, 99, 600d, 600d),
        ];
        NotchApplySimulationAction[] actions =
        [
            BuildMissingTargetAction(10, 1),
            BuildMissingTargetAction(12, 2),
        ];

        var audit = SimulationSafetyAuditService.Analyze(BuildResult(cells, actions));

        Assert.Equal(1, audit.TotalTargetCoverageRiskCount);
        var risk = Assert.Single(audit.TargetCoverageRisks);
        Assert.Null(risk.RegularPadId);
        Assert.Equal(0, risk.IcIndex);
        Assert.Equal(99, risk.DiffIndex);
        Assert.Equal(160d, risk.CoveragePercent);
        Assert.Equal(40d, risk.CoverageExcessPercent);
        Assert.Equal(0d, risk.MaxAfterValue);
        Assert.Equal(SimulationPhysicalAuditClassification.GeometryExpected, risk.Classification);
        Assert.True(audit.HasGlobalFlowResidual);
        Assert.Equal(-160d, audit.GlobalFlowAudit.ResidualValue);
        Assert.Equal(0, audit.TotalNetFlowResidualCount);
        Assert.Empty(audit.NetFlowResiduals);
        Assert.Equal(3, Assert.Single(audit.Violations).RegularPadId);
    }

    private static NotchApplySimulationAction BuildMissingTargetAction(int diffIndex, int regularPadId)
    {
        return new NotchApplySimulationAction(
            NotchAlgorithmVersion.V22,
            IcIndex: 0,
            AnchorDiffIndex: diffIndex,
            RegularPadId: regularPadId,
            CadPadId: null,
            SourceRowNumbers: [regularPadId],
            CombinePercent: 100,
            SourceRetainedPercent: 20,
            SourceBeforeValue: 100d,
            SourceAfterValue: 20d,
            Legs: [new NotchApplySimulationLeg(99, 80, 80d)]);
    }

    private static NotchApplySimulationResult BuildResult(
        IReadOnlyList<NotchApplySimulationDiffCell> cells,
        IReadOnlyList<NotchApplySimulationAction> actions)
    {
        return new NotchApplySimulationResult(
            IsSupported: true,
            Diagnostics: [],
            Version: NotchAlgorithmVersion.V22,
            ContractText: "Synthetic physical audit input",
            AggregationMode: NotchApplySimulationAggregationMode.SingleFrame,
            ConsumedFrameCount: 1,
            Cells: cells,
            Actions: actions,
            DiffIdentityContract: NotchApplySimulationDiffIdentityContract.Empty,
            Histograms: new NotchApplySimulationHistogramSet(
                new NotchApplySimulationHistogram("Before", []),
                new NotchApplySimulationHistogram("After", []),
                new NotchApplySimulationHistogram("Delta", [])),
            Heatmap: new NotchApplySimulationHeatmap(0, 0, 0d, [], []));
    }
}
