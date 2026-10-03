using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Analyzes DXF pads against a regular grid to derive a one-to-one mapping suggestion
/// and a set of issues that require manual verification.
/// </summary>
public sealed partial class DxfRegularMappingAnalyzer
{
    public static DxfRegularMappingResult Analyze(
        CadPadSet cad,
        RegularGrid grid,
        IndexMappingSettings settings,
        IReadOnlyDictionary<int, int>? manualOverrides = null,
        IReadOnlyDictionary<int, int>? expectedDiffIndexByCadId = null,
        Action<double>? reportProgress = null)
    {
        ReportProgress(reportProgress, 0.0);
        settings ??= new IndexMappingSettings();
        settings.ValidateOrThrow();

        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);

        var cadPads = cad.Pads;
        var regPads = grid.Pads;
        var cadCount = cadPads.Count;
        var regCount = regPads.Count;

        var padding = Math.Max(0, settings.CandidatePaddingCells);
        var candidateNumber = Math.Max(1, settings.GetEffectiveCandidateNumber());
        var diagnosticTopK = Math.Max(1, settings.DiagnosticTopK);
        var candidateRetainCount = Math.Max(candidateNumber, diagnosticTopK);

        var estimatedCandidatesPerCad = Math.Max(1, (2 * padding + 1) * (2 * padding + 1));
        var pairs = new List<CandidatePair>(cadCount * Math.Min(estimatedCandidatesPerCad, candidateRetainCount));
        var cadTopCandidates = new List<DxfRegularMappingCandidate>[cadCount];

        var cadBest = new double[cadCount];
        var cadSecond = new double[cadCount];
        var cadHasOverlapEvidence = new bool[cadCount];
        var regularHasOverlapEvidence = new bool[regCount];

        for (var c = 0; c < cadCount; c++)
        {
            var cp = cadPads[c];
            var (rowMin, rowMax, colMin, colMax) = RegularGridCandidateQuery.GetCandidateRange(grid, cp.Bounds, padding);

            var best = 0.0;
            var second = 0.0;
            var retainedCandidates = new List<CandidatePair>(candidateRetainCount);

            for (var r = rowMin; r <= rowMax; r++)
            {
                var rowBase = r * grid.Cols;
                for (var col = colMin; col <= colMax; col++)
                {
                    var regIndex = rowBase + col;
                    var reg = regPads[regIndex];
                    var score = ScoreBuilder.Compute(cp, reg, settings);
                    if (score <= 0)
                    {
                        continue;
                    }

                    cadHasOverlapEvidence[c] = true;
                    regularHasOverlapEvidence[regIndex] = true;

                    CandidateDiagnosticsBuilder.UpdateTopCandidates(retainedCandidates, c, regIndex, score, candidateRetainCount);

                    if (score > best)
                    {
                        second = best;
                        best = score;
                    }
                    else if (score > second)
                    {
                        second = score;
                    }
                }
            }

            cadBest[c] = best;
            cadSecond[c] = second;
            cadTopCandidates[c] = CandidateDiagnosticsBuilder.BuildCandidateDiagnostics(
                retainedCandidates.Take(Math.Min(diagnosticTopK, retainedCandidates.Count)).ToList(),
                regPads);

            foreach (var retained in retainedCandidates.Take(Math.Min(candidateNumber, retainedCandidates.Count)))
            {
                pairs.Add(retained);
            }

            MaybeReportProgress(
                reportProgress,
                0.05 + (0.55 * (c + 1) / Math.Max(1, cadCount)),
                c + 1,
                cadCount,
                interval: 4);
        }

        ReportProgress(reportProgress, 0.60);

        var cadToReg = Enumerable.Repeat(-1, cadCount).ToArray();
        var regToCad = Enumerable.Repeat(-1, regCount).ToArray();
        var cadAssignedScore = new double[cadCount];

        int ResolveCadSequenceIndex(int cadIndex)
        {
            if (expectedDiffIndexByCadId is not null &&
                expectedDiffIndexByCadId.TryGetValue(cadPads[cadIndex].Id, out var mapped))
            {
                return mapped;
            }

            return cadIndex;
        }

