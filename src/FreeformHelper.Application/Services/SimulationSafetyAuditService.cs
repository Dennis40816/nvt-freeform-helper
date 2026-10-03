namespace FreeformHelper.Application.Services;

public sealed record SimulationSafetyViolation(
    int RegularPadId,
    int IcIndex,
    int DiffIndex,
    double BeforeValue,
    double AfterValue,
    double DeltaValue,
    double ExcessValue);

public sealed record SimulationSafetyHighRiskDiff(
    int Rank,
    int RegularPadId,
    int IcIndex,
    int DiffIndex,
    double BeforeValue,
    double AfterValue,
    double DeltaValue,
    double MarginToCap);

public enum SimulationPhysicalAuditClassification
{
    GeometryExpected = 0,
    NetFlowSuspicious = 1,
    EmsRisk = 2,
}

public sealed record SimulationGlobalFlowAudit(
    bool HasActionFlowModel,
    double BeforeTotal,
    double AfterTotal,
    double DeltaTotal,
    double ActionNetFlowTotal,
    double ResidualValue)
{
    public bool IsBalanced => !HasActionFlowModel || Math.Abs(ResidualValue) <= 1e-9;

    public static SimulationGlobalFlowAudit Empty { get; } = new(
        HasActionFlowModel: false,
        BeforeTotal: 0d,
        AfterTotal: 0d,
        DeltaTotal: 0d,
        ActionNetFlowTotal: 0d,
        ResidualValue: 0d);
}

public sealed record SimulationNetFlowAuditDiff(
    int RegularPadId,
    int IcIndex,
    int DiffIndex,
    double CellDeltaValue,
    double SourceDeltaValue,
    double TargetDeltaValue,
    double NetFlowValue,
    double ResidualValue,
    SimulationPhysicalAuditClassification Classification);

public sealed record SimulationTargetCoverageAuditDiff(
    int? RegularPadId,
    int IcIndex,
    int DiffIndex,
    double CoveragePercent,
    double CoverageExcessPercent,
    double MaxAfterValue,
    SimulationPhysicalAuditClassification Classification);

public sealed record SimulationSafetyAuditResult(
    double AfterCap,
    int CellCount,
    double MaxAfterValue,
    int? MaxAfterRegularPadId,
    int? MaxAfterIcIndex,
    int? MaxAfterDiffIndex,
    int TotalViolationCount,
    IReadOnlyList<SimulationSafetyViolation> Violations,
    IReadOnlyList<SimulationSafetyHighRiskDiff> HighRiskDiffs,
    SimulationGlobalFlowAudit GlobalFlowAudit,
    int TotalNetFlowResidualCount,
    IReadOnlyList<SimulationNetFlowAuditDiff> NetFlowResiduals,
    double TargetCoveragePercentCap,
    int TotalTargetCoverageRiskCount,
    IReadOnlyList<SimulationTargetCoverageAuditDiff> TargetCoverageRisks)
{
    public bool HasCells => CellCount > 0;
    public bool HasViolations => TotalViolationCount > 0;
    public bool HasHighRiskDiffs => HighRiskDiffs.Count > 0;
    public bool HasGlobalFlowResidual => !GlobalFlowAudit.IsBalanced;
    public bool HasNetFlowResiduals => TotalNetFlowResidualCount > 0;
    public bool HasTargetCoverageRisks => TotalTargetCoverageRiskCount > 0;
    public bool HasPhysicalAuditRisks =>
        HasGlobalFlowResidual ||
        HasNetFlowResiduals ||
        HasTargetCoverageRisks;

    public static SimulationSafetyAuditResult Empty(double afterCap) => new(
        afterCap,
        CellCount: 0,
        MaxAfterValue: 0d,
        MaxAfterRegularPadId: null,
        MaxAfterIcIndex: null,
        MaxAfterDiffIndex: null,
        TotalViolationCount: 0,
        Violations: Array.Empty<SimulationSafetyViolation>(),
        HighRiskDiffs: Array.Empty<SimulationSafetyHighRiskDiff>(),
        GlobalFlowAudit: SimulationGlobalFlowAudit.Empty,
        TotalNetFlowResidualCount: 0,
        NetFlowResiduals: Array.Empty<SimulationNetFlowAuditDiff>(),
        TargetCoveragePercentCap: SimulationSafetyAuditService.DefaultTargetCoveragePercentCap,
        TotalTargetCoverageRiskCount: 0,
        TargetCoverageRisks: Array.Empty<SimulationTargetCoverageAuditDiff>());
}

