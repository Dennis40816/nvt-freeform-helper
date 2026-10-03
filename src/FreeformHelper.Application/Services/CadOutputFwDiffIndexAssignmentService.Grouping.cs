using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class CadOutputFwDiffIndexAssignmentService
{
    private static Dictionary<int, List<RowGroup>> BuildRowGroups(
        IReadOnlyList<CadPad> orderedPads,
        RegularGrid grid,
        IReadOnlyList<int> perIcCols,
        Dictionary<int, int> cadIcIndexByCadId)
    {
        var rowsByIc = new Dictionary<int, Dictionary<int, RowGroup>>();

        for (var orderIndex = 0; orderIndex < orderedPads.Count; orderIndex++)
        {
            var pad = orderedPads[orderIndex];
            var (row, col) = DxfIndexAssigner.ResolveGridCell(grid, pad.Centroid);
            var icIndex = GridIcChannelAllocationService.ResolveIcIndexForColumn(col, perIcCols);
            cadIcIndexByCadId[pad.Id] = icIndex;

            if (!rowsByIc.TryGetValue(icIndex, out var byRow))
            {
                byRow = new Dictionary<int, RowGroup>();
                rowsByIc[icIndex] = byRow;
            }

            if (!byRow.TryGetValue(row, out var rowGroup))
            {
                rowGroup = new RowGroup(row, orderIndex, new List<CadPad>());
                byRow[row] = rowGroup;
            }

            rowGroup.Pads.Add(pad);
        }

        return rowsByIc.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Values
                .OrderBy(group => group.FirstOrder)
                .ToList());
    }

    private sealed record RowGroup(int RowIndex, int FirstOrder, List<CadPad> Pads);

}
