using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

public static class NotchApplySimulationService
{
    private const int HistogramBinCount = 12;
    private const int HeatmapHotspotCount = 12;

    public static NotchApplySimulationResult Simulate(NotchApplySimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Grid);
        ArgumentNullException.ThrowIfNull(request.ActiveRegularPadIds);
        ArgumentNullException.ThrowIfNull(request.FrameProjections);
        ArgumentNullException.ThrowIfNull(request.Table);

        var diagnostics = new List<string>();
        var selectedFrames = SelectFrames(request.FrameProjections, request.AggregationMode, request.SelectedFrameIndex, diagnostics);
        if (selectedFrames.Count == 0)
        {
            diagnostics.Add("No compatible projected diff frames are available for simulation.");
            return NotchApplySimulationResult.Unsupported(
                request.Version,
                request.AggregationMode,
                consumedFrameCount: 0,
                diagnostics.ToArray());
        }

        var beforeByPadId = AggregateBeforeValues(request.Grid, request.ActiveRegularPadIds, selectedFrames);
        var diffBaseline = NotchDiffIdentityPipeline.BuildActiveDiffBaseline(
            request.Grid.Pads,
            request.ActiveRegularPadIds,
            beforeByPadId);
        var diffIdentityContract = BuildDiffIdentityContract(diffBaseline);
        AppendDuplicateDiffDiagnostics(diffBaseline, diagnostics);
        var beforeByDiff = diffBaseline.ValueByDiffKey;
        IReadOnlyDictionary<NotchDiffKey, double> effectiveBeforeByDiff = beforeByDiff;
        var afterByDiff = new Dictionary<NotchDiffKey, double>(beforeByDiff);
        var actions = new List<NotchApplySimulationAction>();

        if (request.Version == NotchAlgorithmVersion.V21)
        {
            var firmwareProjection = NotchV21FirmwareProjector.Project(
                request.Table.Rows,
                request.NullDiffValue,
                ResolveIcCount(request),
                request.ComputationMode);
            var firmwareEvaluation = NotchV21FirmwareEvaluator.Apply(
                firmwareProjection,
                beforeByDiff,
                diagnostics);
            effectiveBeforeByDiff = firmwareEvaluation.BeforeByDiff;
            afterByDiff = firmwareEvaluation.AfterByDiff;
            actions.AddRange(BuildV21Actions(
                firmwareProjection.SourceRows,
                effectiveBeforeByDiff,
                diagnostics));
        }
        else if (request.Version == NotchAlgorithmVersion.V22)
        {
            var firmwareProjection = NotchV22FirmwareProjector.Project(
                request.Table.Rows,
                request.NullDiffValue,
                ResolveIcCount(request));
            var firmwareBeforeByDiff = beforeByDiff.ToDictionary(
                static item => item.Key,
                static item => NotchV21FirmwareEvaluator.QuantizeToFirmwareInt16(item.Value));
            var firmwareAfterByDiff = new Dictionary<NotchDiffKey, short>(firmwareBeforeByDiff);
            var firmwareEvaluations = ApplyV22FirmwareRows(
                firmwareProjection.NodesByIc,
                firmwareBeforeByDiff,
                firmwareAfterByDiff);
            effectiveBeforeByDiff = firmwareBeforeByDiff.ToDictionary(
                static item => item.Key,
                static item => (double)item.Value);
            afterByDiff = firmwareAfterByDiff.ToDictionary(
                static item => item.Key,
                static item => (double)item.Value);
            actions.AddRange(BuildV22Actions(
                firmwareProjection.SourceRows,
                firmwareEvaluations,
                firmwareBeforeByDiff,
                diagnostics));
        }
        else
        {
            diagnostics.Add($"Apply simulation for {request.Version.ToDisplayLabel()} is not yet defined in a single-source contract.");
            return NotchApplySimulationResult.Unsupported(
                request.Version,
                request.AggregationMode,
                selectedFrames.Count,
                diagnostics.ToArray());
        }

