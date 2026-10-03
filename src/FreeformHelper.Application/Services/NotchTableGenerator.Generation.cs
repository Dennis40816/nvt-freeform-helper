using System.Diagnostics;
using System.Runtime.ExceptionServices;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using CadAllocationProjectionRequest = (
    bool ExportsV22,
    bool ExportsV21,
    int ThresholdQ7,
    double EffectiveV22ThresholdPercent,
    int NullValue,
    bool ApplyTargetCoverageGuard,
    int TargetCoverageCapPercent);
namespace FreeformHelper.Application.Services;

public sealed partial class NotchTableGenerator
{
    private const int CadAllocationGenerationPhaseCount = 4;
    private const string GenerationPhaseBuildProfiles = "Build allocation profiles";
    private const string GenerationPhaseBuildCanonicalCandidates = "Build canonical candidates";
    private const string GenerationPhaseMergeCanonicalCandidates = "Merge candidates by diff";
    private const string GenerationPhaseFinalizeRows = "Finalize canonical exports";

    private static NotchTable GenerateLegacyRegularAnchor(
        CadPadSet cad,
        RegularGrid grid,
        NotchSettings settings,
        IProgress<NotchGenerationProgress>? progress)
    {
        var legacyRowsStopwatch = Stopwatch.StartNew();
        var request = CaptureLegacyNotchGenerationRequest(settings);
        var rows = new List<NotchTableRow>();
        var cadById = cad.Pads.ToDictionary(p => p.Id);
        var cadMaxAllocation = BuildCadMaxAllocation(cadById, grid);

        var candidates = grid.Pads
            .Where(reg => reg.Freeform != FreeformType.None)
            .ToList();
        var totalCount = candidates.Count;
        ReportGenerationProgress(progress, 0, totalCount, rows.Count, null, null);

        for (var index = 0; index < candidates.Count; index++)
        {
            var reg = candidates[index];

            cadById.TryGetValue(reg.MatchedCadPadId ?? -1, out var matchedCad);
            if (matchedCad is null)
            {
                if (ShouldReportGenerationProgress(index + 1, totalCount))
                {
                    ReportGenerationProgress(progress, index + 1, totalCount, rows.Count, null, reg.RegularPadId);
                }

                continue;
            }

            var maxRatio = cadMaxAllocation.TryGetValue(matchedCad.Id, out var ratio) ? ratio : 0.0;

            foreach (var version in request.EnabledVersions)
            {
                if (!PassesThresholdForVersion(version, maxRatio, request))
                {
                    continue;
                }

                if (!CanBuildLegacyRow(version, reg, matchedCad))
                {
                    continue;
                }

                rows.Add(BuildLegacyRow(version, reg, matchedCad, grid, request));
            }

            if (ShouldReportGenerationProgress(index + 1, totalCount))
            {
                ReportGenerationProgress(progress, index + 1, totalCount, rows.Count, matchedCad.Id, reg.RegularPadId);
            }
        }

        ReportGenerationProgress(progress, totalCount, totalCount, rows.Count, null, null);
        legacyRowsStopwatch.Stop();
        return new NotchTable(
            rows,
            generationPhaseTimings: new NotchGenerationPhaseTimings(
                BuildProfilesElapsedMs: 0,
                BuildCanonicalCandidatesElapsedMs: 0,
                MergeCanonicalCandidatesElapsedMs: 0,
                BuildLegacyRowsElapsedMs: legacyRowsStopwatch.ElapsedMilliseconds,
                FinalizeCanonicalExportsElapsedMs: 0));
    }

    private static LegacyNotchGenerationRequest CaptureLegacyNotchGenerationRequest(NotchSettings settings) =>
        new(
            settings.EnabledVersions
                .OrderBy(static version => (int)version)
                .ToArray(),
            settings.LenScale,
            settings.NullValue,
            settings.ThresholdQ7,
            ResolveEffectiveV22ThresholdPercent(settings));

    private static bool CanBuildLegacyRow(
        NotchAlgorithmVersion version,
        RegularPad regularPad,
        CadPad? cadPad) =>
        version switch
        {
            NotchAlgorithmVersion.V21 => V21NotchAlgorithm.CanHandle(regularPad),
            NotchAlgorithmVersion.V22 => V22LegacyRowStrategy.CanHandle(regularPad, cadPad),
            _ => false,
        };

