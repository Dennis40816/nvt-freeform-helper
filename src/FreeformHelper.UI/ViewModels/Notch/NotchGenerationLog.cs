// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Domain.Notch;
using NLog;

namespace FreeformHelper.UI.ViewModels;

internal static class NotchGenerationLog
{
    internal static void LogPhaseTimings(Logger logger, string operationLabel, NotchGenerationPhaseTimings phaseTimings)
    {
        logger.Info(
            CultureInfo.InvariantCulture,
            "{0} phase timings: profiles={1} ms, candidates={2} ms, merge={3} ms, legacy={4} ms, finalize={5} ms, total={6} ms.",
            operationLabel,
            phaseTimings.BuildProfilesElapsedMs,
            phaseTimings.BuildCanonicalCandidatesElapsedMs,
            phaseTimings.MergeCanonicalCandidatesElapsedMs,
            phaseTimings.BuildLegacyRowsElapsedMs,
            phaseTimings.FinalizeCanonicalExportsElapsedMs,
            phaseTimings.TotalElapsedMs);
        if (phaseTimings.CandidateBreakdown?.HasData == true)
        {
            var candidateBreakdown = phaseTimings.CandidateBreakdown;
            var compensationStages = candidateBreakdown.CompensationStageTimings;
            var stageCBreakdown = compensationStages.StageCBreakdown;
            logger.Info(
                CultureInfo.InvariantCulture,
                "{0} candidate breakdown: count={1}, buildCadCandidate={2} ms, compensation={3} ms, postCompute={4} ms, stageA={5} ms, stageB={6} ms, stageC={7} ms, stageD={8} ms.",
                operationLabel,
                candidateBreakdown.CandidateCount,
                candidateBreakdown.BuildV22CadCandidateElapsedMs,
                candidateBreakdown.CompensationComputeElapsedMs,
                candidateBreakdown.CandidatePostComputeElapsedMs,
                compensationStages.StageAElapsedMs,
                compensationStages.StageBElapsedMs,
                compensationStages.StageCElapsedMs,
                compensationStages.StageDElapsedMs);
            if (stageCBreakdown?.HasData == true)
            {
                logger.Info(
                    CultureInfo.InvariantCulture,
                    "{0} stageC breakdown: ownerLookup={1} ms, occupiedArea={2} ms, sourceCoverage={3} ms, reachability={4} ms, ruleDecision={5} ms, previewDebug={6} ms.",
                    operationLabel,
                    stageCBreakdown.OwnerAndBlockerLookupElapsedMs,
                    stageCBreakdown.ExactOccupiedAreaElapsedMs,
                    stageCBreakdown.SourceCoverageElapsedMs,
                    stageCBreakdown.ReachabilityElapsedMs,
                    stageCBreakdown.RuleDecisionElapsedMs,
                    stageCBreakdown.PreviewAndDebugElapsedMs);
            }
        }
    }
}
