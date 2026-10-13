using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the text and line collections that the pad inspector panels show for one snapshot.
/// The view model replaces the whole value when the snapshot changes.
/// </summary>
public sealed record PadInspectorDisplay(
    bool HasSummary,
    string CompactHeaderText,
    string PrimaryText,
    string SecondaryText,
    string MatchText,
    string CompensationText,
    ObservableCollection<PadInspectorDisplayLine> CompensationLines,
    ObservableCollection<string> RuleTraceLines,
    ObservableCollection<PadInspectorRuleTraceSection> RuleTraceSections,
    bool HasRuleTrace)
{
    /// <summary>
    /// Creates the display of a missing snapshot. Each call returns new collections.
    /// </summary>
    public static PadInspectorDisplay CreateEmpty()
    {
        return new PadInspectorDisplay(
            HasSummary: false,
            CompactHeaderText: string.Empty,
            PrimaryText: string.Empty,
            SecondaryText: string.Empty,
            MatchText: string.Empty,
            CompensationText: string.Empty,
            CompensationLines: new ObservableCollection<PadInspectorDisplayLine>(),
            RuleTraceLines: new ObservableCollection<string>(),
            RuleTraceSections: new ObservableCollection<PadInspectorRuleTraceSection>(),
            HasRuleTrace: false);
    }
}
