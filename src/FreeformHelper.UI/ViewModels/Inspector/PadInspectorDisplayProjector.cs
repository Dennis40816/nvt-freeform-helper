using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Projects pad inspector snapshots into display text and compensation lines.
/// </summary>
internal static class PadInspectorDisplayProjector
{
    internal delegate bool CadDisplayIndexLookup(int cadPadId, out int displayIndex);

    /// <summary>
    /// Builds the panel text and lines for a snapshot. Returns null when the snapshot has neither
    /// a CAD nor a regular part, so the caller shows the empty display.
    /// </summary>
    internal static PadInspectorDisplay? BuildPadInspectorDisplay(
        PadInspectorSnapshot value,
        CadDisplayIndexLookup tryGetCadDisplayIndex,
        Func<CadPadInspectorSnapshot, ObservableCollection<PadInspectorDisplayLine>> buildCadCompensationLines)
    {
        string compactHeaderText;
        string primaryText;
        string secondaryText;
        string matchText;
        if (value.Cad is CadPadInspectorSnapshot cad)
        {
            var icText = cad.IcIndex.HasValue ? $"IC {cad.IcIndex.Value + 1}" : "IC -";
            var diffText = cad.DxfIndex.HasValue ? cad.DxfIndex.Value.ToString(CultureInfo.InvariantCulture) : "-";
            var cadDisplayText = tryGetCadDisplayIndex(cad.CadPadId, out var displayIndex)
                ? $"CAD #{displayIndex} (id {cad.CadPadId})"
                : $"CAD {cad.CadPadId}";
            var matchCount = cad.MatchedRegularDetails.Count;
            var confidenceText = matchCount == 0
                ? "-"
                : cad.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture);
            compactHeaderText = $"{cadDisplayText} · Diff {diffText}";
            primaryText = $"{cadDisplayText} · {icText} · Diff idx {diffText}";
            secondaryText = $"Diff source: {cad.CadOutputFwDiffAssignmentModeText}";
            matchText = $"Match: {matchCount} regular pad(s), confidence {confidenceText}";
        }
        else if (value.Regular is RegularPadInspectorSnapshot regular)
        {
            var matchCount = regular.MatchedCadDetails.Count;
            var confidenceText = matchCount == 0
                ? "-"
                : regular.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture);
            compactHeaderText = $"REG {regular.RegularPadId} · Diff {regular.DiffIndex}";
            primaryText = $"REG {regular.RegularPadId} · IC {regular.IcIndex + 1} · Diff idx {regular.DiffIndex}";
            secondaryText = $"Diff source: {regular.DiffSource}";
            matchText = $"Match: {matchCount} CAD pad(s), confidence {confidenceText}";
        }
        else
        {
            return null;
        }

        var compensationLines = BuildCompensationLines(value, buildCadCompensationLines);
        var ruleTraceSections = PadInspectorTraceSectionProjector.BuildRuleTraceSections(value.RuleTrace);
        return new PadInspectorDisplay(
            HasSummary: true,
            CompactHeaderText: compactHeaderText,
            PrimaryText: primaryText,
            SecondaryText: secondaryText,
            MatchText: matchText,
            CompensationLines: compensationLines,
            RuleTraceLines: new ObservableCollection<string>(
                ruleTraceSections
                    .SelectMany(static section => section.Lines)
                    .Select(static line => line.DisplayText)),
            RuleTraceSections: ruleTraceSections,
            HasRuleTrace: ruleTraceSections.Any(static section => section.Lines.Count > 0));
    }

    internal static ObservableCollection<PadInspectorDisplayLine> BuildCompensationLines(
        PadInspectorSnapshot snapshot,
        Func<CadPadInspectorSnapshot, ObservableCollection<PadInspectorDisplayLine>> buildCadCompensationLines)
    {
        if (snapshot.Cad is CadPadInspectorSnapshot cad)
        {
            return buildCadCompensationLines(cad);
        }

        if (snapshot.Regular is RegularPadInspectorSnapshot regular)
        {
            var lines = new ObservableCollection<PadInspectorDisplayLine>
            {
                new(
                    "Freeform",
                    regular.Freeform.ToString(),
                    IsEmphasized: true,
                    Tone: PadInspectorLineTone.Key,
                    FreeformDirection: PadInspectorTraceSectionProjector.ToFreeformDirection(regular.Freeform)),
            };

            if (regular.Freeform != FreeformType.None)
            {
                lines.Add(new(
                    "Source",
                    regular.FreeformSource,
                    Tone: PadInspectorLineTone.Normal));
            }

            return lines;
        }

        return new ObservableCollection<PadInspectorDisplayLine>();
    }

    internal static (string Source, string Detail) BuildRegularFreeformSource(
        RegularPad regularPad,
        IReadOnlyDictionary<int, FreeformType> freeformOverrides)
    {
        if (freeformOverrides.TryGetValue(regularPad.Index, out var overrideType) &&
            overrideType == regularPad.Freeform)
        {
            return ("Override", $"Saved/manual override keeps this regular pad as {regularPad.Freeform}.");
        }

        if (regularPad.Freeform == FreeformType.None)
        {
            return ("None", "No Step 2 freeform tag.");
        }

        return ("Step 2 auto-detect", $"Current value comes from Step 2 auto-detect ({regularPad.Freeform}).");
    }
}