        ManualOverrideApplier.Apply(
            manualOverrides,
            cadPads,
            regPads,
            settings,
            cadToReg,
            regToCad,
            cadAssignedScore);

        pairs.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        var pairCount = pairs.Count;
        for (var i = 0; i < pairCount; i++)
        {
            var p = pairs[i];
            if (cadToReg[p.CadIndex] >= 0 || regToCad[p.RegularPadIndex] >= 0)
            {
                MaybeReportProgress(
                    reportProgress,
                    0.60 + (0.20 * (i + 1) / Math.Max(1, pairCount)),
                    i + 1,
                    pairCount,
                    interval: 2048);
                continue;
            }

            cadToReg[p.CadIndex] = p.RegularPadIndex;
            regToCad[p.RegularPadIndex] = p.CadIndex;
            cadAssignedScore[p.CadIndex] = p.Score;

            MaybeReportProgress(
                reportProgress,
                0.60 + (0.20 * (i + 1) / Math.Max(1, pairCount)),
                i + 1,
                pairCount,
                interval: 2048);
        }

        if (pairCount == 0)
        {
            ReportProgress(reportProgress, 0.80);
        }

        var mappedPairs = new List<DxfRegularMappingPair>(Math.Min(cadCount, regCount));
        for (var c = 0; c < cadCount; c++)
        {
            var regIndex = cadToReg[c];
            if (regIndex < 0)
            {
                continue;
            }

            var reg = regPads[regIndex];
            var cadSequenceIndex = ResolveCadSequenceIndex(c);
            mappedPairs.Add(new DxfRegularMappingPair(
                DxfIndex: cadSequenceIndex,
                CadPadId: cadPads[c].Id,
                RegularPadIndex: reg.RegularPadId,
                IcIndex: reg.IcIndex,
                DiffIndex: reg.DiffIndex,
                Score: cadAssignedScore[c]));
        }

        // --- Build report ---
        var mappedCount = mappedPairs.Count;
        var unmappedCadCount = cadCount - mappedCount;
        var unmappedRegularCount = regToCad.Count(x => x < 0);

        var lowConfidenceCount = 0;
        var ambiguousCount = 0;
        var duplicateDiffCount = 0;
        var issues = new List<DxfRegularMappingIssue>(Math.Min(settings.MaxReportIssues, cadCount + regCount + 1));

        if (cadCount != regCount)
        {
            issues.Add(new DxfRegularMappingIssue(
                DxfRegularMappingIssueKind.CountMismatch,
                $"visible DXF pads={cadCount}, regular pads={regCount}."));
        }

        // Per-CAD issues
        for (var c = 0; c < cadCount; c++)
        {
            var cadId = cadPads[c].Id;
            var cadSequenceIndex = ResolveCadSequenceIndex(c);
            var regIndex = cadToReg[c];
            if (regIndex < 0)
            {
                if (!cadHasOverlapEvidence[c])
                {
                    continue;
                }

                issues.Add(new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.UnmappedCad,
                    $"Expected diff idx {cadSequenceIndex} (CAD id {cadId}).",
                    DxfIndex: cadSequenceIndex,
                    CadPadId: cadId,
                    BestScore: cadBest[c],
                    SecondScore: cadSecond[c],
                    Margin: cadBest[c] - cadSecond[c],
                    TopCandidates: cadTopCandidates[c]));
                continue;
            }

            var assignedScore = cadAssignedScore[c];
            var reg = regPads[regIndex];
            var margin = cadBest[c] - cadSecond[c];