        var cells = request.Grid.Pads
            .OrderBy(static pad => pad.Row)
            .ThenBy(static pad => pad.Col)
            .Select(pad =>
            {
                var isActiveSurfacePad = request.ActiveRegularPadIds.Contains(pad.RegularPadId);
                var diffKey = new NotchDiffKey(pad.IcIndex, pad.DiffIndex);
                var beforeValue = !isActiveSurfacePad
                    ? 0d
                    : effectiveBeforeByDiff.GetValueOrDefault(diffKey);
                var afterValue = afterByDiff.GetValueOrDefault(diffKey, beforeValue);
                return new NotchApplySimulationDiffCell(
                    pad.RegularPadId,
                    pad.Row,
                    pad.Col,
                    pad.IcIndex,
                    pad.DiffIndex,
                    beforeValue,
                    afterValue);
            })
            .ToList();

        var histograms = BuildHistograms(cells);
        var heatmap = BuildHeatmap(request.Grid, cells);
        var contractText = request.Version switch
        {
            NotchAlgorithmVersion.V22 => "V2.2 diff-centric apply",
            NotchAlgorithmVersion.V21 when request.ComputationMode == NotchComputationMode.LegacyRegularAnchor =>
                "V2.1 legacy firmware apply",
            NotchAlgorithmVersion.V21 => "V2.1 projected apply (v2.2 canonical)",
            _ => string.Empty,
        };