    private static NotchTableRow BuildLegacyRow(
        NotchAlgorithmVersion version,
        RegularPad regularPad,
        CadPad? cadPad,
        RegularGrid grid,
        LegacyNotchGenerationRequest request) =>
        version switch
        {
            NotchAlgorithmVersion.V21 => V21NotchAlgorithm.Build(regularPad, cadPad, grid, request),
            NotchAlgorithmVersion.V22 => V22LegacyRowStrategy.Build(regularPad, cadPad, grid, request),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "Unsupported Notch version."),
        };

    /// <summary>
    /// Resolves the compact output-neutral CadAllocation batch once and projects
    /// the final request captured at the candidate-phase boundary.
    /// </summary>
    public (CadAllocationResolvedBatch Batch, NotchTable Table) GenerateCadAllocationResolvedBatch(
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        IProgress<NotchGenerationProgress>? progress = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId = null)
    {
        ValidateCadAllocationBatchRequest(cad, grid, settings);

        return GenerateCadAllocationResolvedBatchCore(
            cad,
            grid,
            settings,
            progress,
            activeRegularPadIds,
            cadOutputFwDiffIndexByCadId);
    }

    /// <summary>
    /// Resolves the compact output-neutral CadAllocation batch without projecting
    /// any final rows.
    /// </summary>
    public CadAllocationResolvedBatch ResolveCadAllocationBatch(
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        IProgress<NotchGenerationProgress>? progress = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId = null,
        CadAllocationSparseResultRequest? selectedSparseResultRequest = null)
    {
        ValidateCadAllocationBatchRequest(cad, grid, settings);
        return ResolveCadAllocationBatchCore(
            cad,
            grid,
            settings,
            progress,
            activeRegularPadIds,
            cadOutputFwDiffIndexByCadId,
            selectedSparseResultRequest,
            captureProjectionRequest: false).Batch;
    }

    /// <summary>
    /// Projects an existing output-neutral CadAllocation batch using only the
    /// current final-output request.
    /// </summary>
    public static NotchTable ProjectCadAllocationResolvedBatch(
        CadAllocationResolvedBatch batch,
        ProjectSettings settings,
        IProgress<NotchGenerationProgress>? progress = null,
        bool includeResolutionTimings = false)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateOrThrow();
        if (settings.Notch.ComputationMode != NotchComputationMode.CadAllocation)
        {
            throw new ArgumentException(
                "CadAllocation batch projection requires CadAllocation computation mode.",
                nameof(settings));
        }

        if (batch is not ResolvedCadAllocationBatch resolvedBatch)
        {
            throw new ArgumentException("Unsupported CadAllocation batch implementation.", nameof(batch));
        }

        if (!HasMatchingCadAllocationResolutionSettings(resolvedBatch, settings))
        {
            throw new ArgumentException(
                "CadAllocation batch computation inputs do not match the requested settings.",
                nameof(settings));
        }

        var projectionRequest = CaptureCadAllocationProjectionRequest(
            settings.Notch,
            resolvedBatch.ResolutionSettings.CompensationModel);
        return ProjectCadAllocationResolvedBatchCore(
            resolvedBatch,
            projectionRequest,
            progress,
            includeResolutionTimings);
    }

    /// <summary>
    /// Returns whether a resolved batch was built from the supplied computation inputs.
    /// Final-output settings are intentionally excluded from this comparison.
    /// </summary>
    public static bool IsCadAllocationResolvedBatchCompatible(
        CadAllocationResolvedBatch batch,
        ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Notch.ComputationMode == NotchComputationMode.CadAllocation &&
               batch is ResolvedCadAllocationBatch resolvedBatch &&
               HasMatchingCadAllocationResolutionSettings(resolvedBatch, settings);
    }

    /// <summary>
    /// Returns whether two requests consume the same CadAllocation resolution inputs.
    /// Final-output settings are intentionally excluded.
    /// </summary>
    public static bool HaveEquivalentCadAllocationResolutionSettings(
        ProjectSettings first,
        ProjectSettings second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return first.Notch.ComputationMode == NotchComputationMode.CadAllocation &&
               second.Notch.ComputationMode == NotchComputationMode.CadAllocation &&
               CaptureCadAllocationResolutionSettings(first) ==
               CaptureCadAllocationResolutionSettings(second);
    }

    private static bool HasMatchingCadAllocationResolutionSettings(
        ResolvedCadAllocationBatch batch,
        ProjectSettings settings)
    {
        return batch.ResolutionSettings == CaptureCadAllocationResolutionSettings(settings);
    }

    private (CadAllocationResolvedBatch Batch, NotchTable Table) GenerateCadAllocationResolvedBatchCore(
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings,
        IProgress<NotchGenerationProgress>? progress,
        IReadOnlySet<int>? activeRegularPadIds,
        IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId)
    {
        var resolution = ResolveCadAllocationBatchCore(
            cad,
            grid,
            settings,
            progress,
            activeRegularPadIds,
            cadOutputFwDiffIndexByCadId,
            selectedSparseResultRequest: null,
            captureProjectionRequest: true);
        return (
            resolution.Batch,
            ProjectCadAllocationResolvedBatchCore(
                resolution.Batch,
                resolution.ProjectionRequest!.Value,
                progress,
                includeResolutionTimings: true));
    }

    private (ResolvedCadAllocationBatch Batch, CadAllocationProjectionRequest? ProjectionRequest)
        ResolveCadAllocationBatchCore(
            CadPadSet cad,
            RegularGrid grid,
            ProjectSettings settings,
            IProgress<NotchGenerationProgress>? progress,
            IReadOnlySet<int>? activeRegularPadIds,
            IReadOnlyDictionary<int, int>? cadOutputFwDiffIndexByCadId,
            CadAllocationSparseResultRequest? selectedSparseResultRequest,
            bool captureProjectionRequest)
    {
        var cadPads = cad.Pads;
        var buildProfilesStopwatch = Stopwatch.StartNew();
        var cadProfiles = BuildCadAllocationProfiles(
            cadPads,
            grid,
            progress,
            phase: GenerationPhaseBuildProfiles,
            phaseStep: 1,
            phaseStepCount: CadAllocationGenerationPhaseCount);
        buildProfilesStopwatch.Stop();
        var activeRegularPadIdsSnapshot = activeRegularPadIds?.ToHashSet();
        var cadOutputFwDiffIndexSnapshot = cadOutputFwDiffIndexByCadId?.ToDictionary();
        var cadPoolByIc = BuildCadPoolByIc(cadProfiles);
        var strictOverlapRatio = NotchV22CompensationService.NormalizeStrictOverlapRatio(
            settings.Notch.MultiOwnerStrictOverlapPercent / 100.0);
        var boundaryRegularIndices = NotchV22CompensationService.BuildBoundaryRegularIndices(
            grid,
            activeRegularPadIdsSnapshot);
        var boundaryQueryContextsByIc = BuildBoundaryQueryContextsByIc(cadPoolByIc, strictOverlapRatio);
        ReportGenerationProgress(
            progress,
            0,
            cadProfiles.Count,
            0,
            null,
            null,
            GenerationPhaseBuildCanonicalCandidates,
            phaseStep: 2,
            phaseStepCount: CadAllocationGenerationPhaseCount);
        var resolutionSettings = CaptureCadAllocationResolutionSettings(settings) with { StrictOverlapRatio = strictOverlapRatio };
        var generationContext = new CadAllocationGenerationContext(
            cadPads,
            grid,
            cadProfiles,
            cadPoolByIc,
            boundaryQueryContextsByIc,
            boundaryRegularIndices,
            activeRegularPadIdsSnapshot,
            cadOutputFwDiffIndexSnapshot,
            resolutionSettings,
            NotchV22TargetAllocationPolicy.ResolveAreaMode(resolutionSettings.CompensationModel),
            selectedSparseResultRequest);
        CadAllocationProjectionRequest? projectionRequest = captureProjectionRequest
            ? CaptureCadAllocationProjectionRequest(settings.Notch, resolutionSettings.CompensationModel)
            : null;
        var canonicalCandidateBuild = BuildCanonicalCandidatesByDiff(
            generationContext,
            progress,
            candidatePhaseStep: 2,
            mergePhaseStep: 3,
            phaseStepCount: CadAllocationGenerationPhaseCount);
        var batch = new ResolvedCadAllocationBatch(
            canonicalCandidateBuild,
            buildProfilesStopwatch.ElapsedMilliseconds,
            resolutionSettings);
        return (batch, projectionRequest);
    }

    private static NotchTable ProjectCadAllocationResolvedBatchCore(
        ResolvedCadAllocationBatch batch,
        CadAllocationProjectionRequest projectionRequest,
        IProgress<NotchGenerationProgress>? progress,
        bool includeResolutionTimings)
    {
        var canonicalCandidateBuild = batch.CanonicalCandidateBuild;
        var rows = new List<NotchTableRow>();
        ReportGenerationProgress(
            progress,
            0,
            1,
            rows.Count,
            null,
            null,
            GenerationPhaseFinalizeRows,
            phaseStep: 4,
            phaseStepCount: CadAllocationGenerationPhaseCount);
        var finalizeStopwatch = Stopwatch.StartNew();
        var admittedCandidatesByDiff = BuildCanonicalProjectionCandidateView(
            canonicalCandidateBuild.CandidatesByDiff,
            projectionRequest);
        var canonicalRows = BuildV22DiffCentricRows(
                admittedCandidatesByDiff,
                projectionRequest.NullValue,
                projectionRequest.ApplyTargetCoverageGuard,
                projectionRequest.TargetCoverageCapPercent)
            .ToList();
        var toFullCoverageAudit = BuildToFullCoverageAudit(
            admittedCandidatesByDiff,
            canonicalRows,
            projectionRequest.NullValue);
        AppendCanonicalExports(
            canonicalRows,
            rows,
            projectionRequest.ExportsV22,
            projectionRequest.ExportsV21,
            projectionRequest.NullValue);
        finalizeStopwatch.Stop();

        ReportGenerationProgress(
            progress,
            1,
            1,
            rows.Count,
            null,
            null,
            GenerationPhaseFinalizeRows,
            phaseStep: 4,
            phaseStepCount: CadAllocationGenerationPhaseCount);
        return new NotchTable(
            rows,
            toFullCoverageAudit,
            new NotchGenerationPhaseTimings(
                BuildProfilesElapsedMs: includeResolutionTimings ? batch.BuildProfilesElapsedMs : 0,
                BuildCanonicalCandidatesElapsedMs:
                    includeResolutionTimings ? canonicalCandidateBuild.BuildCanonicalCandidatesElapsedMs : 0,
                MergeCanonicalCandidatesElapsedMs:
                    includeResolutionTimings ? canonicalCandidateBuild.MergeCanonicalCandidatesElapsedMs : 0,
                BuildLegacyRowsElapsedMs: 0,
                FinalizeCanonicalExportsElapsedMs: finalizeStopwatch.ElapsedMilliseconds,
                CandidateBreakdown:
                    includeResolutionTimings ? canonicalCandidateBuild.CandidateBreakdown : NotchCandidatePhaseTimings.Empty));
    }

    private static CadAllocationProjectionRequest CaptureCadAllocationProjectionRequest(
            NotchSettings settings,
            NotchCompensationModel compensationModel)
    {
        var enabledVersions = settings.EnabledVersions;
        return (
            ExportsV22: enabledVersions.Contains(NotchAlgorithmVersion.V22),
            ExportsV21: enabledVersions.Contains(NotchAlgorithmVersion.V21),
            ThresholdQ7: settings.ThresholdQ7,
            EffectiveV22ThresholdPercent: ResolveEffectiveV22ThresholdPercent(settings),
            NullValue: settings.NullValue,
            ApplyTargetCoverageGuard: settings.EnableTargetCoverageGuard &&
                                      compensationModel == NotchCompensationModel.CurrentGain,
            TargetCoverageCapPercent: settings.TargetCoverageCapPercent);
    }

    private static void ValidateCadAllocationBatchRequest(
        CadPadSet cad,
        RegularGrid grid,
        ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settings);
        settings.ValidateOrThrow();
        if (settings.Notch.ComputationMode != NotchComputationMode.CadAllocation)
        {
            throw new ArgumentException(
                "CadAllocation batch generation requires CadAllocation computation mode.",
                nameof(settings));
        }
    }

    /// <summary>
    /// Computes a deterministic compact cache fingerprint for settings consumed while resolving a
    /// CadAllocation batch. Final projection and formatter settings are intentionally excluded.
    /// Callers must also use <see cref="IsCadAllocationResolvedBatchCompatible"/> before reuse because
    /// the 32-bit fingerprint is not a collision-free identity.
    /// </summary>
    public static int ComputeCadAllocationResolvedBatchSettingsFingerprint(ProjectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ComputeCadAllocationResolutionSettingsFingerprint(
            CaptureCadAllocationResolutionSettings(settings));
    }

    private static CadAllocationResolutionSettings CaptureCadAllocationResolutionSettings(ProjectSettings settings)
    {
        var notch = settings.Notch;
        var (enableToRegular, enableToFull) = notch.ResolveStep3Switches();
        return new(
            CompensationModel: notch.CompensationModel,
            EnableToRegular: enableToRegular,
            EnableToFull: enableToFull,
            EnableToFullRuleEngine: notch.EnableToFullRuleEngine,
            EnableToFullRuleTrace: notch.EnableToFullRuleTrace,
            EnableBoundaryVirtualAreaCap: notch.EnableBoundaryVirtualAreaCap,
            BoundaryVirtualAreaCapRatio: NotchV22CompensationService
                .NormalizeBoundaryVirtualAreaCapRatio(notch.BoundaryVirtualAreaCapRatio),
            StrictOverlapRatio: NotchV22CompensationService.NormalizeStrictOverlapRatio(
                notch.MultiOwnerStrictOverlapPercent / 100.0));
    }

    private static int ComputeCadAllocationResolutionSettingsFingerprint(
        CadAllocationResolutionSettings settings)
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + (int)settings.CompensationModel;
            hash = (hash * 31) + (settings.EnableToRegular ? 1 : 0);
            hash = (hash * 31) + (settings.EnableToFull ? 1 : 0);
            hash = (hash * 31) + (settings.EnableToFullRuleEngine ? 1 : 0);
            hash = (hash * 31) + (settings.EnableToFullRuleTrace ? 1 : 0);
            hash = (hash * 31) + (settings.EnableBoundaryVirtualAreaCap ? 1 : 0);
            hash = (hash * 31) + settings.BoundaryVirtualAreaCapRatio.GetHashCode();
            return (hash * 31) + settings.StrictOverlapRatio.GetHashCode();
        }
    }

    private static IReadOnlyDictionary<(int IcIndex, int DiffIndex), List<V22CadCandidate>> BuildCanonicalProjectionCandidateView(
        IReadOnlyDictionary<(int IcIndex, int DiffIndex), List<V22CadCandidate>> candidatesByDiff,
        CadAllocationProjectionRequest request)
    {
        bool PassesThreshold(double maxRatio) => request.ExportsV22
            ? PassesV22ThresholdPercent(Math.Max(0.0, maxRatio), request.EffectiveV22ThresholdPercent)
            : PassesV21ThresholdQ7(Math.Max(0.0, maxRatio), request.ThresholdQ7);

        if (!request.ExportsV22 && !request.ExportsV21)
        {
            return CanonicalCandidateBuildResult.Empty.CandidatesByDiff;
        }

        if (PassesThreshold(0.0))
        {
            foreach (var candidate in candidatesByDiff.Values.SelectMany(static candidates => candidates))
            {
                if (candidate.ProjectionError is not null)
                {
                    throw new InvalidOperationException(candidate.ProjectionError);
                }
            }

            return candidatesByDiff;
        }

        var admitted = new Dictionary<(int IcIndex, int DiffIndex), List<V22CadCandidate>>();
        foreach (var (key, candidates) in candidatesByDiff)
        {
            var admittedCandidates = new List<V22CadCandidate>(candidates.Count);
            foreach (var candidate in candidates)
            {
                if (!PassesThreshold(candidate.MaxRatio))
                {
                    continue;
                }

                if (candidate.ProjectionError is not null)
                {
                    throw new InvalidOperationException(candidate.ProjectionError);
                }

                admittedCandidates.Add(candidate);
            }

            if (admittedCandidates.Count > 0)
            {
                admitted[key] = admittedCandidates;
            }
        }

        return admitted;
    }

    private static void AppendCanonicalExports(
        IReadOnlyList<NotchTableRow> canonicalRows,
        List<NotchTableRow> rows,
        bool exportsV22,
        bool exportsV21,
        int nullValue)
    {
        if (exportsV22)
        {
            rows.AddRange(canonicalRows);
        }

        if (exportsV21)
        {
            rows.AddRange(ProjectV22RowsToV21Rows(canonicalRows, nullValue));
        }
    }

    private static CanonicalCandidateBuildResult BuildCanonicalCandidatesByDiff(
        CadAllocationGenerationContext context,
        IProgress<NotchGenerationProgress>? progress,
        int candidatePhaseStep,
        int mergePhaseStep,
        int phaseStepCount)
    {
        var cadProfiles = context.CadProfiles;
        var totalCount = cadProfiles.Count;
        if (cadProfiles.Count == 0)
        {
            ReportGenerationProgress(
                progress,
                1,
                1,
                0,
                null,
                null,
                GenerationPhaseBuildCanonicalCandidates,
                phaseStep: candidatePhaseStep,
                phaseStepCount: phaseStepCount);
            ReportGenerationProgress(
                progress,
                1,
                1,
                0,
                null,
                null,
                GenerationPhaseMergeCanonicalCandidates,
                phaseStep: mergePhaseStep,
                phaseStepCount: phaseStepCount);
            return CanonicalCandidateBuildResult.Empty;
        }

        var candidatesByProfile = new List<KeyValuePair<(int IcIndex, int DiffIndex), V22CadCandidate>>?[cadProfiles.Count];
        var selectedResolvedByProfile = new NotchV22ResolvedResult?[cadProfiles.Count];
        var processedInParallel = 0;
        var candidateTimingAccumulator = new CandidateTimingAccumulator();
        var candidateStopwatch = Stopwatch.StartNew();
        try
        {
            Parallel.For(0, cadProfiles.Count, index =>
            {
                var profile = cadProfiles[index];
                candidatesByProfile[index] = BuildCanonicalCandidatesForProfile(
                    profile,
                    context,
                    candidateTimingAccumulator,
                    out selectedResolvedByProfile[index]);

                var processed = Interlocked.Increment(ref processedInParallel);
                if (ShouldReportGenerationProgress(processed, totalCount))
                {
                    ReportGenerationProgress(
                        progress,
                        processed,
                        totalCount,
                        0,
                        profile.CadPad.Id,
                        null,
                        GenerationPhaseBuildCanonicalCandidates,
                        phaseStep: candidatePhaseStep,
                        phaseStepCount: phaseStepCount);
                }
            });
        }
        catch (AggregateException ex) when (ex.InnerExceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
            throw;
        }
        candidateStopwatch.Stop();

        var candidatesByDiff = new Dictionary<(int IcIndex, int DiffIndex), List<V22CadCandidate>>();
        var mergeStopwatch = Stopwatch.StartNew();
        ReportGenerationProgress(
            progress,
            0,
            candidatesByProfile.Length,
            0,
            null,
            null,
            GenerationPhaseMergeCanonicalCandidates,
            phaseStep: mergePhaseStep,
            phaseStepCount: phaseStepCount);
        for (var index = 0; index < candidatesByProfile.Length; index++)
        {
            var candidates = candidatesByProfile[index];
            if (candidates is null || candidates.Count == 0)
            {
                if (ShouldReportGenerationProgress(index + 1, candidatesByProfile.Length))
                {
                    ReportGenerationProgress(
                        progress,
                        index + 1,
                        candidatesByProfile.Length,
                        0,
                        cadProfiles[index].CadPad.Id,
                        null,
                        GenerationPhaseMergeCanonicalCandidates,
                        phaseStep: mergePhaseStep,
                        phaseStepCount: phaseStepCount);
                }

                continue;
            }

            foreach (var candidate in candidates)
            {
                if (!candidatesByDiff.TryGetValue(candidate.Key, out var sameDiffCandidates))
                {
                    sameDiffCandidates = new List<V22CadCandidate>();
                    candidatesByDiff[candidate.Key] = sameDiffCandidates;
                }

                sameDiffCandidates.Add(candidate.Value);
            }

            if (ShouldReportGenerationProgress(index + 1, candidatesByProfile.Length))
            {
                ReportGenerationProgress(
                    progress,
                    index + 1,
                    candidatesByProfile.Length,
                    candidatesByDiff.Count,
                    cadProfiles[index].CadPad.Id,
                    null,
                    GenerationPhaseMergeCanonicalCandidates,
                    phaseStep: mergePhaseStep,
                    phaseStepCount: phaseStepCount);
            }
        }

        var selectedSparseResult = context.SelectedSparseResultRequest is { } selectedRequest &&
                                   selectedResolvedByProfile.FirstOrDefault(
                                       static resolved => resolved is not null) is { } selectedResolved
            ? new CadAllocationSparseResult(
                selectedRequest.CadPadId,
                selectedRequest.AnchorIcIndex,
                selectedRequest.AnchorDiffIndex,
                selectedResolved)
            : null;
        ReportGenerationProgress(
            progress,
            candidatesByProfile.Length,
            candidatesByProfile.Length,
            candidatesByDiff.Count,
            null,
            null,
            GenerationPhaseMergeCanonicalCandidates,
            phaseStep: mergePhaseStep,
            phaseStepCount: phaseStepCount);
        mergeStopwatch.Stop();
        return new CanonicalCandidateBuildResult(
            candidatesByDiff,
            candidateStopwatch.ElapsedMilliseconds,
            mergeStopwatch.ElapsedMilliseconds,
            candidateTimingAccumulator.Build(),
            selectedSparseResult);
    }

    private static List<KeyValuePair<(int IcIndex, int DiffIndex), V22CadCandidate>> BuildCanonicalCandidatesForProfile(
        CadAllocationProfile profile,
        CadAllocationGenerationContext context,
        CandidateTimingAccumulator timingAccumulator,
        out NotchV22ResolvedResult? selectedResolvedResult)
    {
        selectedResolvedResult = null;
        if (!profile.HasAllocations ||
            profile.Anchor is null)
        {
            return new List<KeyValuePair<(int IcIndex, int DiffIndex), V22CadCandidate>>();
        }

        var candidates = new List<KeyValuePair<(int IcIndex, int DiffIndex), V22CadCandidate>>(profile.IcIndices.Count);
        var cadPad = profile.CadPad;
        var allocations = profile.Allocations;
        foreach (var icIndex in profile.IcIndices)
        {
            var anchorForIc = SelectCadAllocationAnchor(cadPad.Id, allocations, icIndex);
            if (anchorForIc is null)
            {
                continue;
            }

            var sourceDiffIndex = context.CadOutputFwDiffIndexByCadId is not null &&
                                  context.CadOutputFwDiffIndexByCadId.TryGetValue(cadPad.Id, out var cadOutputFwDiffIndex)
                ? cadOutputFwDiffIndex
                : anchorForIc.DiffIndex;
            var candidate = BuildV22CadCandidate(
                anchorForIc,
                sourceDiffIndex,
                profile,
                context,
                timingAccumulator,
                out var candidateSelectedResolvedResult);
            selectedResolvedResult ??= candidateSelectedResolvedResult;
            if (candidate.ProjectionError is null &&
                anchorForIc.Freeform == FreeformType.None &&
                !candidate.HasToFullInfluence)
            {
                continue;
            }

            candidates.Add(new KeyValuePair<(int IcIndex, int DiffIndex), V22CadCandidate>(
                (candidate.IcIndex, candidate.DiffIndex),
                candidate));
        }

        return candidates;
    }

    private static Dictionary<int, NotchV22CompensationService.NotchV22BoundaryQueryContext> BuildBoundaryQueryContextsByIc(
        Dictionary<int, IReadOnlyList<CadPad>> cadPoolByIc,
        double strictOverlapRatio)
    {
        if (cadPoolByIc.Count == 0)
        {
            return new Dictionary<int, NotchV22CompensationService.NotchV22BoundaryQueryContext>();
        }

        var contexts = new Dictionary<int, NotchV22CompensationService.NotchV22BoundaryQueryContext>(cadPoolByIc.Count);
        foreach (var (icIndex, sameIcCadPads) in cadPoolByIc)
        {
            contexts[icIndex] = NotchV22CompensationService.CreateBoundaryQueryContext(
                sameIcCadPads,
                strictOverlapRatio);
        }

        return contexts;
    }

    private static bool ShouldReportGenerationProgress(int processed, int total)
    {
        if (total <= 0)
        {
            return true;
        }

        if (processed <= 1 || processed >= total)
        {
            return true;
        }

        if (total <= 200)
        {
            return true;
        }

        var step = Math.Max(1, total / 100);
        return processed % step == 0;
    }

    private static void ReportGenerationProgress(
        IProgress<NotchGenerationProgress>? progress,
        int processed,
        int total,
        int generatedRows,
        int? cadPadId,
        int? regularPadId,
        string? phase = null,
        int phaseStep = 0,
        int phaseStepCount = 0)
    {
        progress?.Report(new NotchGenerationProgress(
            Math.Max(0, processed),
            Math.Max(0, total),
            Math.Max(0, generatedRows),
            cadPadId,
            regularPadId,
            phase,
            Math.Max(0, phaseStep),
            Math.Max(0, phaseStepCount)));
    }

}
