using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

internal static class NotchSparseResultUseCase
{
    public static NotchTableGenerator.CadAllocationSparseResultRequest? CreateRequest(
        IReadOnlySet<int> selectedCadIds,
        CadPadSet cad,
        RegularGrid? currentGrid,
        RegularGrid generationGrid,
        WorkflowDataSnapshot workflow,
        Func<CadPad, NotchV22ResolvedResult?> resolveReusable)
    {
        if (!ReferenceEquals(currentGrid, generationGrid) || selectedCadIds.Count != 1)
        {
            return null;
        }

        var cadPadId = selectedCadIds.First();
        var cadPad = cad.Pads.FirstOrDefault(pad => pad.Id == cadPadId);
        if (cadPad is null ||
            !workflow.TryGetCadIcIndex(cadPadId, out var anchorIcIndex) ||
            !workflow.TryGetCadOutputFwDiffIndex(cadPadId, out var anchorDiffIndex))
        {
            return null;
        }

        return new(cadPadId, anchorIcIndex, anchorDiffIndex, resolveReusable(cadPad));
    }

    public static NotchV22ResolvedResult? GetCurrentResult(
        NotchTableGenerator.CadAllocationResolvedBatch batch,
        NotchTableGenerator.CadAllocationSparseResultRequest request,
        IReadOnlySet<int> selectedCadIds,
        RegularGrid? currentGrid,
        RegularGrid generationGrid,
        WorkflowDataSnapshot workflow)
        => ReferenceEquals(currentGrid, generationGrid) &&
           selectedCadIds.Count == 1 &&
           selectedCadIds.Contains(request.CadPadId) &&
           batch.SelectedSparseResult is { } result &&
           result.CadPadId == request.CadPadId &&
           result.AnchorIcIndex == request.AnchorIcIndex &&
           result.AnchorDiffIndex == request.AnchorDiffIndex &&
           workflow.TryGetCadIcIndex(request.CadPadId, out var currentAnchorIcIndex) &&
           currentAnchorIcIndex == request.AnchorIcIndex &&
           workflow.TryGetCadOutputFwDiffIndex(request.CadPadId, out var currentAnchorDiffIndex) &&
           currentAnchorDiffIndex == request.AnchorDiffIndex
            ? result.ResolvedResult
            : null;
}
