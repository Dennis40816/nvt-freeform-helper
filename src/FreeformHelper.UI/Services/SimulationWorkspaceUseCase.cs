using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

public enum SimulationScenarioKind
{
    Manual = 0,
    Csv = 1,
    Copper = 2,
    CopperPathSweep = 3,
}

public sealed record SimulationScenario(
    SimulationScenarioKind Kind,
    NotchApplySimulationImportedDataset Dataset,
    NotchAlgorithmVersion Version,
    NotchApplySimulationAggregationMode AggregationMode,
    int SelectedFrameIndex,
    string SourceLabel);

public sealed record SimulationScenarioSnapshot(
    SimulationScenario Scenario,
    NotchApplySimulationReviewSnapshot ReviewSnapshot,
    SimulationSafetyAuditResult SafetyAudit)
{
    public NotchApplySimulationImportedDataset Dataset => ReviewSnapshot.Dataset;
    public NotchTable FilteredTable => ReviewSnapshot.FilteredTable;
    public NotchApplySimulationResult Result => ReviewSnapshot.Result;
    public IReadOnlyList<string> Diagnostics => ReviewSnapshot.Diagnostics;
}

public sealed class SimulationWorkspaceUseCase
{
    private readonly NotchApplySimulationReviewUseCase _reviewUseCase;

    public SimulationWorkspaceUseCase(NotchApplySimulationReviewUseCase reviewUseCase)
    {
        _reviewUseCase = reviewUseCase ?? throw new ArgumentNullException(nameof(reviewUseCase));
    }

    public NotchApplySimulationImportedDataset ImportCsvSources(
        IReadOnlyList<string> sourcePaths,
        RegularGrid grid)
    {
        return _reviewUseCase.ImportSources(sourcePaths, grid);
    }

