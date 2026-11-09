using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Projects pad inspector rule traces into ordered display sections.
/// </summary>
internal static class PadInspectorTraceSectionProjector
{
    internal static ObservableCollection<PadInspectorRuleTraceSection> BuildRuleTraceSections(
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

    internal static bool IsRuleTraceOutcomeEmphasized(string rule)
    {
        return rule.StartsWith("To Full", StringComparison.OrdinalIgnoreCase);
    }

    internal static PadInspectorLineTone ResolveRuleTraceOutcomeTone(PadInspectorRuleTraceEntry entry)
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

    internal static PadInspectorDisplayLine ParseRuleTraceDetailLine(string detailLine)
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

    internal static bool TrySplitLabelValue(string raw, out string label, out string value)
    {
        var separatorIndex = raw.IndexOf(':', StringComparison.Ordinal);
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

    internal static int GetRuleTraceSectionPriority(string title)
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

    internal static bool TryExtractNotchFreeformDirection(string text, out FreeformType freeformType)
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

    internal static PadInspectorFreeformDirection ToFreeformDirection(FreeformType freeformType)
    {
        return freeformType switch
        {
            FreeformType.XWay => PadInspectorFreeformDirection.XWay,
            FreeformType.YWay => PadInspectorFreeformDirection.YWay,
            FreeformType.XYWay => PadInspectorFreeformDirection.XYWay,
            _ => PadInspectorFreeformDirection.None,
        };
    }

    internal static string ClassifyRuleTraceSection(string rule)
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
