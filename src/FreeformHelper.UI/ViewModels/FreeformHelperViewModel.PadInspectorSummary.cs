using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    partial void OnCurrentPadInspectorSnapshotChanged(PadInspectorSnapshot? value)
    {
        if (value is null)
        {
            HasPadInspectorSummary = false;
            PadInspectorCompactHeaderText = string.Empty;
            PadInspectorPrimaryText = string.Empty;
            PadInspectorSecondaryText = string.Empty;
            PadInspectorMatchText = string.Empty;
            PadInspectorCompensationText = string.Empty;
            PadInspectorCompensationLines = new ObservableCollection<PadInspectorDisplayLine>();
            PadInspectorRuleTraceLines = new ObservableCollection<string>();
            PadInspectorRuleTraceSections = new ObservableCollection<PadInspectorRuleTraceSection>();
            HasPadInspectorRuleTrace = false;
            IsPadInspectorTraceExpanded = false;
            IsPadInspectorTracePopupOpen = false;
            IsPadInspectorRawTraceExpanded = false;
            return;
        }

        if (value.Cad is CadPadInspectorSnapshot cad)
        {
            var icText = cad.IcIndex.HasValue ? $"IC {cad.IcIndex.Value + 1}" : "IC -";
            var diffText = cad.DxfIndex.HasValue ? cad.DxfIndex.Value.ToString(CultureInfo.InvariantCulture) : "-";
            var cadDisplayText = TryGetCadDisplayIndex(cad.CadPadId, out var displayIndex)
                ? $"CAD #{displayIndex} (id {cad.CadPadId})"
                : $"CAD {cad.CadPadId}";
            var matchCount = cad.MatchedRegularDetails.Count;
            var confidenceText = matchCount == 0
                ? "-"
                : cad.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture);
            PadInspectorCompactHeaderText = $"{cadDisplayText} · Diff {diffText}";
            PadInspectorPrimaryText = $"{cadDisplayText} · {icText} · Diff idx {diffText}";
            PadInspectorSecondaryText = $"Diff source: {cad.CadOutputFwDiffAssignmentModeText}";
            PadInspectorMatchText = $"Match: {matchCount} regular pad(s), confidence {confidenceText}";
        }
        else if (value.Regular is RegularPadInspectorSnapshot regular)
        {
            var matchCount = regular.MatchedCadDetails.Count;
            var confidenceText = matchCount == 0
                ? "-"
                : regular.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture);
            PadInspectorCompactHeaderText = $"REG {regular.RegularPadId} · Diff {regular.DiffIndex}";
            PadInspectorPrimaryText = $"REG {regular.RegularPadId} · IC {regular.IcIndex + 1} · Diff idx {regular.DiffIndex}";
            PadInspectorSecondaryText = $"Diff source: {regular.DiffSource}";
            PadInspectorMatchText = $"Match: {matchCount} CAD pad(s), confidence {confidenceText}";
        }
        else
        {
            HasPadInspectorSummary = false;
            PadInspectorCompactHeaderText = string.Empty;
            PadInspectorPrimaryText = string.Empty;
            PadInspectorSecondaryText = string.Empty;
            PadInspectorMatchText = string.Empty;
            PadInspectorCompensationText = string.Empty;
            PadInspectorCompensationLines = new ObservableCollection<PadInspectorDisplayLine>();
            PadInspectorRuleTraceLines = new ObservableCollection<string>();
            PadInspectorRuleTraceSections = new ObservableCollection<PadInspectorRuleTraceSection>();
            IsPadInspectorTraceExpanded = false;
            IsPadInspectorRawTraceExpanded = false;
            return;
        }

        PadInspectorCompensationLines = BuildCompensationLines(value);
        PadInspectorCompensationText = string.Join(
            Environment.NewLine,
            PadInspectorCompensationLines.Select(static line => line.DisplayText));
        PadInspectorRuleTraceSections = BuildRuleTraceSections(value.RuleTrace);
        PadInspectorRuleTraceLines = new ObservableCollection<string>(
            PadInspectorRuleTraceSections
                .SelectMany(static section => section.Lines)
                .Select(static line => line.DisplayText));
        HasPadInspectorRuleTrace = PadInspectorRuleTraceSections.Any(static section => section.Lines.Count > 0);
        IsPadInspectorTracePopupOpen = false;
        IsPadInspectorRawTraceExpanded = false;
        HasPadInspectorSummary = true;
    }

    partial void OnIsPadInspectorTraceExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(PadInspectorTraceToggleText));
    }

    partial void OnIsPadInspectorTracePopupOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(PadInspectorTracePopupToggleText));
    }

    private static ObservableCollection<PadInspectorDisplayLine> BuildCompensationLines(PadInspectorSnapshot snapshot)
    {
        if (snapshot.Cad is CadPadInspectorSnapshot cad)
        {
            return BuildCadCompensationLines(cad);
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
                    FreeformDirection: ToFreeformDirection(regular.Freeform)),
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

    private static ObservableCollection<PadInspectorDisplayLine> BuildCadCompensationLines(CadPadInspectorSnapshot cad)
    {
        var lines = new List<PadInspectorDisplayLine>();
        if (cad.Notch is null)
        {
            lines.Add(new("Notch 2.2", "Unavailable", IsEmphasized: true, Tone: PadInspectorLineTone.Warning));
            lines.Add(new("Notch rows", cad.NotchRowSummary));
            return new ObservableCollection<PadInspectorDisplayLine>(lines);
        }

        var notchDisplay = NotchDisplayProjector.Build(cad.Notch);
        var toFullTone = cad.Notch.IsToFullEnabled ? PadInspectorLineTone.Key : PadInspectorLineTone.Warning;
        var reasonTone = cad.Notch.IsToFullEnabled ? PadInspectorLineTone.Key : PadInspectorLineTone.Warning;

        lines.Add(new(
            "To Full",
            notchDisplay.ToFullValueText,
            IsEmphasized: true,
            Tone: toFullTone));
        if (!string.IsNullOrWhiteSpace(notchDisplay.ToFullReasonFullText))
        {
            lines.Add(new("Reason", notchDisplay.ToFullReasonFullText, IsEmphasized: true, Tone: reasonTone));
        }

        if (TryExtractNotchFreeformDirection(cad.NotchRowSummary, out var freeformDirection))
        {
            lines.Add(new(
                "Freeform",
                freeformDirection.ToString(),
                IsEmphasized: true,
                Tone: PadInspectorLineTone.Key,
                FreeformDirection: ToFreeformDirection(freeformDirection)));
        }

        lines.Add(new(
            "To Regular",
            notchDisplay.ToRegularValueText));
        lines.Add(new(
            "Combined",
            notchDisplay.CombinedValueText,
            IsEmphasized: notchDisplay.HasCombinedOverflowRisk,
            Tone: notchDisplay.HasCombinedOverflowRisk ? PadInspectorLineTone.Warning : PadInspectorLineTone.Normal));
        lines.Add(new("Notch rows", cad.NotchRowSummary));

        return new ObservableCollection<PadInspectorDisplayLine>(lines);
    }

    private static ObservableCollection<PadInspectorRuleTraceSection> BuildRuleTraceSections(
        IReadOnlyList<PadInspectorRuleTraceEntry> entries)
    {
        var orderedSections = new List<(string Title, List<PadInspectorDisplayLine> Lines)>();
        foreach (var entry in entries)
        {
            var title = ClassifyRuleTraceSection(entry.Rule);
            var section = orderedSections.FirstOrDefault(sectionItem =>
                string.Equals(sectionItem.Title, title, StringComparison.Ordinal));
            if (section.Lines is null)
            {
                section = (title, new List<PadInspectorDisplayLine>());
                orderedSections.Add(section);
            }

            section.Lines.Add(new PadInspectorDisplayLine(
                entry.Rule,
                entry.Outcome,
                IsEmphasized: IsRuleTraceOutcomeEmphasized(entry.Rule),
                Tone: ResolveRuleTraceOutcomeTone(entry)));
            if (!string.IsNullOrWhiteSpace(entry.Detail))
            {
                var detailLines = entry.Detail
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var detail in detailLines)
                {
                    section.Lines.Add(ParseRuleTraceDetailLine(detail));
                }
            }
        }

        return new ObservableCollection<PadInspectorRuleTraceSection>(
            orderedSections
                .Select((section, index) => new { section, index })
                .OrderBy(static item => GetRuleTraceSectionPriority(item.section.Title))
                .ThenBy(static item => item.index)
                .Select(static item => new PadInspectorRuleTraceSection(item.section.Title, item.section.Lines)));
    }

    private static bool IsRuleTraceOutcomeEmphasized(string rule)
    {
        return rule.StartsWith("To Full", StringComparison.OrdinalIgnoreCase);
    }

    private static PadInspectorLineTone ResolveRuleTraceOutcomeTone(PadInspectorRuleTraceEntry entry)
    {
        if (entry.Rule.StartsWith("To Full", StringComparison.OrdinalIgnoreCase))
        {
            return entry.Outcome.Contains("Disabled", StringComparison.OrdinalIgnoreCase)
                ? PadInspectorLineTone.Warning
                : PadInspectorLineTone.Key;
        }

        if (entry.Rule.StartsWith("Notch export", StringComparison.OrdinalIgnoreCase))
        {
            return entry.Outcome.Contains("Eligible", StringComparison.OrdinalIgnoreCase)
                ? PadInspectorLineTone.Key
                : PadInspectorLineTone.Warning;
        }

        if (entry.Rule.StartsWith("Match", StringComparison.OrdinalIgnoreCase))
        {
            return entry.Outcome.Contains("No matched", StringComparison.OrdinalIgnoreCase)
                ? PadInspectorLineTone.Warning
                : PadInspectorLineTone.Normal;
        }

        return PadInspectorLineTone.Normal;
    }

    private static PadInspectorDisplayLine ParseRuleTraceDetailLine(string detailLine)
    {
        if (TrySplitLabelValue(detailLine, out var label, out var value))
        {
            var tone = string.Equals(label, "Rule", StringComparison.OrdinalIgnoreCase) &&
                       value.Contains("NOT_BOUNDARY", StringComparison.OrdinalIgnoreCase)
                ? PadInspectorLineTone.Warning
                : PadInspectorLineTone.Normal;
            var freeformDirection = string.Equals(label, "freeform", StringComparison.OrdinalIgnoreCase) &&
                                    TryExtractNotchFreeformDirection(value, out var extracted)
                ? ToFreeformDirection(extracted)
                : PadInspectorFreeformDirection.None;
            return new PadInspectorDisplayLine(label, value, Tone: tone, FreeformDirection: freeformDirection);
        }

        return new PadInspectorDisplayLine(string.Empty, detailLine);
    }

    private static bool TrySplitLabelValue(string raw, out string label, out string value)
    {
        var separatorIndex = raw.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= raw.Length - 1)
        {
            label = string.Empty;
            value = string.Empty;
            return false;
        }

        var candidateLabel = raw[..separatorIndex].Trim();
        var candidateValue = raw[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(candidateLabel) || string.IsNullOrWhiteSpace(candidateValue))
        {
            label = string.Empty;
            value = string.Empty;
            return false;
        }

        label = candidateLabel;
        value = candidateValue;
        return true;
    }

    private static int GetRuleTraceSectionPriority(string title)
    {
        if (string.Equals(title, "Notch compute", StringComparison.Ordinal))
        {
            return 0;
        }

        if (string.Equals(title, "Notch eligibility", StringComparison.Ordinal))
        {
            return 1;
        }

        if (string.Equals(title, "Match", StringComparison.Ordinal))
        {
            return 2;
        }

        if (string.Equals(title, "Identity", StringComparison.Ordinal))
        {
            return 3;
        }

        if (string.Equals(title, "Debug raw", StringComparison.Ordinal))
        {
            return 4;
        }

        return 5;
    }

    private static bool TryExtractNotchFreeformDirection(string text, out FreeformType freeformType)
    {
        freeformType = FreeformType.None;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = Regex.Match(
            text,
            @"freeform(?:\s*=\s*|\s*:?\s*)(?<value>XWay|YWay|XYWay|None)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        var raw = match.Groups["value"].Value;
        if (string.Equals(raw, "XWay", StringComparison.OrdinalIgnoreCase))
        {
            freeformType = FreeformType.XWay;
            return true;
        }

        if (string.Equals(raw, "YWay", StringComparison.OrdinalIgnoreCase))
        {
            freeformType = FreeformType.YWay;
            return true;
        }

        if (string.Equals(raw, "XYWay", StringComparison.OrdinalIgnoreCase))
        {
            freeformType = FreeformType.XYWay;
            return true;
        }

        if (string.Equals(raw, "None", StringComparison.OrdinalIgnoreCase))
        {
            freeformType = FreeformType.None;
            return true;
        }

        return false;
    }

    private static PadInspectorFreeformDirection ToFreeformDirection(FreeformType freeformType)
    {
        return freeformType switch
        {
            FreeformType.XWay => PadInspectorFreeformDirection.XWay,
            FreeformType.YWay => PadInspectorFreeformDirection.YWay,
            FreeformType.XYWay => PadInspectorFreeformDirection.XYWay,
            _ => PadInspectorFreeformDirection.None,
        };
    }

    private (string Source, string Detail) BuildRegularFreeformSource(RegularPad regularPad)
    {
        if (regularPad.Freeform == FreeformType.None)
        {
            return ("None", "No Step 2 freeform tag.");
        }

        if (_projectFile.FreeformOverrides.TryGetValue(regularPad.Index, out var overrideType) &&
            overrideType == regularPad.Freeform)
        {
            return ("Override", $"Saved/manual override keeps this regular pad as {regularPad.Freeform}.");
        }

        return ("Step 2 auto-detect", $"Current value comes from Step 2 auto-detect ({regularPad.Freeform}).");
    }

    private static string ClassifyRuleTraceSection(string rule)
    {
        if (rule.StartsWith("Diff", StringComparison.OrdinalIgnoreCase))
        {
            return "Identity";
        }

        if (rule.StartsWith("Match", StringComparison.OrdinalIgnoreCase))
        {
            return "Match";
        }

        if (rule.StartsWith("Notch", StringComparison.OrdinalIgnoreCase))
        {
            return "Notch eligibility";
        }

        if (rule.StartsWith("To Regular", StringComparison.OrdinalIgnoreCase) ||
            rule.StartsWith("To Full", StringComparison.OrdinalIgnoreCase) ||
            rule.StartsWith("Switch", StringComparison.OrdinalIgnoreCase))
        {
            return "Notch compute";
        }

        if (rule.StartsWith("Raw", StringComparison.OrdinalIgnoreCase))
        {
            return "Debug raw";
        }

        return "Other";
    }
}