            if (assignedScore < settings.LowConfidenceThreshold)
            {
                lowConfidenceCount++;
                issues.Add(new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.LowConfidence,
                    $"Expected diff idx {cadSequenceIndex} (CAD id {cadId}) -> diff{reg.DiffIndex} (IC{reg.IcIndex}, regId {reg.RegularPadId}), score={assignedScore:0.###}.",
                    DxfIndex: cadSequenceIndex,
                    CadPadId: cadId,
                    RegularPadIndex: reg.RegularPadId,
                    IcIndex: reg.IcIndex,
                    DiffIndex: reg.DiffIndex,
                    Score: assignedScore,
                    BestScore: cadBest[c],
                    SecondScore: cadSecond[c],
                    Margin: margin,
                    TopCandidates: cadTopCandidates[c]));
            }

            if (cadBest[c] > 0 && margin >= 0 && margin < settings.AmbiguousMargin)
            {
                ambiguousCount++;
                issues.Add(new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.Ambiguous,
                    $"Expected diff idx {cadSequenceIndex} (CAD id {cadId}) best={cadBest[c]:0.###}, second={cadSecond[c]:0.###}, margin={margin:0.###}.",
                    DxfIndex: cadSequenceIndex,
                    CadPadId: cadId,
                    RegularPadIndex: reg.RegularPadId,
                    IcIndex: reg.IcIndex,
                    DiffIndex: reg.DiffIndex,
                    Score: assignedScore,
                    BestScore: cadBest[c],
                    SecondScore: cadSecond[c],
                    Margin: margin,
                    TopCandidates: cadTopCandidates[c]));
            }

            MaybeReportProgress(
                reportProgress,
                0.80 + (0.15 * (c + 1) / Math.Max(1, cadCount)),
                c + 1,
                cadCount,
                interval: 64);
        }

        if (cadCount == 0)
        {
            ReportProgress(reportProgress, 0.95);
        }

        var duplicateDiffGroups = Enumerable.Range(0, cadCount)
            .Where(c => cadToReg[c] >= 0)
            .Select(c => new
            {
                CadIndex = c,
                CadPadId = cadPads[c].Id,
                DxfIndex = ResolveCadSequenceIndex(c),
                Regular = regPads[cadToReg[c]],
                Score = cadAssignedScore[c],
            })
            .GroupBy(static entry => (entry.Regular.IcIndex, entry.DxfIndex))
            .Where(static group => group.Count() > 1)
            .ToList();
        duplicateDiffCount = duplicateDiffGroups.Sum(group => group.Count());
        foreach (var duplicateGroup in duplicateDiffGroups)
        {
            var cadIdsText = string.Join(", ", duplicateGroup.Select(static entry => entry.CadPadId).OrderBy(static id => id));
            var message = $"Current diff idx {duplicateGroup.Key.DxfIndex} in IC{duplicateGroup.Key.IcIndex + 1} is shared by CAD {cadIdsText}.";
            foreach (var duplicateEntry in duplicateGroup)
            {
                issues.Add(new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.DuplicateDiff,
                    message,
                    DxfIndex: duplicateEntry.DxfIndex,
                    CadPadId: duplicateEntry.CadPadId,
                    RegularPadIndex: duplicateEntry.Regular.RegularPadId,
                    IcIndex: duplicateEntry.Regular.IcIndex,
                    DiffIndex: duplicateEntry.Regular.DiffIndex,
                    Score: duplicateEntry.Score,
                    BestScore: cadBest[duplicateEntry.CadIndex],
                    SecondScore: cadSecond[duplicateEntry.CadIndex],
                    Margin: cadBest[duplicateEntry.CadIndex] - cadSecond[duplicateEntry.CadIndex],
                    TopCandidates: cadTopCandidates[duplicateEntry.CadIndex]));
            }
        }

        // Per-regular issues
        for (var i = 0; i < regCount; i++)
        {
            if (regToCad[i] >= 0)
            {
                continue;
            }

            if (!regularHasOverlapEvidence[i])
            {
                continue;
            }

            var reg = regPads[i];
            issues.Add(new DxfRegularMappingIssue(
                DxfRegularMappingIssueKind.UnmappedRegular,
                $"diff{reg.DiffIndex} (IC{reg.IcIndex}, regId {reg.RegularPadId}).",
                RegularPadIndex: reg.RegularPadId,
                IcIndex: reg.IcIndex,
                DiffIndex: reg.DiffIndex));

            MaybeReportProgress(
                reportProgress,
                0.95 + (0.04 * (i + 1) / Math.Max(1, regCount)),
                i + 1,
                regCount,
                interval: 128);
        }

        if (regCount == 0)
        {
            ReportProgress(reportProgress, 0.99);
        }

        var totalIssueCount = issues.Count;
        var issuesTruncated = issues.Count > settings.MaxReportIssues;
        if (issuesTruncated)
        {
            issues = issues.Take(settings.MaxReportIssues).ToList();
        }

        var sampleIssues = issues.Take(Math.Min(settings.MaxSampleIssues, issues.Count)).ToList();

        var hasIssues = issues.Count > 0;

        var summary = $"mapped={mappedCount}, unmappedCad={unmappedCadCount}, unmappedRegular={unmappedRegularCount}, lowConf={lowConfidenceCount}, ambiguous={ambiguousCount}, duplicateDiff={duplicateDiffCount}";

        var report = new DxfRegularMappingReport(
            cadCount,
            regCount,
            mappedCount,
            unmappedCadCount,
            unmappedRegularCount,
            lowConfidenceCount,
            ambiguousCount,
            hasIssues,
            summary,
            TotalIssueCount: totalIssueCount,
            IssuesTruncated: issuesTruncated,
            Issues: issues,
            sampleIssues,
            DuplicateDiffCount: duplicateDiffCount);

        ReportProgress(reportProgress, 1.0);
        return new DxfRegularMappingResult(mappedPairs, report);
    }

    private static void ReportProgress(Action<double>? reportProgress, double value)
    {
        if (reportProgress is null)
        {
            return;
        }

        reportProgress(Math.Clamp(value, 0.0, 1.0));
    }

    private static void MaybeReportProgress(
        Action<double>? reportProgress,
        double value,
        int processed,
        int total,
        int interval)
    {
        if (reportProgress is null || total <= 0)
        {
            return;
        }

        if (processed >= total || processed % interval == 0)
        {
            ReportProgress(reportProgress, value);
        }
    }
}

