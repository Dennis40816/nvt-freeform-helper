using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationSafetyAuditServiceTests
{
    private static readonly int[] SingleSourceRowNumbers = { 1 };

    [Theory]
    [InlineData(0d, false)]
    [InlineData(0.5e-9, false)]
    [InlineData(2e-9, true)]
    public void Analyze_UsesEmsAfterCapToleranceBoundary(double offsetFromCap, bool expectedViolation)
    {
        var cell = new NotchApplySimulationDiffCell(
            RegularPadId: 1,
            RegularRow: 0,
            RegularCol: 0,
            IcIndex: 0,
            DiffIndex: 10,
            BeforeValue: 0d,
            AfterValue: SimulationSafetyAuditService.DefaultEmsAfterCap + offsetFromCap);

        var result = SimulationSafetyAuditService.Analyze(new[] { cell });

        Assert.Equal(expectedViolation, result.HasViolations);
        Assert.Equal(expectedViolation ? 1 : 0, result.TotalViolationCount);
    }

    [Fact]
    public void AnalyzeResult_WhenBoundaryCellHasPhysicalRisks_DoesNotClassifyThemAsEmsRisk()
    {
        var afterValue = SimulationSafetyAuditService.DefaultEmsAfterCap + 0.5e-9;
        var cell = new NotchApplySimulationDiffCell(
            RegularPadId: 1,
            RegularRow: 0,
            RegularCol: 0,
            IcIndex: 0,
            DiffIndex: 10,
            BeforeValue: 470d,
            AfterValue: afterValue);
        var action = new NotchApplySimulationAction(
            NotchAlgorithmVersion.V22,
            IcIndex: 0,
            AnchorDiffIndex: 10,
            RegularPadId: 1,
            CadPadId: null,
            SourceRowNumbers: SingleSourceRowNumbers,
            CombinePercent: 130,
            SourceRetainedPercent: 130,
            SourceBeforeValue: 470d,
            SourceAfterValue: 470d,
            Legs: Array.Empty<NotchApplySimulationLeg>());

        var result = SimulationSafetyAuditService.Analyze(BuildResult([cell], [action]));

        Assert.False(result.HasViolations);
        Assert.Equal(
            SimulationPhysicalAuditClassification.NetFlowSuspicious,
            Assert.Single(result.NetFlowResiduals).Classification);
        Assert.Equal(
            SimulationPhysicalAuditClassification.GeometryExpected,
            Assert.Single(result.TargetCoverageRisks).Classification);
    }

    [Fact]
    public void Analyze_WhenAllCellsAreUnderCap_ReportsCleanStateAndMaxAfter()
    {
        var cells = new[]
        {
            new NotchApplySimulationDiffCell(RegularPadId: 1, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 10, BeforeValue: 400d, AfterValue: 452d),
            new NotchApplySimulationDiffCell(RegularPadId: 2, RegularRow: 0, RegularCol: 1, IcIndex: 0, DiffIndex: 11, BeforeValue: 400d, AfterValue: 380d),
        };

        var result = SimulationSafetyAuditService.Analyze(cells);

        Assert.False(result.HasViolations);
        Assert.Equal(452d, result.MaxAfterValue);
        Assert.Equal(1, result.MaxAfterRegularPadId);
        Assert.True(result.HasHighRiskDiffs);
        Assert.Equal(1, result.HighRiskDiffs[0].RegularPadId);
        Assert.Equal(28d, result.HighRiskDiffs[0].MarginToCap);
    }

    [Fact]
    public void Analyze_WhenAfterExceedsCap_ReportsEmsRiskViolations()
    {
        var cells = new[]
        {
            new NotchApplySimulationDiffCell(RegularPadId: 1, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 10, BeforeValue: 400d, AfterValue: 481d),
            new NotchApplySimulationDiffCell(RegularPadId: 2, RegularRow: 0, RegularCol: 1, IcIndex: 0, DiffIndex: 11, BeforeValue: 400d, AfterValue: 500d),
        };

        var result = SimulationSafetyAuditService.Analyze(cells);

        Assert.True(result.HasViolations);
        Assert.Equal(2, result.TotalViolationCount);
        Assert.Equal(500d, result.MaxAfterValue);
        var highest = result.Violations[0];
        Assert.Equal(2, highest.RegularPadId);
        Assert.Equal(20d, highest.ExcessValue);
        Assert.Equal(2, result.HighRiskDiffs[0].RegularPadId);
        Assert.Equal(-20d, result.HighRiskDiffs[0].MarginToCap);
    }

    [Fact]
    public void AnalyzeResult_WhenActionsMatchCells_ReportsBalancedPhysicalAudit()
    {
        var cells = new[]
        {
            new NotchApplySimulationDiffCell(RegularPadId: 1, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 10, BeforeValue: 400d, AfterValue: 200d),
            new NotchApplySimulationDiffCell(RegularPadId: 2, RegularRow: 0, RegularCol: 1, IcIndex: 0, DiffIndex: 11, BeforeValue: 100d, AfterValue: 300d),
        };
        var actions = new[]
        {
            new NotchApplySimulationAction(
                NotchAlgorithmVersion.V22,
                IcIndex: 0,
                AnchorDiffIndex: 10,
                RegularPadId: 1,
                CadPadId: 313,
                SourceRowNumbers: SingleSourceRowNumbers,
                CombinePercent: 100,
                SourceRetainedPercent: 50,
                SourceBeforeValue: 400d,
                SourceAfterValue: 200d,
                Legs: new[]
                {
                    new NotchApplySimulationLeg(TargetDiffIndex: 11, RatioPercent: 50, DeltaValue: 200d),
                }),
        };

        var result = SimulationSafetyAuditService.Analyze(BuildResult(cells, actions));

        Assert.False(result.HasPhysicalAuditRisks);
        Assert.True(result.GlobalFlowAudit.IsBalanced);
        Assert.Equal(0, result.TotalNetFlowResidualCount);
        Assert.Equal(0, result.TotalTargetCoverageRiskCount);
    }

    [Fact]
    public void AnalyzeResult_WhenCellDeltaDiffersFromActionFlow_ReportsNetFlowResidual()
    {
        var cells = new[]
        {
            new NotchApplySimulationDiffCell(RegularPadId: 1, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 10, BeforeValue: 400d, AfterValue: 200d),
            new NotchApplySimulationDiffCell(RegularPadId: 2, RegularRow: 0, RegularCol: 1, IcIndex: 0, DiffIndex: 11, BeforeValue: 100d, AfterValue: 250d),
        };
        var actions = new[]
        {
            new NotchApplySimulationAction(
                NotchAlgorithmVersion.V22,
                IcIndex: 0,
                AnchorDiffIndex: 10,
                RegularPadId: 1,
                CadPadId: 313,
                SourceRowNumbers: SingleSourceRowNumbers,
                CombinePercent: 100,
                SourceRetainedPercent: 50,
                SourceBeforeValue: 400d,
                SourceAfterValue: 200d,
                Legs: new[]
                {
                    new NotchApplySimulationLeg(TargetDiffIndex: 11, RatioPercent: 50, DeltaValue: 200d),
                }),
        };

        var result = SimulationSafetyAuditService.Analyze(BuildResult(cells, actions));

        Assert.True(result.HasPhysicalAuditRisks);
        Assert.Equal(1, result.TotalNetFlowResidualCount);
        var residual = Assert.Single(result.NetFlowResiduals);
        Assert.Equal(2, residual.RegularPadId);
        Assert.Equal(-50d, residual.ResidualValue);
        Assert.Equal(SimulationPhysicalAuditClassification.NetFlowSuspicious, residual.Classification);
    }

    [Fact]
    public void AnalyzeResult_WhenTargetCoverageExceedsCap_ReportsCoverageRisk()
    {
        var cells = new[]
        {
            new NotchApplySimulationDiffCell(RegularPadId: 1, RegularRow: 0, RegularCol: 0, IcIndex: 0, DiffIndex: 10, BeforeValue: 100d, AfterValue: 20d),
            new NotchApplySimulationDiffCell(RegularPadId: 2, RegularRow: 0, RegularCol: 1, IcIndex: 0, DiffIndex: 11, BeforeValue: 100d, AfterValue: 260d),
            new NotchApplySimulationDiffCell(RegularPadId: 3, RegularRow: 0, RegularCol: 2, IcIndex: 0, DiffIndex: 12, BeforeValue: 100d, AfterValue: 20d),
        };
        var actions = new[]
        {
            BuildAction(anchorDiffIndex: 10, regularPadId: 1, sourceRetainedPercent: 20, targetDiffIndex: 11, targetRatioPercent: 80),
            BuildAction(anchorDiffIndex: 12, regularPadId: 3, sourceRetainedPercent: 20, targetDiffIndex: 11, targetRatioPercent: 80),
        };

        var result = SimulationSafetyAuditService.Analyze(BuildResult(cells, actions));

        Assert.True(result.HasTargetCoverageRisks);
        Assert.Equal(1, result.TotalTargetCoverageRiskCount);
        var risk = Assert.Single(result.TargetCoverageRisks);
        Assert.Equal(11, risk.DiffIndex);
        Assert.Equal(160d, risk.CoveragePercent);
        Assert.Equal(40d, risk.CoverageExcessPercent);
    }

    private static NotchApplySimulationAction BuildAction(
        int anchorDiffIndex,
        int regularPadId,
        int sourceRetainedPercent,
        int targetDiffIndex,
        int targetRatioPercent)
    {
        return new NotchApplySimulationAction(
            NotchAlgorithmVersion.V22,
            IcIndex: 0,
            AnchorDiffIndex: anchorDiffIndex,
            RegularPadId: regularPadId,
            CadPadId: null,
            SourceRowNumbers: SingleSourceRowNumbers,
            CombinePercent: sourceRetainedPercent + targetRatioPercent,
            SourceRetainedPercent: sourceRetainedPercent,
            SourceBeforeValue: 100d,
            SourceAfterValue: sourceRetainedPercent,
            Legs: new[]
            {
                new NotchApplySimulationLeg(
                    TargetDiffIndex: targetDiffIndex,
                    RatioPercent: targetRatioPercent,
                    DeltaValue: targetRatioPercent),
            });
    }

    private static NotchApplySimulationResult BuildResult(
        IReadOnlyList<NotchApplySimulationDiffCell> cells,
        IReadOnlyList<NotchApplySimulationAction> actions)
    {
        return new NotchApplySimulationResult(
            IsSupported: true,
            Diagnostics: Array.Empty<string>(),
            Version: NotchAlgorithmVersion.V22,
            ContractText: "test",
            AggregationMode: NotchApplySimulationAggregationMode.SingleFrame,
            ConsumedFrameCount: 1,
            Cells: cells,
            Actions: actions,
            DiffIdentityContract: NotchApplySimulationDiffIdentityContract.Empty,
            Histograms: new NotchApplySimulationHistogramSet(
                new NotchApplySimulationHistogram("Before", Array.Empty<NotchApplySimulationHistogramBin>()),
                new NotchApplySimulationHistogram("After", Array.Empty<NotchApplySimulationHistogramBin>()),
                new NotchApplySimulationHistogram("Delta", Array.Empty<NotchApplySimulationHistogramBin>())),
            Heatmap: new NotchApplySimulationHeatmap(
                GridRows: 0,
                GridCols: 0,
                MaxAbsDelta: 0d,
                Cells: Array.Empty<NotchApplySimulationHeatmapCell>(),
                Hotspots: Array.Empty<NotchApplySimulationHotspot>()));
    }
}
