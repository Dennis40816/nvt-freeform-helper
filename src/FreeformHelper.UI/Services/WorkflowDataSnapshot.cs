using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Services;

internal sealed record WorkflowDataSnapshot(
    IReadOnlyDictionary<int, int> CadOutputFwDiffIndexByCadId,
    IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> CadOutputFwDiffAssignmentDecisionsByCadId,
    IReadOnlyDictionary<int, int> CadIcIndexByCadId,
    IReadOnlyDictionary<int, int> CadDisplayIndexByCadId,
    IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> CadToRegularByCadId,
    IReadOnlyDictionary<int, int> CadOutputFwDiffIndexOverridesByCadId,
    IReadOnlySet<int>? ActiveRegularVisibilityMaskPadIds)
{
    public static readonly WorkflowDataSnapshot Empty = new(
        new Dictionary<int, int>(),
        new Dictionary<int, CadOutputFwDiffAssignmentDecision>(),
        new Dictionary<int, int>(),
        new Dictionary<int, int>(),
        new Dictionary<int, IReadOnlyList<PadMatchLink>>(),
        new Dictionary<int, int>(),
        null);

    public bool TryGetCadOutputFwDiffIndex(int cadPadId, out int diffIndex)
    {
        return CadOutputFwDiffIndexByCadId.TryGetValue(cadPadId, out diffIndex);
    }

    public bool TryGetCadIcIndex(int cadPadId, out int icIndex)
    {
        return CadIcIndexByCadId.TryGetValue(cadPadId, out icIndex);
    }
}
