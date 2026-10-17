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

/// <summary>Builds the algorithm trace of one decision row.</summary>
internal interface IIndexMappingTraceBuilder
{
    IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel> Build(IndexMappingDecisionRowViewModel row, int traceSchemaVersion);
}

/// <summary>Default builder: the projector with the current UTC time.</summary>
internal sealed class IndexMappingTraceBuilder : IIndexMappingTraceBuilder
{
    public static IndexMappingTraceBuilder Instance { get; } = new();

    private IndexMappingTraceBuilder()
    {
    }

    public IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel> Build(IndexMappingDecisionRowViewModel row, int traceSchemaVersion) =>
        IndexMappingAlgorithmTraceProjector.Build(row, traceSchemaVersion, TimeProvider.System.GetUtcNow().UtcDateTime);
}

/// <summary>
/// The builder a report view model uses. It is fixed per view model and forwards to <see cref="Builder"/>,
/// which is the default builder unless a test replaces it.
/// </summary>
internal sealed class IndexMappingTraceBuilderSlot : IIndexMappingTraceBuilder
{
    public IIndexMappingTraceBuilder Builder { get; set; } = IndexMappingTraceBuilder.Instance;

    public IReadOnlyList<IndexMappingAlgorithmTraceEntryViewModel> Build(IndexMappingDecisionRowViewModel row, int traceSchemaVersion) =>
        Builder.Build(row, traceSchemaVersion);
}