public sealed record DxfRegularMappingResult(
    IReadOnlyList<DxfRegularMappingPair> Pairs,
    DxfRegularMappingReport Report);

public sealed record DxfRegularMappingPair(
    int DxfIndex,
    int CadPadId,
    int RegularPadIndex,
    int IcIndex,
    int DiffIndex,
    double Score);

public sealed record DxfRegularMappingReport(
    int CadCount,
    int RegularCount,
    int MappedCount,
    int UnmappedCadCount,
    int UnmappedRegularCount,
    int LowConfidenceCount,
    int AmbiguousCount,
    bool HasIssues,
    string Summary,
    int TotalIssueCount,
    bool IssuesTruncated,
    IReadOnlyList<DxfRegularMappingIssue> Issues,
    IReadOnlyList<DxfRegularMappingIssue> SampleIssues,
    IReadOnlyList<DxfRegularMaskAuditRow>? MaskAuditRows = null,
    int MaskChangedCount = 0,
    int MaskRemovedCount = 0,
    int DuplicateDiffCount = 0);

public enum DxfRegularMappingIssueKind
{
    CountMismatch = 0,
    UnmappedCad = 1,
    UnmappedRegular = 2,
    LowConfidence = 3,
    Ambiguous = 4,
    DuplicateDiff = 5,
}

public sealed record DxfRegularMappingIssue(
    DxfRegularMappingIssueKind Kind,
    string Message,
    int? DxfIndex = null,
    int? CadPadId = null,
    int? RegularPadIndex = null,
    int? IcIndex = null,
    int? DiffIndex = null,
    double? Score = null,
    double? BestScore = null,
    double? SecondScore = null,
    double? Margin = null,
    IReadOnlyList<DxfRegularMappingCandidate>? TopCandidates = null);

public sealed record DxfRegularMappingCandidate(
    int RegularPadIndex,
    int IcIndex,
    int DiffIndex,
    double Score);