    public static SimulationScenario BuildManualScenario(
        SimulationWorkspaceSession session,
        NotchAlgorithmVersion version,
        double globalValue,
        IReadOnlyDictionary<int, double> manualOverrides)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SimulationScenario(
            SimulationScenarioKind.Manual,
            BuildManualDataset(session.FwDiffGrid, globalValue, manualOverrides),
            version,
            NotchApplySimulationAggregationMode.SingleFrame,
            SelectedFrameIndex: 0,
            "Manual baseline");
    }

    public static SimulationScenario BuildCsvScenario(
        NotchApplySimulationImportedDataset dataset,
        NotchAlgorithmVersion version,
        NotchApplySimulationAggregationMode aggregationMode,
        int selectedFrameIndex)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        return new SimulationScenario(
            SimulationScenarioKind.Csv,
            dataset,
            version,
            aggregationMode,
            selectedFrameIndex,
            "CSV frame");
    }

    public static SimulationScenario BuildCopperScenario(
        SimulationWorkspaceSession session,
        NotchAlgorithmVersion version,
        CopperPillarSimulationRequest request,
        SimulationScenarioKind kind = SimulationScenarioKind.Copper)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        if (kind is not SimulationScenarioKind.Copper and not SimulationScenarioKind.CopperPathSweep)
        {
            throw new ArgumentException("Copper scenarios must use Copper or CopperPathSweep kind.", nameof(kind));
        }

        return new SimulationScenario(
            kind,
            BuildCopperDataset(session, request),
            version,
            NotchApplySimulationAggregationMode.SingleFrame,
            SelectedFrameIndex: 0,
            kind == SimulationScenarioKind.CopperPathSweep ? "Copper path sweep" : "Copper pillar");
    }

    public static NotchApplySimulationImportedDataset BuildManualDataset(
        RegularGrid grid,
        double globalValue,
        IReadOnlyDictionary<int, double> manualOverrides)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(manualOverrides);

        var rows = new List<IReadOnlyList<double>>(grid.Rows);
        for (var row = 0; row < grid.Rows; row++)
        {
            var values = new double[grid.Cols];
            for (var col = 0; col < grid.Cols; col++)
            {
                var pad = grid.GetPad(row, col);
                values[col] = manualOverrides.TryGetValue(pad.RegularPadId, out var overrideValue)
                    ? overrideValue
                    : globalValue;
            }

            rows.Add(values);
        }

        var frame = new DiffFrameCsvFrame(
            SequenceIndex: 0,
            FrameIndex: 0,
            BreakpointIndex: null,
            TimestampText: "manual",
            Timestamp: null,
            HeaderLineNumber: 0,
            MarkerLineNumber: null,
            DataStartLineNumber: 0,
            DataEndLineNumber: grid.Rows - 1,
            Rows: rows);
        var projection = DiffFrameGridProjectionService.ProjectFrame(frame, grid);

        return new NotchApplySimulationImportedDataset(
            Files: new[]
            {
                new NotchApplySimulationImportedFile(
                    SourcePath: "manual://baseline",
                    FileName: "Manual baseline",
                    FrameCount: 1,
                    CompatibleFrameCount: projection.IsCompatible ? 1 : 0,
                    DeclaredCols: grid.Cols,
                    DeclaredRows: grid.Rows,
                    FirstFrameCols: grid.Cols,
                    FirstFrameRows: grid.Rows,
                    Diagnostics: Array.Empty<string>()),
            },
            Frames: new[]
            {
                new NotchApplySimulationImportedFrame(
                    GlobalIndex: 0,
                    SourceIndex: 0,
                    SourceFrameIndex: 0,
                    SourceFileName: "Manual baseline",
                    Frame: frame,
                    Projection: projection),
            },
            Diagnostics: projection.IsCompatible
                ? Array.Empty<string>()
                : new[] { projection.Diagnostic ?? "Manual baseline is incompatible with the current regular grid." });
    }

    public static NotchApplySimulationImportedDataset BuildCopperDataset(
        SimulationWorkspaceSession session,
        CopperPillarSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var grid = session.FwDiffGrid;
        var projection = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            grid,
            session.CadPads,
            session.CadOutputFwDiffIndices,
            session.CadIcIndices,
            request);
        var rows = new List<IReadOnlyList<double>>(grid.Rows);
        for (var row = 0; row < grid.Rows; row++)
        {
            rows.Add(new double[grid.Cols]);
        }

        foreach (var cell in projection.Cells)
        {
            if (cell.RegularRow >= 0 && cell.RegularRow < grid.Rows &&
                cell.RegularCol >= 0 && cell.RegularCol < grid.Cols)
            {
                ((double[])rows[cell.RegularRow])[cell.RegularCol] = cell.Value;
            }
        }

        var frame = new DiffFrameCsvFrame(
            SequenceIndex: 0,
            FrameIndex: 0,
            BreakpointIndex: null,
            TimestampText: "copper",
            Timestamp: null,
            HeaderLineNumber: 0,
            MarkerLineNumber: null,
            DataStartLineNumber: 0,
            DataEndLineNumber: grid.Rows - 1,
            Rows: rows);

        return new NotchApplySimulationImportedDataset(
            Files: new[]
            {
                new NotchApplySimulationImportedFile(
                    SourcePath: "copper://pillar",
                    FileName: "Copper pillar",
                    FrameCount: 1,
                    CompatibleFrameCount: projection.IsCompatible ? 1 : 0,
                    DeclaredCols: grid.Cols,
                    DeclaredRows: grid.Rows,
                    FirstFrameCols: grid.Cols,
                    FirstFrameRows: grid.Rows,
                    Diagnostics: projection.IsCompatible
                        ? Array.Empty<string>()
                        : new[] { projection.Diagnostic ?? "Copper projection is incompatible with the current regular grid." }),
            },
            Frames: new[]
            {
                new NotchApplySimulationImportedFrame(
                    GlobalIndex: 0,
                    SourceIndex: 0,
                    SourceFrameIndex: 0,
                    SourceFileName: "Copper pillar",
                    Frame: frame,
                    Projection: projection),
            },
            Diagnostics: projection.IsCompatible
                ? Array.Empty<string>()
                : new[] { projection.Diagnostic ?? "Copper projection is incompatible with the current regular grid." });
    }

    public static SimulationScenarioSnapshot BuildScenarioSnapshot(
        SimulationWorkspaceSession session,
        SimulationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(scenario);

        var reviewSnapshot = NotchApplySimulationReviewUseCase.BuildSnapshot(
            scenario.Dataset,
            session.FwDiffGrid,
            session.ActiveRegularPadIds,
            session.Table,
            scenario.Version,
            session.NullDiffValue,
            scenario.AggregationMode,
            scenario.SelectedFrameIndex,
            session.ComputationMode);
        return new SimulationScenarioSnapshot(
            scenario,
            reviewSnapshot,
            SimulationSafetyAuditService.Analyze(reviewSnapshot.Result));
    }

    public static CopperPillarPathReplayResult ReplayCopperPath(
        SimulationWorkspaceSession session,
        NotchAlgorithmVersion version,
        CopperPillarPathReplayRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        var stepCount = Math.Max(1, request.StepCount);
        var steps = new List<CopperPillarPathReplayStep>(stepCount);
        for (var index = 0; index < stepCount; index++)
        {
            var position = Interpolate(request.Start, request.End, index, stepCount);
            var scenario = BuildCopperScenario(
                session,
                version,
                new CopperPillarSimulationRequest(
                    position.X,
                    position.Y,
                    request.Diameter,
                    request.PeakValue,
                    request.BaselineValue,
                    request.CircleSegmentCount),
                SimulationScenarioKind.CopperPathSweep);
            var snapshot = BuildScenarioSnapshot(session, scenario);
            var audit = snapshot.SafetyAudit;
            steps.Add(new CopperPillarPathReplayStep(
                StepIndex: index,
                CenterX: position.X,
                CenterY: position.Y,
                MaxAfterValue: audit.MaxAfterValue,
                EmsViolationCount: audit.TotalViolationCount,
                WorstRegularPadId: audit.MaxAfterRegularPadId,
                WorstIcIndex: audit.MaxAfterIcIndex,
                WorstDiffIndex: audit.MaxAfterDiffIndex,
                NetFlowResidualCount: audit.TotalNetFlowResidualCount,
                TargetCoverageRiskCount: audit.TotalTargetCoverageRiskCount,
                StatusText: SimulationSafetyTextProjector.BuildReplayStatusText(
                    snapshot.Result.IsSupported,
                    audit.HasViolations,
                    audit.HasPhysicalAuditRisks),
                Diagnostics: snapshot.Diagnostics)
            {
                IsSupported = snapshot.Result.IsSupported,
                HasGlobalFlowResidual = audit.HasGlobalFlowResidual,
            });
        }

        return new CopperPillarPathReplayResult(steps);
    }

    private static Point2 Interpolate(Point2 start, Point2 end, int index, int stepCount)
    {
        if (stepCount <= 1)
        {
            return start;
        }

        var ratio = index / (stepCount - 1d);
        return new Point2(
            start.X + ((end.X - start.X) * ratio),
            start.Y + ((end.Y - start.Y) * ratio));
    }

}
