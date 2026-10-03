using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchTableGenerator
{
    private void ResetCadAllocationMemoIfGridChanged(RegularGrid grid)
    {
        if (ReferenceEquals(_cadAllocationMemoGrid, grid))
        {
            return;
        }

        _cadAllocationMemoGrid = grid;
        _cadAllocationMemoByCadId.Clear();
    }

    private IReadOnlyList<NotchAllocation> GetOrBuildCadAllocations(CadPad cadPad, RegularGrid grid)
    {
        ResetCadAllocationMemoIfGridChanged(grid);
        if (_cadAllocationMemoByCadId.TryGetValue(cadPad.Id, out var cached) &&
            IsSameCadGeometry(cadPad.Polygon, cached.CadPolygon))
        {
            return cached.Allocations;
        }

        var allocations = NotchAllocationService.BuildAllocations(cadPad, grid);
        if (_cadAllocationMemoByCadId.Count >= CadAllocationMemoMaxEntries &&
            !_cadAllocationMemoByCadId.ContainsKey(cadPad.Id))
        {
            _cadAllocationMemoByCadId.Clear();
        }

        _cadAllocationMemoByCadId[cadPad.Id] = new CadAllocationMemoEntry(cadPad.Polygon, allocations);
        return allocations;
    }

    private static bool IsSameCadGeometry(Polygon2 current, Polygon2 cached) =>
        ReferenceEquals(current, cached) ||
        string.Equals(
            CadPadGeometrySignature.BuildExact(current),
            CadPadGeometrySignature.BuildExact(cached),
            StringComparison.Ordinal);

    private sealed record CadAllocationMemoEntry(
        Polygon2 CadPolygon,
        IReadOnlyList<NotchAllocation> Allocations);
}
