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
        // Defer conversion until overrides are applied, after candidate progress callbacks.
        return AnalyzeCore(cad, grid, settings,
            manualOverrides is null ? null : new LegacyManualOverrides(manualOverrides),
            expectedDiffIndexByCadId, reportProgress);
    }

    /// <summary>
    /// Analyzes with typed override identities. A distinct name preserves legacy null-literal calls.
    /// </summary>
    public static DxfRegularMappingResult AnalyzeWithTypedOverrides(
        CadPadSet cad,
        RegularGrid grid,
        IndexMappingSettings settings,
        IReadOnlyDictionary<CadPadId, RegularPadId>? manualOverrides,
        IReadOnlyDictionary<int, int>? expectedDiffIndexByCadId = null,
        Action<double>? reportProgress = null)
    {
        return AnalyzeCore(cad, grid, settings, manualOverrides, expectedDiffIndexByCadId, reportProgress);
    }

    private static DxfRegularMappingResult AnalyzeCore(
        CadPadSet cad,
        RegularGrid grid,
        IndexMappingSettings settings,
        IReadOnlyCollection<KeyValuePair<CadPadId, RegularPadId>>? manualOverrides,
        IReadOnlyDictionary<int, int>? expectedDiffIndexByCadId,
        Action<double>? reportProgress)
    {
        ReportProgress(reportProgress, 0.0);
        settings ??= new IndexMappingSettings();
        settings.ValidateOrThrow();

        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);

        var cadPads = cad.Pads;
        var regPads = grid.Pads;

        var scan = ScanCandidates(cad, grid, settings, reportProgress);
        ReportProgress(reportProgress, 0.60);

        var assignment = AssignPairs(cadPads, regPads, settings, manualOverrides, scan, reportProgress);
        var mappedPairs = BuildMappedPairs(cadPads, regPads, assignment, expectedDiffIndexByCadId);

        // --- Build report ---
        var inputs = new ReportInputs(cadPads, regPads, settings, scan, assignment, expectedDiffIndexByCadId, reportProgress);
        var tally = CollectIssues(inputs);
        var report = BuildReport(inputs, mappedPairs.Count, tally);

        ReportProgress(reportProgress, 1.0);
        return new DxfRegularMappingResult(mappedPairs, report);
    }

    private static CandidateScan ScanCandidates(
        CadPadSet cad,
        RegularGrid grid,
        IndexMappingSettings settings,
        Action<double>? reportProgress)
    {
        var cadPads = cad.Pads;
        var cadCount = cadPads.Count;
        var regCount = grid.Pads.Count;

        var padding = Math.Max(0, settings.CandidatePaddingCells);
        var candidateNumber = Math.Max(1, settings.GetEffectiveCandidateNumber());
        var diagnosticTopK = Math.Max(1, settings.DiagnosticTopK);
        var limits = new CandidateLimits(padding, candidateNumber, diagnosticTopK, Math.Max(candidateNumber, diagnosticTopK));

        var estimatedCandidatesPerCad = Math.Max(1, (2 * padding + 1) * (2 * padding + 1));
        var scan = new CandidateScan(
            new List<CandidatePair>(cadCount * Math.Min(estimatedCandidatesPerCad, limits.RetainCount)),
            new List<DxfRegularMappingCandidate>[cadCount],
            new double[cadCount],
            new double[cadCount],
            new bool[cadCount],
            new bool[regCount]);

        for (var c = 0; c < cadCount; c++)
        {
            ScanCadCandidates(scan, c, cadPads[c], grid, settings, limits);

            MaybeReportProgress(
                reportProgress,
                0.05 + (0.55 * (c + 1) / Math.Max(1, cadCount)),
                c + 1,
                cadCount,
                interval: 4);
        }

        return scan;
    }

    private static void ScanCadCandidates(
        CandidateScan scan,
        int c,
        CadPad cp,
        RegularGrid grid,
        IndexMappingSettings settings,
        CandidateLimits limits)
    {
        var regPads = grid.Pads;
        var (rowMin, rowMax, colMin, colMax) = RegularGridCandidateQuery.GetCandidateRange(grid, cp.Bounds, limits.Padding);

        var best = 0.0;
        var second = 0.0;
        var retainedCandidates = new List<CandidatePair>(limits.RetainCount);

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

                scan.CadHasOverlapEvidence[c] = true;
                scan.RegularHasOverlapEvidence[regIndex] = true;

                CandidateDiagnosticsBuilder.UpdateTopCandidates(retainedCandidates, c, regIndex, score, limits.RetainCount);

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

        scan.Best[c] = best;
        scan.Second[c] = second;
        scan.TopCandidates[c] = CandidateDiagnosticsBuilder.BuildCandidateDiagnostics(
            retainedCandidates.Take(Math.Min(limits.DiagnosticTopK, retainedCandidates.Count)).ToList(),
            regPads);

        foreach (var retained in retainedCandidates.Take(Math.Min(limits.CandidateNumber, retainedCandidates.Count)))
        {
            scan.Pairs.Add(retained);
        }
    }

    private static PadAssignment AssignPairs(
        IReadOnlyList<CadPad> cadPads,
        IReadOnlyList<RegularPad> regPads,
        IndexMappingSettings settings,
        IReadOnlyCollection<KeyValuePair<CadPadId, RegularPadId>>? manualOverrides,
        CandidateScan scan,
        Action<double>? reportProgress)
    {
        var assignment = new PadAssignment(
            Enumerable.Repeat(-1, cadPads.Count).ToArray(),
            Enumerable.Repeat(-1, regPads.Count).ToArray(),
            new double[cadPads.Count]);

        ManualOverrideApplier.Apply(
            manualOverrides,
            cadPads,
            regPads,
            settings,
            assignment.CadToReg,
            assignment.RegToCad,
            assignment.CadAssignedScore);

        var pairs = scan.Pairs;
        pairs.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        var pairCount = pairs.Count;
        for (var i = 0; i < pairCount; i++)
        {
            var p = pairs[i];
            if (assignment.CadToReg[p.CadIndex] < 0 && assignment.RegToCad[p.RegularPadIndex] < 0)
            {
                assignment.CadToReg[p.CadIndex] = p.RegularPadIndex;
                assignment.RegToCad[p.RegularPadIndex] = p.CadIndex;
                assignment.CadAssignedScore[p.CadIndex] = p.Score;
            }

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

        return assignment;
    }

    private static int ResolveCadSequenceIndex(
        IReadOnlyList<CadPad> cadPads,
        IReadOnlyDictionary<int, int>? expectedDiffIndexByCadId,
        int cadIndex)
    {
        if (expectedDiffIndexByCadId is not null &&
            expectedDiffIndexByCadId.TryGetValue(cadPads[cadIndex].Id, out var mapped))
        {
            return mapped;
        }

        return cadIndex;
    }

    private static List<DxfRegularMappingPair> BuildMappedPairs(
        IReadOnlyList<CadPad> cadPads,
        IReadOnlyList<RegularPad> regPads,
        PadAssignment assignment,
        IReadOnlyDictionary<int, int>? expectedDiffIndexByCadId)
    {
        var cadCount = cadPads.Count;
        var mappedPairs = new List<DxfRegularMappingPair>(Math.Min(cadCount, regPads.Count));
        for (var c = 0; c < cadCount; c++)
        {
            var regIndex = assignment.CadToReg[c];
            if (regIndex < 0)
            {
                continue;
            }

            var reg = regPads[regIndex];
            var cadSequenceIndex = ResolveCadSequenceIndex(cadPads, expectedDiffIndexByCadId, c);
            mappedPairs.Add(new DxfRegularMappingPair(
                DxfIndex: cadSequenceIndex,
                CadPadId: cadPads[c].Id,
                RegularPadIndex: reg.RegularPadId,
                IcIndex: reg.IcIndex,
                DiffIndex: reg.DiffIndex,
                Score: assignment.CadAssignedScore[c]));
        }

        return mappedPairs;
    }

    private static IssueTally CollectIssues(ReportInputs inputs)
    {
        var cadCount = inputs.CadPads.Count;
        var regCount = inputs.RegPads.Count;
        var tally = new IssueTally(Math.Min(inputs.Settings.MaxReportIssues, cadCount + regCount + 1));

        if (cadCount != regCount)
        {
            tally.Issues.Add(new DxfRegularMappingIssue(
                DxfRegularMappingIssueKind.CountMismatch,
                $"visible DXF pads={cadCount}, regular pads={regCount}."));
        }

        AddPerCadIssues(inputs, tally);

        if (cadCount == 0)
        {
            ReportProgress(inputs.ReportProgress, 0.95);
        }

        AddDuplicateDiffIssues(inputs, tally);
        AddUnmappedRegularIssues(inputs, tally);

        if (regCount == 0)
        {
            ReportProgress(inputs.ReportProgress, 0.99);
        }

        return tally;
    }

    private static void AddPerCadIssues(ReportInputs inputs, IssueTally tally)
    {
        var cadCount = inputs.CadPads.Count;
        for (var c = 0; c < cadCount; c++)
        {
            var regIndex = inputs.Assignment.CadToReg[c];
            if (regIndex < 0)
            {
                AddUnmappedCadIssue(inputs, tally, c);
                continue;
            }

            AddMappedCadIssues(inputs, tally, c, inputs.RegPads[regIndex]);

            MaybeReportProgress(
                inputs.ReportProgress,
                0.80 + (0.15 * (c + 1) / Math.Max(1, cadCount)),
                c + 1,
                cadCount,
                interval: 64);
        }
    }

    private static void AddUnmappedCadIssue(ReportInputs inputs, IssueTally tally, int c)
    {
        if (!inputs.Scan.CadHasOverlapEvidence[c])
        {
            return;
        }

        var cadId = inputs.CadPads[c].Id;
        var cadSequenceIndex = ResolveCadSequenceIndex(inputs.CadPads, inputs.ExpectedDiffIndexByCadId, c);
        tally.Issues.Add(new DxfRegularMappingIssue(
            DxfRegularMappingIssueKind.UnmappedCad,
            $"Expected diff idx {cadSequenceIndex} (CAD id {cadId}).",
            DxfIndex: cadSequenceIndex,
            CadPadId: cadId,
            BestScore: inputs.Scan.Best[c],
            SecondScore: inputs.Scan.Second[c],
            Margin: inputs.Scan.Best[c] - inputs.Scan.Second[c],
            TopCandidates: inputs.Scan.TopCandidates[c]));
    }

    private static void AddMappedCadIssues(ReportInputs inputs, IssueTally tally, int c, RegularPad reg)
    {
        var scan = inputs.Scan;
        var settings = inputs.Settings;
        var cadId = inputs.CadPads[c].Id;
        var cadSequenceIndex = ResolveCadSequenceIndex(inputs.CadPads, inputs.ExpectedDiffIndexByCadId, c);
        var assignedScore = inputs.Assignment.CadAssignedScore[c];
        var margin = scan.Best[c] - scan.Second[c];

        if (assignedScore < settings.LowConfidenceThreshold)
        {
            tally.LowConfidenceCount++;
            tally.Issues.Add(new DxfRegularMappingIssue(
                DxfRegularMappingIssueKind.LowConfidence,
                $"Expected diff idx {cadSequenceIndex} (CAD id {cadId}) -> diff{reg.DiffIndex} (IC{reg.IcIndex}, regId {reg.RegularPadId}), score={assignedScore:0.###}.",
                DxfIndex: cadSequenceIndex,
                CadPadId: cadId,
                RegularPadIndex: reg.RegularPadId,
                IcIndex: reg.IcIndex,
                DiffIndex: reg.DiffIndex,
                Score: assignedScore,
                BestScore: scan.Best[c],
                SecondScore: scan.Second[c],
                Margin: margin,
                TopCandidates: scan.TopCandidates[c]));
        }

        if (scan.Best[c] > 0 && margin >= 0 && margin < settings.AmbiguousMargin)
        {
            tally.AmbiguousCount++;
            tally.Issues.Add(new DxfRegularMappingIssue(
                DxfRegularMappingIssueKind.Ambiguous,
                $"Expected diff idx {cadSequenceIndex} (CAD id {cadId}) best={scan.Best[c]:0.###}, second={scan.Second[c]:0.###}, margin={margin:0.###}.",
                DxfIndex: cadSequenceIndex,
                CadPadId: cadId,
                RegularPadIndex: reg.RegularPadId,
                IcIndex: reg.IcIndex,
                DiffIndex: reg.DiffIndex,
                Score: assignedScore,
                BestScore: scan.Best[c],
                SecondScore: scan.Second[c],
                Margin: margin,
                TopCandidates: scan.TopCandidates[c]));
        }
    }

    private static void AddDuplicateDiffIssues(ReportInputs inputs, IssueTally tally)
    {
        var cadPads = inputs.CadPads;
        var regPads = inputs.RegPads;
        var scan = inputs.Scan;
        var assignment = inputs.Assignment;
        var duplicateDiffGroups = Enumerable.Range(0, cadPads.Count)
            .Where(c => assignment.CadToReg[c] >= 0)
            .Select(c => new
            {
                CadIndex = c,
                CadPadId = cadPads[c].Id,
                DxfIndex = ResolveCadSequenceIndex(cadPads, inputs.ExpectedDiffIndexByCadId, c),
                Regular = regPads[assignment.CadToReg[c]],
                Score = assignment.CadAssignedScore[c],
            })
            .GroupBy(static entry => (entry.Regular.IcIndex, entry.DxfIndex))
            .Where(static group => group.Count() > 1)
            .ToList();
        tally.DuplicateDiffCount = duplicateDiffGroups.Sum(group => group.Count());
        foreach (var duplicateGroup in duplicateDiffGroups)
        {
            var cadIdsText = string.Join(", ", duplicateGroup.Select(static entry => entry.CadPadId).OrderBy(static id => id));
            var message = $"Current diff idx {duplicateGroup.Key.DxfIndex} in IC{duplicateGroup.Key.IcIndex + 1} is shared by CAD {cadIdsText}.";
            foreach (var duplicateEntry in duplicateGroup)
            {
                tally.Issues.Add(new DxfRegularMappingIssue(
                    DxfRegularMappingIssueKind.DuplicateDiff,
                    message,
                    DxfIndex: duplicateEntry.DxfIndex,
                    CadPadId: duplicateEntry.CadPadId,
                    RegularPadIndex: duplicateEntry.Regular.RegularPadId,
                    IcIndex: duplicateEntry.Regular.IcIndex,
                    DiffIndex: duplicateEntry.Regular.DiffIndex,
                    Score: duplicateEntry.Score,
                    BestScore: scan.Best[duplicateEntry.CadIndex],
                    SecondScore: scan.Second[duplicateEntry.CadIndex],
                    Margin: scan.Best[duplicateEntry.CadIndex] - scan.Second[duplicateEntry.CadIndex],
                    TopCandidates: scan.TopCandidates[duplicateEntry.CadIndex]));
            }
        }
    }

    private static void AddUnmappedRegularIssues(ReportInputs inputs, IssueTally tally)
    {
        var regCount = inputs.RegPads.Count;
        for (var i = 0; i < regCount; i++)
        {
            if (inputs.Assignment.RegToCad[i] >= 0)
            {
                continue;
            }

            if (!inputs.Scan.RegularHasOverlapEvidence[i])
            {
                continue;
            }

            var reg = inputs.RegPads[i];
            tally.Issues.Add(new DxfRegularMappingIssue(
                DxfRegularMappingIssueKind.UnmappedRegular,
                $"diff{reg.DiffIndex} (IC{reg.IcIndex}, regId {reg.RegularPadId}).",
                RegularPadIndex: reg.RegularPadId,
                IcIndex: reg.IcIndex,
                DiffIndex: reg.DiffIndex));

            MaybeReportProgress(
                inputs.ReportProgress,
                0.95 + (0.04 * (i + 1) / Math.Max(1, regCount)),
                i + 1,
                regCount,
                interval: 128);
        }
    }

    private static DxfRegularMappingReport BuildReport(ReportInputs inputs, int mappedCount, IssueTally tally)
    {
        var settings = inputs.Settings;
        var cadCount = inputs.CadPads.Count;
        var regCount = inputs.RegPads.Count;
        var unmappedCadCount = cadCount - mappedCount;
        var unmappedRegularCount = inputs.Assignment.RegToCad.Count(x => x < 0);

        var issues = tally.Issues;
        var totalIssueCount = issues.Count;
        var issuesTruncated = issues.Count > settings.MaxReportIssues;
        if (issuesTruncated)
        {
            issues = issues.Take(settings.MaxReportIssues).ToList();
        }

        var sampleIssues = issues.Take(Math.Min(settings.MaxSampleIssues, issues.Count)).ToList();

        var hasIssues = issues.Count > 0;

        var summary = $"mapped={mappedCount}, unmappedCad={unmappedCadCount}, unmappedRegular={unmappedRegularCount}, lowConf={tally.LowConfidenceCount}, ambiguous={tally.AmbiguousCount}, duplicateDiff={tally.DuplicateDiffCount}";

        return new DxfRegularMappingReport(
            cadCount,
            regCount,
            mappedCount,
            unmappedCadCount,
            unmappedRegularCount,
            tally.LowConfidenceCount,
            tally.AmbiguousCount,
            hasIssues,
            summary,
            TotalIssueCount: totalIssueCount,
            IssuesTruncated: issuesTruncated,
            Issues: issues,
            sampleIssues,
            DuplicateDiffCount: tally.DuplicateDiffCount);
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

    private readonly record struct CandidateLimits(int Padding, int CandidateNumber, int DiagnosticTopK, int RetainCount);

    private readonly record struct CandidateScan(
        List<CandidatePair> Pairs,
        List<DxfRegularMappingCandidate>[] TopCandidates,
        double[] Best,
        double[] Second,
        bool[] CadHasOverlapEvidence,
        bool[] RegularHasOverlapEvidence);

    private readonly record struct PadAssignment(int[] CadToReg, int[] RegToCad, double[] CadAssignedScore);

    private readonly record struct ReportInputs(
        IReadOnlyList<CadPad> CadPads,
        IReadOnlyList<RegularPad> RegPads,
        IndexMappingSettings Settings,
        CandidateScan Scan,
        PadAssignment Assignment,
        IReadOnlyDictionary<int, int>? ExpectedDiffIndexByCadId,
        Action<double>? ReportProgress);

    private sealed class IssueTally(int capacity)
    {
        public List<DxfRegularMappingIssue> Issues { get; } = new(capacity);

        public int LowConfidenceCount { get; set; }

        public int AmbiguousCount { get; set; }

        public int DuplicateDiffCount { get; set; }
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
