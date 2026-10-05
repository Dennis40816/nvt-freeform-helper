using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static partial class DxfRegularMaskAuditService
{
    private static SeedConstructionResult BuildSeedDecisions(AuditInvocationContext context)
    {
        var mode = context.ActiveRegularPadIds is null
            ? CadOutputFwDiffAssignmentMode.GeometryOnly
            : CadOutputFwDiffAssignmentMode.CsvConstrained;
        var duplicateDiffKeys = BuildDuplicateDiffKeySet(
            context.OrderedCadPads,
            context.CurrentAssignedDiffByCadId,
            context.CadIcIndexByCadId);
        var decisions = new Dictionary<int, CadOutputFwDiffAssignmentDecision>(context.OrderedCadPads.Count);

        for (var index = 0; index < context.OrderedCadPads.Count; index++)
        {
            var cadPad = context.OrderedCadPads[index];
            var candidateSet = BuildCandidateSet(
                cadPad.Id,
                context.CadToRegular,
                context.RegularPadById,
                context.CadIcIndexByCadId,
                context.ActiveRegularPadIds);
            var currentPrimaryDiff = ResolveCurrentPrimaryDiff(context.CurrentAssignedDiffByCadId, cadPad.Id);
            var decisionSource = context.ManualOverrideDiffByCadId is not null &&
                                 context.ManualOverrideDiffByCadId.ContainsKey(cadPad.Id)
                ? CadOutputFwDiffAssignmentDecisionSource.ManualOverride
                : CadOutputFwDiffAssignmentDecisionSource.Seed;
            var hasDuplicateConflict = currentPrimaryDiff.HasValue &&
                                       context.CadIcIndexByCadId.TryGetValue(cadPad.Id, out var cadIcIndex) &&
                                       duplicateDiffKeys.Contains((cadIcIndex, currentPrimaryDiff.Value));
            var reasonCode = ResolveReasonCode(
                mode,
                candidateSet.RawSeed,
                candidateSet.MaskedSeed,
                currentPrimaryDiff,
                hasDuplicateConflict,
                candidateSet.GeometryCandidates,
                candidateSet.CsvConfirmedCandidates);
            var repairSuggestionDiff = ResolveRepairSuggestionDiff(
                mode,
                currentPrimaryDiff,
                candidateSet.GeometryCandidates,
                candidateSet.CsvConfirmedCandidates);
            var confidence = mode == CadOutputFwDiffAssignmentMode.CsvConstrained
                ? ComputeConfidence(candidateSet.CsvConfirmedCandidates)
                : ComputeConfidence(candidateSet.GeometryCandidates);

            decisions[cadPad.Id] = new CadOutputFwDiffAssignmentDecision(
                CadPadId: cadPad.Id,
                OrderedCadIndex: index,
                Mode: mode,
                RawSeed: candidateSet.RawSeed,
                MaskedSeed: candidateSet.MaskedSeed,
                GeometryCandidates: candidateSet.GeometryCandidates,
                CsvConfirmedCandidates: candidateSet.CsvConfirmedCandidates,
                CurrentPrimaryDiffIndex: currentPrimaryDiff,
                PassiveCompensationDiffIndex: null,
                RepairSuggestionDiffIndex: repairSuggestionDiff,
                ReasonCode: reasonCode,
                DecisionSource: decisionSource,
                Confidence: confidence);
        }

        return new SeedConstructionResult(decisions);
    }

    // Keep caller-owned inputs live and pass the same invocation-owned decisions through both stages.
    private sealed record AuditInvocationContext(
        IReadOnlyList<CadPad> OrderedCadPads,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> CadToRegular,
        IReadOnlyDictionary<int, RegularPad> RegularPadById,
        IReadOnlyDictionary<int, int> CadIcIndexByCadId,
        IReadOnlySet<int>? ActiveRegularPadIds,
        IReadOnlyDictionary<int, int>? CurrentAssignedDiffByCadId,
        IReadOnlyDictionary<int, int>? ManualOverrideDiffByCadId);

    private readonly record struct SeedConstructionResult(Dictionary<int, CadOutputFwDiffAssignmentDecision> Decisions);

    private readonly record struct SegmentAnalysisResult(Dictionary<int, CadOutputFwDiffAssignmentDecision> Decisions);
}
