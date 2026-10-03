using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static class NotchCadOutputFwDiffProjectionService
{
    public static NotchCadOutputFwDiffProjectionResult Project(
        RegularGrid grid,
        NotchTable table,
        IReadOnlyDictionary<int, int> cadOutputFwDiffByCadId)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(cadOutputFwDiffByCadId);

        if (table.Rows.Count == 0 || cadOutputFwDiffByCadId.Count == 0)
        {
            return new NotchCadOutputFwDiffProjectionResult(grid, table, 0, 0, 0);
        }

        var changedRows = 0;
        var representativeRegularByCadId = new Dictionary<int, int>();
        var rawDiffByRegularPadId = grid.Pads.ToDictionary(static pad => pad.RegularPadId, static pad => pad.DiffIndex);
        var diffProjectionContract = NotchDiffIdentityPipeline.BuildProjectionContract(table.Rows, cadOutputFwDiffByCadId);
        var projectedRows = new List<NotchTableRow>(table.Rows.Count);

        foreach (var row in table.Rows)
        {
            var projectedAnchorDiff = diffProjectionContract.ResolveAnchorDiff(row);
            if (row.CadPadId is int cadPadId)
            {
                if (rawDiffByRegularPadId.TryGetValue(row.RegularPadIndex, out var rawRegularDiff) &&
                    rawRegularDiff != projectedAnchorDiff)
                {
                    representativeRegularByCadId.TryAdd(cadPadId, row.RegularPadIndex);
                }
            }

            var projectedRow = ProjectRow(row, projectedAnchorDiff);
            if (!ReferenceEquals(projectedRow, row))
            {
                changedRows++;
            }

            projectedRows.Add(projectedRow);
        }

        if (changedRows == 0 && representativeRegularByCadId.Count == 0)
        {
            return new NotchCadOutputFwDiffProjectionResult(
                grid,
                table,
                0,
                0,
                diffProjectionContract.ConflictedRawDiffKeyCount);
        }

        var projectedGrid = ProjectGrid(grid, representativeRegularByCadId, cadOutputFwDiffByCadId);
        return new NotchCadOutputFwDiffProjectionResult(
            projectedGrid,
            new NotchTable(projectedRows, table.ToFullCoverageAudit, table.GenerationPhaseTimings),
            changedRows,
            representativeRegularByCadId.Count,
            diffProjectionContract.ConflictedRawDiffKeyCount);
    }

    private static RegularGrid ProjectGrid(
        RegularGrid grid,
        IReadOnlyDictionary<int, int> representativeRegularByCadId,
        IReadOnlyDictionary<int, int> cadOutputFwDiffByCadId)
    {
        var pads = grid.Pads
            .Select(ClonePad)
            .ToList();
        var padsById = pads.ToDictionary(static pad => pad.RegularPadId);
        foreach (var pair in representativeRegularByCadId)
        {
            var cadPadId = pair.Key;
            var regularPadId = pair.Value;
            if (!cadOutputFwDiffByCadId.TryGetValue(cadPadId, out var projectedDiff))
            {
                continue;
            }

            if (padsById.TryGetValue(regularPadId, out var projectedPad))
            {
                projectedPad.DiffIndex = projectedDiff;
            }
        }

        return new RegularGrid(
            grid.Rows,
            grid.Cols,
            grid.XEdges.ToArray(),
            grid.YEdges.ToArray(),
            pads);
    }

    private static RegularPad ClonePad(RegularPad source)
    {
        var clone = new RegularPad(source.Row, source.Col, source.RegularPadId, source.Polygon)
        {
            IcIndex = source.IcIndex,
            DiffIndex = source.DiffIndex,
            MatchedCadPadId = source.MatchedCadPadId,
            MatchScore = source.MatchScore,
            Freeform = source.Freeform,
        };
        return clone;
    }

    private static NotchTableRow ProjectRow(
        NotchTableRow row,
        int projectedDiff)
    {
        if (row.Version == NotchAlgorithmVersion.V22 && row.V22Node is not null)
        {
            var node = row.V22Node;
            if (projectedDiff == row.DiffIndex)
            {
                return row;
            }

            var projectedNode = new NotchV22Node(
                AnchorDiffIndex: projectedDiff,
                CombinePercent: node.CombinePercent,
                TargetDiffIndex1: node.TargetDiffIndex1,
                TargetRatioPercent1: node.TargetRatioPercent1,
                TargetDiffIndex2: node.TargetDiffIndex2,
                TargetRatioPercent2: node.TargetRatioPercent2,
                Flags: node.Flags);
            return new NotchTableRow(
                row.IcIndex,
                projectedDiff,
                row.RegularPadIndex,
                row.CadPadId,
                projectedNode,
                row.Comment);
        }

        if (projectedDiff == row.DiffIndex)
        {
            return row;
        }

        var values = (int[])row.Values.Clone();
        if (values.Length > 0)
        {
            values[0] = projectedDiff;
        }

        return new NotchTableRow(
            row.Version,
            row.IcIndex,
            projectedDiff,
            row.RegularPadIndex,
            row.CadPadId,
            values,
            row.Comment);
    }
}

public sealed record NotchCadOutputFwDiffProjectionResult(
    RegularGrid Grid,
    NotchTable Table,
    int ChangedRowCount,
    int ChangedAnchorCount,
    int ConflictedRawDiffKeyCount);
