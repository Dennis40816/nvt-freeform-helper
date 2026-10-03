using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchTableGenerator
{
    private sealed record CadAllocationResolutionSettings(
        NotchCompensationModel CompensationModel,
        bool EnableToRegular, bool EnableToFull,
        bool EnableToFullRuleEngine, bool EnableToFullRuleTrace,
        bool EnableBoundaryVirtualAreaCap,
        double BoundaryVirtualAreaCapRatio, double StrictOverlapRatio);

    private sealed class CadAllocationGenerationContext(
        IReadOnlyList<CadPad> cadPads,
        RegularGrid grid,
        IReadOnlyList<CadAllocationProfile> cadProfiles,
        IReadOnlyDictionary<int, IReadOnlyList<CadPad>> cadPoolByIc,
        IReadOnlyDictionary<int, NotchV22CompensationService.NotchV22BoundaryQueryContext> boundaryQueryContextsByIc,
        IReadOnlySet<int> boundaryRegularIndices,
        IReadOnlySet<int>? activeRegularPadIds,
        IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId,
        CadAllocationResolutionSettings resolutionSettings,
        NotchV22TargetAllocationAreaMode targetAllocationAreaMode,
        CadAllocationSparseResultRequest? selectedSparseResultRequest)
    {
        public readonly IReadOnlyList<CadPad> CadPads = cadPads;
        public readonly RegularGrid Grid = grid;
        public readonly IReadOnlyList<CadAllocationProfile> CadProfiles = cadProfiles;
        public readonly IReadOnlyDictionary<int, IReadOnlyList<CadPad>> CadPoolByIc = cadPoolByIc;
        public readonly IReadOnlyDictionary<int, NotchV22CompensationService.NotchV22BoundaryQueryContext> BoundaryQueryContextsByIc =
            boundaryQueryContextsByIc;
        public readonly IReadOnlySet<int> BoundaryRegularIndices = boundaryRegularIndices;
        public readonly IReadOnlySet<int>? ActiveRegularPadIds = activeRegularPadIds;
        public readonly IReadOnlyDictionary<int, int>? CadOutputFwDiffIndexByCadId = cadOutputFwDiffIndexByCadId;
        public readonly CadAllocationResolutionSettings ResolutionSettings = resolutionSettings;
        public readonly NotchV22TargetAllocationAreaMode TargetAllocationAreaMode = targetAllocationAreaMode;
        public readonly CadAllocationSparseResultRequest? SelectedSparseResultRequest = selectedSparseResultRequest;
    }

    private sealed record CanonicalCandidateBuildResult(
        Dictionary<(int IcIndex, int DiffIndex), List<V22CadCandidate>> CandidatesByDiff,
        long BuildCanonicalCandidatesElapsedMs,
        long MergeCanonicalCandidatesElapsedMs,
        NotchCandidatePhaseTimings CandidateBreakdown,
        CadAllocationSparseResult? SelectedSparseResult)
    {
        public static CanonicalCandidateBuildResult Empty { get; } =
            new(
                new Dictionary<(int IcIndex, int DiffIndex), List<V22CadCandidate>>(),
                0,
                0,
                NotchCandidatePhaseTimings.Empty,
                null);
    }

    private sealed class ResolvedCadAllocationBatch(
        CanonicalCandidateBuildResult canonicalCandidateBuild,
        long buildProfilesElapsedMs,
        CadAllocationResolutionSettings resolutionSettings) : CadAllocationResolvedBatch(
            canonicalCandidateBuild.SelectedSparseResult)
    {
        public readonly CanonicalCandidateBuildResult CanonicalCandidateBuild = canonicalCandidateBuild;
        public readonly long BuildProfilesElapsedMs = buildProfilesElapsedMs;
        public readonly CadAllocationResolutionSettings ResolutionSettings = resolutionSettings;
    }

    private sealed class CandidateTimingAccumulator
    {
        private long _candidateCount;
        private long _buildV22CadCandidateElapsedMs;
        private long _compensationComputeElapsedMs;
        private long _compensationStageAElapsedMs;
        private long _compensationStageBElapsedMs;
        private long _compensationStageCElapsedMs;
        private long _compensationStageDElapsedMs;
        private long _stageCOwnerAndBlockerLookupElapsedMs;
        private long _stageCExactOccupiedAreaElapsedMs;
        private long _stageCSourceCoverageElapsedMs;
        private long _stageCReachabilityElapsedMs;
        private long _stageCRuleDecisionElapsedMs;
        private long _stageCPreviewAndDebugElapsedMs;

        public void Add(
            long buildV22CadCandidateElapsedMs,
            long compensationComputeElapsedMs,
            NotchV22CompensationStageTimings compensationStageTimings)
        {
            var stageCBreakdown = compensationStageTimings.StageCBreakdown ?? NotchV22StageCSubphaseTimings.Empty;
            Interlocked.Increment(ref _candidateCount);
            Interlocked.Add(ref _buildV22CadCandidateElapsedMs, buildV22CadCandidateElapsedMs);
            Interlocked.Add(ref _compensationComputeElapsedMs, compensationComputeElapsedMs);
            Interlocked.Add(ref _compensationStageAElapsedMs, compensationStageTimings.StageAElapsedMs);
            Interlocked.Add(ref _compensationStageBElapsedMs, compensationStageTimings.StageBElapsedMs);
            Interlocked.Add(ref _compensationStageCElapsedMs, compensationStageTimings.StageCElapsedMs);
            Interlocked.Add(ref _compensationStageDElapsedMs, compensationStageTimings.StageDElapsedMs);
            Interlocked.Add(ref _stageCOwnerAndBlockerLookupElapsedMs, stageCBreakdown.OwnerAndBlockerLookupElapsedMs);
            Interlocked.Add(ref _stageCExactOccupiedAreaElapsedMs, stageCBreakdown.ExactOccupiedAreaElapsedMs);
            Interlocked.Add(ref _stageCSourceCoverageElapsedMs, stageCBreakdown.SourceCoverageElapsedMs);
            Interlocked.Add(ref _stageCReachabilityElapsedMs, stageCBreakdown.ReachabilityElapsedMs);
            Interlocked.Add(ref _stageCRuleDecisionElapsedMs, stageCBreakdown.RuleDecisionElapsedMs);
            Interlocked.Add(ref _stageCPreviewAndDebugElapsedMs, stageCBreakdown.PreviewAndDebugElapsedMs);
        }

        public NotchCandidatePhaseTimings Build()
        {
            return new NotchCandidatePhaseTimings(
                CandidateCount: (int)Volatile.Read(ref _candidateCount),
                BuildV22CadCandidateElapsedMs: Volatile.Read(ref _buildV22CadCandidateElapsedMs),
                CompensationComputeElapsedMs: Volatile.Read(ref _compensationComputeElapsedMs),
                CompensationStageTimings: new NotchV22CompensationStageTimings(
                    StageAElapsedMs: Volatile.Read(ref _compensationStageAElapsedMs),
                    StageBElapsedMs: Volatile.Read(ref _compensationStageBElapsedMs),
                    StageCElapsedMs: Volatile.Read(ref _compensationStageCElapsedMs),
                    StageDElapsedMs: Volatile.Read(ref _compensationStageDElapsedMs),
                    StageCBreakdown: new NotchV22StageCSubphaseTimings(
                        OwnerAndBlockerLookupElapsedMs: Volatile.Read(ref _stageCOwnerAndBlockerLookupElapsedMs),
                        ExactOccupiedAreaElapsedMs: Volatile.Read(ref _stageCExactOccupiedAreaElapsedMs),
                        SourceCoverageElapsedMs: Volatile.Read(ref _stageCSourceCoverageElapsedMs),
                        ReachabilityElapsedMs: Volatile.Read(ref _stageCReachabilityElapsedMs),
                        RuleDecisionElapsedMs: Volatile.Read(ref _stageCRuleDecisionElapsedMs),
                        PreviewAndDebugElapsedMs: Volatile.Read(ref _stageCPreviewAndDebugElapsedMs))));
        }
    }
}