        return new NotchApplySimulationResult(
            IsSupported: true,
            Diagnostics: diagnostics,
            Version: request.Version,
            ContractText: contractText,
            AggregationMode: request.AggregationMode,
            ConsumedFrameCount: selectedFrames.Count,
            Cells: cells,
            Actions: actions,
            DiffIdentityContract: diffIdentityContract,
            Histograms: histograms,
            Heatmap: heatmap);
    }

    private static int ResolveIcCount(NotchApplySimulationRequest request)
    {
        var maxGridIc = request.Grid.Pads.Count == 0
            ? -1
            : request.Grid.Pads.Max(static pad => pad.IcIndex);
        var maxRowIc = request.Table.Rows.Count == 0
            ? -1
            : request.Table.Rows.Max(static row => row.IcIndex);
        return Math.Max(1, Math.Max(maxGridIc, maxRowIc) + 1);
    }

    private static List<NotchApplySimulationAction> BuildV21Actions(
        IReadOnlyList<NotchV21ProjectedSourceRow> sourceRows,
        IReadOnlyDictionary<NotchDiffKey, double> beforeByDiff,
        List<string> diagnostics)
    {
        var actions = new List<NotchApplySimulationAction>();
        var groups = sourceRows
            .GroupBy(static row => new SimulationOperationKey(
                row.IcIndex,
                row.SourceDiffIndex,
                row.RegularPadId,
                row.CadPadId))
            .OrderBy(static group => group.Min(static row => row.RowNumber));

        foreach (var group in groups)
        {
            var orderedRows = group.OrderBy(static row => row.RowNumber).ToList();
            var first = orderedRows[0];
            var sourceKey = new NotchDiffKey(first.IcIndex, first.SourceDiffIndex);
            if (!beforeByDiff.TryGetValue(sourceKey, out var rawSourceBefore))
            {
                diagnostics.Add(
                    $"Missing anchor diff IC{first.IcIndex + 1}/diff{first.SourceDiffIndex} in projected grid.");
                continue;
            }

            var sourceBefore = NotchV21FirmwareEvaluator.QuantizeToFirmwareInt16(rawSourceBefore);
            var sourceRetainedPercent = 100 + orderedRows.Sum(static row =>
                NotchV21Q7Codec.DecodeSignedPercent(row.SelfType, row.SelfRatioQ7));

            short sourceAfter = sourceBefore;
            foreach (var row in orderedRows)
            {
                var sourceDelta = NotchV21Q7Codec.ScaleSigned(
                    sourceBefore,
                    row.SelfType,
                    row.SelfRatioQ7);
                sourceAfter = unchecked((short)(sourceAfter + sourceDelta));
            }

            var appliedLegs = orderedRows
                .SelectMany(static row => row.Legs)
                .GroupBy(static leg => leg.TargetDiffIndex)
                .Select(groupedLegs =>
                {
                    short delta = 0;
                    foreach (var leg in groupedLegs)
                    {
                        var scaled = NotchV21Q7Codec.ScaleSigned(sourceBefore, leg.Type, leg.RatioQ7);
                        delta = unchecked((short)(delta + scaled));
                    }

                    return new NotchApplySimulationLeg(
                        groupedLegs.Key,
                        groupedLegs.Sum(static leg => leg.SignedPercent),
                        delta);
                })
                .Where(static leg => leg.RatioPercent != 0 || Math.Abs(leg.DeltaValue) > 1e-9)
                .OrderByDescending(static leg => Math.Abs(leg.RatioPercent))
                .ThenBy(static leg => leg.TargetDiffIndex)
                .ToList();

            actions.Add(new NotchApplySimulationAction(
                NotchAlgorithmVersion.V21,
                first.IcIndex,
                first.SourceDiffIndex,
                first.RegularPadId,
                first.CadPadId,
                orderedRows.Select(static row => row.RowNumber).ToArray(),
                first.CombinePercent,
                sourceRetainedPercent,
                sourceBefore,
                sourceAfter,
                appliedLegs));
        }

        return actions;
    }

    private static Dictionary<int, V22FirmwareRowEvaluation> ApplyV22FirmwareRows(
        IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> nodesByIc,
        Dictionary<NotchDiffKey, short> beforeByDiff,
        Dictionary<NotchDiffKey, short> afterByDiff)
    {
        var evaluations = new Dictionary<int, V22FirmwareRowEvaluation>();
        foreach (var nodes in nodesByIc)
        {
            foreach (var node in nodes)
            {
                var sourceKey = new NotchDiffKey(node.IcIndex, node.SourceDiffIndex);
                if (!beforeByDiff.TryGetValue(sourceKey, out var sourceBeforeValue))
                {
                    continue;
                }

                var rawRetainedPercent = node.CombinePercent -
                                         node.FirstRatioPercent -
                                         node.SecondRatioPercent;
                var retainedWasClamped = !node.IsContinuation && rawRetainedPercent < 0;
                var retainedPercent = node.IsContinuation ? 100 : Math.Max(0, rawRetainedPercent);
                var sourceDelta = node.IsContinuation
                    ? (short)0
                    : ScaleV22FirmwarePercent(sourceBeforeValue, retainedPercent - 100);
                if (!node.IsContinuation)
                {
                    afterByDiff[sourceKey] = AddV22FirmwareValue(afterByDiff[sourceKey], sourceDelta);
                }

                var targets = new List<V22FirmwareTargetEvaluation>(2);
                EvaluateTarget(node.FirstTargetDiffIndex, node.FirstRatioPercent);
                EvaluateTarget(node.SecondTargetDiffIndex, node.SecondRatioPercent);
                evaluations[node.SourceOrdinal] = new V22FirmwareRowEvaluation(
                    node,
                    sourceBeforeValue,
                    retainedPercent,
                    retainedWasClamped,
                    sourceDelta,
                    targets);

                void EvaluateTarget(int? targetDiffIndex, int ratioPercent)
                {
                    if (!targetDiffIndex.HasValue || ratioPercent == 0)
                    {
                        return;
                    }

                    var targetKey = new NotchDiffKey(node.IcIndex, targetDiffIndex.Value);
                    var delta = ScaleV22FirmwarePercent(sourceBeforeValue, ratioPercent);
                    var targetFound = afterByDiff.TryGetValue(targetKey, out var targetValue);
                    if (targetFound)
                    {
                        afterByDiff[targetKey] = AddV22FirmwareValue(targetValue, delta);
                    }

                    targets.Add(new V22FirmwareTargetEvaluation(
                        targetDiffIndex.Value,
                        ratioPercent,
                        delta,
                        targetFound));
                }
            }
        }

        return evaluations;
    }

    private static short ScaleV22FirmwarePercent(short value, int percent) =>
        unchecked((short)((value * percent) / 100));

    private static short AddV22FirmwareValue(short value, short delta) =>
        unchecked((short)(value + delta));

    private static List<NotchApplySimulationAction> BuildV22Actions(
        IReadOnlyList<NotchV22FirmwareRow> sourceRows,
        IReadOnlyDictionary<int, V22FirmwareRowEvaluation> evaluationsByOrdinal,
        Dictionary<NotchDiffKey, short> beforeByDiff,
        List<string> diagnostics)
    {
        var actions = new List<NotchApplySimulationAction>();
        var groups = sourceRows
            .GroupBy(static row => new SimulationOperationKey(
                row.IcIndex,
                row.SourceDiffIndex,
                row.RegularPadId,
                row.CadPadId));

        foreach (var group in groups)
        {
            var orderedRows = group.ToList();
            var evaluatedRows = orderedRows
                .Where(row => evaluationsByOrdinal.ContainsKey(row.SourceOrdinal))
                .Select(row => evaluationsByOrdinal[row.SourceOrdinal])
                .ToList();
            var firstProjectedRow = orderedRows[0];
            var sourceKey = new NotchDiffKey(firstProjectedRow.IcIndex, firstProjectedRow.SourceDiffIndex);
            if (evaluatedRows.Count == 0)
            {
                if (!beforeByDiff.ContainsKey(sourceKey))
                {
                    diagnostics.Add(
                        $"Missing anchor diff IC{firstProjectedRow.IcIndex + 1}/" +
                        $"diff{firstProjectedRow.SourceDiffIndex} in projected grid.");
                }

                continue;
            }

            var first = evaluatedRows[0].Row;
            var mainEvaluations = evaluatedRows.Where(static evaluation => !evaluation.Row.IsContinuation).ToList();
            var combinePercent = mainEvaluations.Count == 0
                ? first.CombinePercent
                : mainEvaluations[^1].Row.CombinePercent;
            var sourceRetainedPercent = 100 + mainEvaluations.Sum(static evaluation =>
                evaluation.RetainedPercent - 100);
            var sourceBefore = evaluatedRows[0].SourceBefore;
            short sourceAfter = sourceBefore;
            foreach (var evaluation in mainEvaluations)
            {
                sourceAfter = AddV22FirmwareValue(sourceAfter, evaluation.SourceDelta);
            }

            if (mainEvaluations.Any(static evaluation => evaluation.RetainedWasClamped))
            {
                diagnostics.Add(
                    $"Source retained percent below zero for IC{first.IcIndex + 1}/diff{first.SourceDiffIndex}; clamped to zero.");
            }

            var projectedLegs = evaluatedRows
                .SelectMany(static evaluation => evaluation.Targets)
                .GroupBy(static target => target.TargetDiffIndex)
                .Select(groupedTargets =>
                {
                    short delta = 0;
                    foreach (var target in groupedTargets)
                    {
                        delta = AddV22FirmwareValue(delta, target.DeltaValue);
                    }

                    return (
                        Leg: new NotchApplySimulationLeg(
                            groupedTargets.Key,
                            groupedTargets.Sum(static target => target.RatioPercent),
                            delta),
                        TargetFound: groupedTargets.All(static target => target.TargetFound));
                })
                .Where(static item => item.Leg.RatioPercent != 0 || Math.Abs(item.Leg.DeltaValue) > 1e-9)
                .OrderByDescending(static item => Math.Abs(item.Leg.RatioPercent))
                .ThenBy(static item => item.Leg.TargetDiffIndex)
                .ToList();

            foreach (var projectedLeg in projectedLegs.Where(static item => !item.TargetFound))
            {
                diagnostics.Add(
                    $"Missing target diff IC{first.IcIndex + 1}/diff{projectedLeg.Leg.TargetDiffIndex} in projected grid.");
            }

            actions.Add(new NotchApplySimulationAction(
                NotchAlgorithmVersion.V22,
                first.IcIndex,
                first.SourceDiffIndex,
                first.RegularPadId,
                first.CadPadId,
                evaluatedRows.Select(static evaluation => evaluation.Row.SourceOrdinal).ToArray(),
                combinePercent,
                sourceRetainedPercent,
                sourceBefore,
                sourceAfter,
                projectedLegs.Select(static item => item.Leg).ToList()));
        }

        return actions;
    }

    private static void AppendDuplicateDiffDiagnostics(
        NotchActiveDiffBaseline diffBaseline,
        List<string> diagnostics)
    {
        foreach (var duplicateResolution in diffBaseline.DuplicateResolutions)
        {
            var mergedRegularPads = new[] { duplicateResolution.PrimaryRegularPadId }
                .Concat(duplicateResolution.SuppressedRegularPadIds)
                .OrderBy(static regularPadId => regularPadId)
                .ToArray();
            diagnostics.Add(
                $"Duplicate active diff key detected for IC{duplicateResolution.DiffKey.IcIndex + 1}/diff{duplicateResolution.DiffKey.DiffIndex}; merged REG {string.Join(", ", mergedRegularPads)} by sum.");
        }
    }

    private static NotchApplySimulationDiffIdentityContract BuildDiffIdentityContract(
        NotchActiveDiffBaseline diffBaseline)
    {
        var duplicateResolutions = diffBaseline.DuplicateResolutions
            .Select(static resolution => new NotchApplySimulationDuplicateDiffResolution(
                IcIndex: resolution.DiffKey.IcIndex,
                DiffIndex: resolution.DiffKey.DiffIndex,
                PrimaryRegularPadId: resolution.PrimaryRegularPadId,
                SuppressedRegularPadIds: resolution.SuppressedRegularPadIds.ToArray(),
                Strategy: NotchApplySimulationDuplicateDiffResolutionStrategy.MergeSumActiveRegularPads))
            .ToArray();
        return new NotchApplySimulationDiffIdentityContract(
            DuplicateResolutionStrategy: NotchApplySimulationDuplicateDiffResolutionStrategy.MergeSumActiveRegularPads,
            DuplicateResolutions: duplicateResolutions);
    }

    private static IReadOnlyList<DiffFrameGridProjectionResult> SelectFrames(
        IReadOnlyList<DiffFrameGridProjectionResult> projections,
        NotchApplySimulationAggregationMode aggregationMode,
        int selectedFrameIndex,
        List<string> diagnostics)
    {
        if (aggregationMode == NotchApplySimulationAggregationMode.SingleFrame)
        {
            if (selectedFrameIndex < 0 || selectedFrameIndex >= projections.Count)
            {
                diagnostics.Add($"Selected frame index {selectedFrameIndex} is out of range.");
                return Array.Empty<DiffFrameGridProjectionResult>();
            }

            var projection = projections[selectedFrameIndex];
            if (!projection.IsCompatible)
            {
                diagnostics.Add(projection.Diagnostic ?? $"Selected frame {selectedFrameIndex} is incompatible with the current regular grid.");
                return Array.Empty<DiffFrameGridProjectionResult>();
            }

            return new[] { projection };
        }

        var compatible = projections.Where(static projection => projection.IsCompatible).ToList();
        var skippedCount = projections.Count - compatible.Count;
        if (skippedCount > 0)
        {
            diagnostics.Add($"Ignored {skippedCount} incompatible projected frame(s) during mean aggregation.");
        }

        return compatible;
    }

    private static Dictionary<int, double> AggregateBeforeValues(
        FreeformHelper.Domain.Pads.RegularGrid grid,
        IReadOnlySet<int> activeRegularPadIds,
        IReadOnlyList<DiffFrameGridProjectionResult> selectedFrames)
    {
        var cellMaps = selectedFrames
            .Select(static frame => frame.Cells.ToDictionary(static cell => cell.RegularPadId, static cell => cell.Value))
            .ToList();
        var divisor = Math.Max(selectedFrames.Count, 1);
        var values = new Dictionary<int, double>(grid.Pads.Count);

        foreach (var pad in grid.Pads)
        {
            if (!activeRegularPadIds.Contains(pad.RegularPadId))
            {
                values[pad.RegularPadId] = 0d;
                continue;
            }

            var sum = 0d;
            foreach (var frameMap in cellMaps)
            {
                sum += frameMap.GetValueOrDefault(pad.RegularPadId);
            }

            values[pad.RegularPadId] = sum / divisor;
        }

        return values;
    }

    private static NotchApplySimulationHistogramSet BuildHistograms(List<NotchApplySimulationDiffCell> cells)
    {
        var beforeValues = cells.Select(static cell => cell.BeforeValue).ToList();
        var afterValues = cells.Select(static cell => cell.AfterValue).ToList();
        var deltaValues = cells.Select(static cell => cell.DeltaValue).ToList();

        var beforeAfterMin = Math.Min(
            beforeValues.Count == 0 ? 0d : beforeValues.Min(),
            afterValues.Count == 0 ? 0d : afterValues.Min());
        var beforeAfterMax = Math.Max(
            beforeValues.Count == 0 ? 0d : beforeValues.Max(),
            afterValues.Count == 0 ? 0d : afterValues.Max());
        var deltaMaxAbs = deltaValues.Count == 0 ? 0d : deltaValues.Max(static value => Math.Abs(value));

        return new NotchApplySimulationHistogramSet(
            BuildHistogram("Before", beforeValues, beforeAfterMin, beforeAfterMax),
            BuildHistogram("After", afterValues, beforeAfterMin, beforeAfterMax),
            BuildHistogram("Delta", deltaValues, -deltaMaxAbs, deltaMaxAbs));
    }

    private static NotchApplySimulationHistogram BuildHistogram(
        string title,
        List<double> values,
        double min,
        double max)
    {
        if (values.Count == 0)
        {
            return new NotchApplySimulationHistogram(title, Array.Empty<NotchApplySimulationHistogramBin>());
        }

        if (Math.Abs(max - min) < 1e-9)
        {
            return new NotchApplySimulationHistogram(
                title,
                new[]
                {
                    new NotchApplySimulationHistogramBin(0, FormatBinLabel(min, max), min, max, values.Count),
                });
        }

        var width = (max - min) / HistogramBinCount;
        var counts = new int[HistogramBinCount];
        foreach (var value in values)
        {
            var normalized = (value - min) / Math.Max(max - min, 1e-12);
            var index = Math.Clamp((int)Math.Floor(normalized * HistogramBinCount), 0, HistogramBinCount - 1);
            counts[index]++;
        }

        var bins = new List<NotchApplySimulationHistogramBin>(HistogramBinCount);
        for (var index = 0; index < HistogramBinCount; index++)
        {
            var start = min + (index * width);
            var end = index == HistogramBinCount - 1 ? max : start + width;
            bins.Add(new NotchApplySimulationHistogramBin(
                index,
                FormatBinLabel(start, end),
                start,
                end,
                counts[index]));
        }

        return new NotchApplySimulationHistogram(title, bins);
    }

    private static string FormatBinLabel(double start, double end)
    {
        static string Format(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return $"{Format(start)} - {Format(end)}";
    }

    private static NotchApplySimulationHeatmap BuildHeatmap(
        FreeformHelper.Domain.Pads.RegularGrid grid,
        List<NotchApplySimulationDiffCell> cells)
    {
        var maxAbsDelta = cells.Count == 0 ? 0d : cells.Max(static cell => Math.Abs(cell.DeltaValue));
        var heatmapCells = cells
            .Select(static cell => new NotchApplySimulationHeatmapCell(
                cell.RegularPadId,
                cell.RegularRow,
                cell.RegularCol,
                cell.IcIndex,
                cell.DiffIndex,
                cell.DeltaValue))
            .ToList();
        var hotspots = cells
            .Where(static cell => Math.Abs(cell.DeltaValue) > 1e-9)
            .OrderByDescending(static cell => Math.Abs(cell.DeltaValue))
            .ThenBy(static cell => cell.IcIndex)
            .ThenBy(static cell => cell.DiffIndex)
            .Take(HeatmapHotspotCount)
            .Select((cell, index) => new NotchApplySimulationHotspot(
                index + 1,
                cell.RegularPadId,
                cell.RegularRow,
                cell.RegularCol,
                cell.IcIndex,
                cell.DiffIndex,
                cell.BeforeValue,
                cell.AfterValue,
                cell.DeltaValue))
            .ToList();

        return new NotchApplySimulationHeatmap(
            grid.Rows,
            grid.Cols,
            maxAbsDelta,
            heatmapCells,
            hotspots);
    }

    private sealed record SimulationOperationKey(int IcIndex, int AnchorDiffIndex, int RegularPadId, int? CadPadId);

    private sealed record V22FirmwareRowEvaluation(
        NotchV22FirmwareRow Row,
        short SourceBefore,
        int RetainedPercent,
        bool RetainedWasClamped,
        short SourceDelta,
        IReadOnlyList<V22FirmwareTargetEvaluation> Targets);

    private sealed record V22FirmwareTargetEvaluation(
        int TargetDiffIndex,
        int RatioPercent,
        short DeltaValue,
        bool TargetFound);
}