public static class SimulationSafetyAuditService
{
    public const double DefaultEmsAfterCap = 480d;
    public const double DefaultTargetCoveragePercentCap = 120d;
    private const double SignificantEpsilon = 1e-9;
    private const int DefaultViolationPreviewCount = 12;
    private const int DefaultHighRiskPreviewCount = 8;

    public static bool IsEmsAfterCapViolation(double afterValue, double afterCap) =>
        afterValue > afterCap + SignificantEpsilon;

    public static SimulationSafetyAuditResult Analyze(
        IReadOnlyList<NotchApplySimulationDiffCell> cells,
        double afterCap = DefaultEmsAfterCap,
        int violationPreviewCount = DefaultViolationPreviewCount,
        int highRiskPreviewCount = DefaultHighRiskPreviewCount)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (cells.Count == 0)
        {
            return SimulationSafetyAuditResult.Empty(afterCap);
        }

        return AnalyzeCells(
            cells,
            afterCap,
            violationPreviewCount,
            highRiskPreviewCount,
            SimulationGlobalFlowAudit.Empty,
            totalNetFlowResidualCount: 0,
            netFlowResiduals: Array.Empty<SimulationNetFlowAuditDiff>(),
            targetCoveragePercentCap: DefaultTargetCoveragePercentCap,
            totalTargetCoverageRiskCount: 0,
            targetCoverageRisks: Array.Empty<SimulationTargetCoverageAuditDiff>());
    }

    public static SimulationSafetyAuditResult Analyze(
        NotchApplySimulationResult result,
        double afterCap = DefaultEmsAfterCap,
        double targetCoveragePercentCap = DefaultTargetCoveragePercentCap,
        int violationPreviewCount = DefaultViolationPreviewCount,
        int highRiskPreviewCount = DefaultHighRiskPreviewCount)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Cells.Count == 0)
        {
            return SimulationSafetyAuditResult.Empty(afterCap);
        }

        var globalFlowAudit = BuildGlobalFlowAudit(result);
        var netFlowResiduals = BuildNetFlowResiduals(
            result.Cells,
            result.Actions,
            afterCap,
            highRiskPreviewCount,
            out var totalNetFlowResidualCount);
        var targetCoverageRisks = BuildTargetCoverageRisks(
            result.Cells,
            result.Actions,
            afterCap,
            targetCoveragePercentCap,
            highRiskPreviewCount,
            out var totalTargetCoverageRiskCount);

        return AnalyzeCells(
            result.Cells,
            afterCap,
            violationPreviewCount,
            highRiskPreviewCount,
            globalFlowAudit,
            totalNetFlowResidualCount,
            netFlowResiduals,
            targetCoveragePercentCap,
            totalTargetCoverageRiskCount,
            targetCoverageRisks);
    }

    private static SimulationSafetyAuditResult AnalyzeCells(
        IReadOnlyList<NotchApplySimulationDiffCell> cells,
        double afterCap,
        int violationPreviewCount,
        int highRiskPreviewCount,
        SimulationGlobalFlowAudit globalFlowAudit,
        int totalNetFlowResidualCount,
        IReadOnlyList<SimulationNetFlowAuditDiff> netFlowResiduals,
        double targetCoveragePercentCap,
        int totalTargetCoverageRiskCount,
        IReadOnlyList<SimulationTargetCoverageAuditDiff> targetCoverageRisks)
    {
        var maxCell = cells
            .OrderByDescending(static cell => cell.AfterValue)
            .ThenBy(static cell => cell.RegularPadId)
            .First();
        var exceedingCells = cells
            .Where(cell => IsEmsAfterCapViolation(cell.AfterValue, afterCap))
            .OrderByDescending(static cell => cell.AfterValue)
            .ThenBy(static cell => cell.RegularPadId)
            .ToArray();
        var preview = exceedingCells
            .Take(Math.Max(0, violationPreviewCount))
            .Select(cell => new SimulationSafetyViolation(
                cell.RegularPadId,
                cell.IcIndex,
                cell.DiffIndex,
                cell.BeforeValue,
                cell.AfterValue,
                cell.DeltaValue,
                cell.AfterValue - afterCap))
            .ToArray();
        var highRiskPreview = cells
            .OrderByDescending(static cell => cell.AfterValue)
            .ThenBy(static cell => cell.RegularPadId)
            .Take(Math.Max(0, highRiskPreviewCount))
            .Select((cell, index) => new SimulationSafetyHighRiskDiff(
                Rank: index + 1,
                RegularPadId: cell.RegularPadId,
                IcIndex: cell.IcIndex,
                DiffIndex: cell.DiffIndex,
                BeforeValue: cell.BeforeValue,
                AfterValue: cell.AfterValue,
                DeltaValue: cell.DeltaValue,
                MarginToCap: afterCap - cell.AfterValue))
            .ToArray();

        return new SimulationSafetyAuditResult(
            afterCap,
            cells.Count,
            maxCell.AfterValue,
            maxCell.RegularPadId,
            maxCell.IcIndex,
            maxCell.DiffIndex,
            exceedingCells.Length,
            preview,
            highRiskPreview,
            globalFlowAudit,
            totalNetFlowResidualCount,
            netFlowResiduals,
            targetCoveragePercentCap,
            totalTargetCoverageRiskCount,
            targetCoverageRisks);
    }

    private static SimulationGlobalFlowAudit BuildGlobalFlowAudit(NotchApplySimulationResult result)
    {
        if (result.Actions.Count == 0)
        {
            return new SimulationGlobalFlowAudit(
                HasActionFlowModel: false,
                BeforeTotal: result.BeforeTotal,
                AfterTotal: result.AfterTotal,
                DeltaTotal: result.DeltaTotal,
                ActionNetFlowTotal: 0d,
                ResidualValue: 0d);
        }

        var actionNetFlowTotal = 0d;
        foreach (var action in result.Actions)
        {
            actionNetFlowTotal += action.SourceAfterValue - action.SourceBeforeValue;
            actionNetFlowTotal += action.Legs.Sum(static leg => leg.DeltaValue);
        }

        return new SimulationGlobalFlowAudit(
            HasActionFlowModel: true,
            BeforeTotal: result.BeforeTotal,
            AfterTotal: result.AfterTotal,
            DeltaTotal: result.DeltaTotal,
            ActionNetFlowTotal: actionNetFlowTotal,
            ResidualValue: result.DeltaTotal - actionNetFlowTotal);
    }

    private static SimulationNetFlowAuditDiff[] BuildNetFlowResiduals(
        IReadOnlyList<NotchApplySimulationDiffCell> cells,
        IReadOnlyList<NotchApplySimulationAction> actions,
        double afterCap,
        int previewCount,
        out int totalResidualCount)
    {
        if (actions.Count == 0)
        {
            totalResidualCount = 0;
            return Array.Empty<SimulationNetFlowAuditDiff>();
        }

        var flowByDiff = BuildFlowByDiff(actions);
        var residuals = new List<SimulationNetFlowAuditDiff>();
        foreach (var cell in cells)
        {
            var key = new NotchFlowKey(cell.IcIndex, cell.DiffIndex);
            flowByDiff.TryGetValue(key, out var flow);
            var sourceDelta = flow?.SourceDelta ?? 0d;
            var targetDelta = flow?.TargetDelta ?? 0d;
            var netFlow = sourceDelta + targetDelta;
            var residual = cell.DeltaValue - netFlow;
            if (Math.Abs(residual) <= SignificantEpsilon)
            {
                continue;
            }

            residuals.Add(new SimulationNetFlowAuditDiff(
                cell.RegularPadId,
                cell.IcIndex,
                cell.DiffIndex,
                cell.DeltaValue,
                sourceDelta,
                targetDelta,
                netFlow,
                residual,
                IsEmsAfterCapViolation(cell.AfterValue, afterCap)
                    ? SimulationPhysicalAuditClassification.EmsRisk
                    : SimulationPhysicalAuditClassification.NetFlowSuspicious));
        }

        totalResidualCount = residuals.Count;
        return residuals
            .OrderByDescending(static item => Math.Abs(item.ResidualValue))
            .ThenBy(static item => item.RegularPadId)
            .Take(Math.Max(0, previewCount))
            .ToArray();
    }

    private static SimulationTargetCoverageAuditDiff[] BuildTargetCoverageRisks(
        IReadOnlyList<NotchApplySimulationDiffCell> cells,
        IReadOnlyList<NotchApplySimulationAction> actions,
        double afterCap,
        double targetCoveragePercentCap,
        int previewCount,
        out int totalRiskCount)
    {
        var coveragePercentByDiff = new Dictionary<NotchFlowKey, double>();
        foreach (var action in actions)
        {
            AddCoverage(
                coveragePercentByDiff,
                new NotchFlowKey(action.IcIndex, action.AnchorDiffIndex),
                action.SourceRetainedPercent);
            foreach (var leg in action.Legs)
            {
                AddCoverage(
                    coveragePercentByDiff,
                    new NotchFlowKey(action.IcIndex, leg.TargetDiffIndex),
                    leg.RatioPercent);
            }
        }

        if (coveragePercentByDiff.Count == 0)
        {
            totalRiskCount = 0;
            return Array.Empty<SimulationTargetCoverageAuditDiff>();
        }

        var cellsByDiff = cells
            .GroupBy(static cell => new NotchFlowKey(cell.IcIndex, cell.DiffIndex))
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var risks = new List<SimulationTargetCoverageAuditDiff>();
        foreach (var pair in coveragePercentByDiff)
        {
            var excess = pair.Value - targetCoveragePercentCap;
            if (excess <= SignificantEpsilon)
            {
                continue;
            }

            cellsByDiff.TryGetValue(pair.Key, out var diffCells);
            var primaryCell = diffCells?
                .OrderByDescending(static cell => cell.AfterValue)
                .ThenBy(static cell => cell.RegularPadId)
                .FirstOrDefault();
            var maxAfter = diffCells?.Length > 0
                ? diffCells.Max(static cell => cell.AfterValue)
                : 0d;
            risks.Add(new SimulationTargetCoverageAuditDiff(
                primaryCell?.RegularPadId,
                pair.Key.IcIndex,
                pair.Key.DiffIndex,
                pair.Value,
                excess,
                maxAfter,
                IsEmsAfterCapViolation(maxAfter, afterCap)
                    ? SimulationPhysicalAuditClassification.EmsRisk
                    : SimulationPhysicalAuditClassification.GeometryExpected));
        }

        totalRiskCount = risks.Count;
        return risks
            .OrderByDescending(static item => item.CoveragePercent)
            .ThenBy(static item => item.RegularPadId ?? int.MaxValue)
            .Take(Math.Max(0, previewCount))
            .ToArray();
    }

    private static Dictionary<NotchFlowKey, FlowAccumulator> BuildFlowByDiff(
        IReadOnlyList<NotchApplySimulationAction> actions)
    {
        var flowByDiff = new Dictionary<NotchFlowKey, FlowAccumulator>();
        foreach (var action in actions)
        {
            var sourceDelta = action.SourceAfterValue - action.SourceBeforeValue;
            var sourceFlow = GetOrAddFlow(flowByDiff, new NotchFlowKey(action.IcIndex, action.AnchorDiffIndex));
            sourceFlow.SourceDelta += sourceDelta;

            foreach (var leg in action.Legs)
            {
                var targetFlow = GetOrAddFlow(flowByDiff, new NotchFlowKey(action.IcIndex, leg.TargetDiffIndex));
                targetFlow.TargetDelta += leg.DeltaValue;
            }
        }

        return flowByDiff;
    }

    private static FlowAccumulator GetOrAddFlow(
        Dictionary<NotchFlowKey, FlowAccumulator> flowByDiff,
        NotchFlowKey key)
    {
        if (!flowByDiff.TryGetValue(key, out var flow))
        {
            flow = new FlowAccumulator();
            flowByDiff[key] = flow;
        }

        return flow;
    }

    private static void AddCoverage(
        Dictionary<NotchFlowKey, double> coveragePercentByDiff,
        NotchFlowKey key,
        int ratioPercent)
    {
        coveragePercentByDiff.TryGetValue(key, out var existing);
        coveragePercentByDiff[key] = existing + ratioPercent;
    }

    private readonly record struct NotchFlowKey(int IcIndex, int DiffIndex);

    private sealed class FlowAccumulator
    {
        public double SourceDelta { get; set; }
        public double TargetDelta { get; set; }
    }
}
