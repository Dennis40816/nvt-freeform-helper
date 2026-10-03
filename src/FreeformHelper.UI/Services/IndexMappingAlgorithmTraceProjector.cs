using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal static class IndexMappingAlgorithmTraceProjector
{
    public static IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel> Build(
        IndexMappingDecisionRowViewModel row,
        int traceSchemaVersion,
        DateTime generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(row);

        var generatedAt = generatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        var entries = new List<IndexMappingAlgorithmTraceEntryViewModel>
        {
            new("algorithm", "DxfCadOutputFwDiffAssignment + MaskAudit", true),
            new("rule id", row.DecisionReasonCodeText, true),
            new("mode", row.DecisionModeText),
            new("source", row.DecisionSourceText),
            new("input.current", row.CurrentDisplay),
            new("input.raw", row.RawDisplay),
            new("output.suggested", row.SuggestedDisplay),
            new("confidence", row.DecisionConfidence.HasValue
                ? row.DecisionConfidence.Value.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)
                : "n/a"),
            new("segment", row.SegmentKey),
            new("candidateCount", row.CandidateLines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("final reason", row.ReasonText),
            new("traceSchemaVersion", traceSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("generatedAt", generatedAt),
        };

        if (row.PassiveCompensationDiffIndex.HasValue)
        {
            entries.Add(new(
                "derived.passiveCompensationDiff",
                row.PassiveCompensationDiffIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (row.ApplyDiffIndex.HasValue)
        {
            entries.Add(new(
                "derived.repairSuggestionDiff",
                row.ApplyDiffIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (row.HasCoverageText)
        {
            entries.Add(new("coverage", row.CoverageText));
        }

        return entries;
    }
}
