using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal static partial class NotchDisplayProjector
{
    private static readonly char[] DiagnosticLineSeparators = ['\r', '\n'];
    private static readonly char[] DiagnosticOwnerSeparators = ['|', ';'];

    public static NotchDisplayProjection Build(PadInspectorNotchSnapshot notch)
    {
        ArgumentNullException.ThrowIfNull(notch);
        return Build(
            notch.ToRegularRatio,
            notch.ToFullRatio,
            notch.CombinedRatio,
            notch.IsToFullEnabled,
            notch.Stage3Area,
            notch.Targets.Select(static target => new NotchDisplayTargetInput(
                target.IcIndex,
                target.DiffIndex,
                target.EffectiveArea,
                target.Ratio,
                target.RatioPercentRounded,
                target.PassesStrictThreshold,
                target.IsAnchorDiff,
                target.ToFullAppliedRegularCount,
                target.RegularCount,
                target.RegularAreas.Select(static area => new NotchDisplayRegularAreaInput(
                    area.RegularPadId,
                    area.EffectiveArea)).ToList(),
                target.RegularPadIds)).ToList(),
            notch.Diagnostics,
            notch.TargetCoverageProjection);
    }

    public static NotchDisplayProjection Build(
        NotchV22CompensationResult compensation,
        NotchV22TargetAllocationSummary? allocation = null,
        string? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(compensation);
        return Build(
            compensation.ToRegularRatio,
            compensation.ToFullRatio,
            compensation.CombinedRatio,
            compensation.IsToFullEnabled,
            allocation?.Stage3Area ?? compensation.Stage3Area,
            allocation?.Targets.Select(static target => new NotchDisplayTargetInput(
                target.IcIndex,
                target.DiffIndex,
                target.EffectiveArea,
                target.Ratio,
                target.RatioPercentRounded,
                target.PassesStrictThreshold,
                target.IsAnchorDiff,
                target.ToFullAppliedRegularCount,
                target.RegularCount,
                target.RegularAreas.Select(static area => new NotchDisplayRegularAreaInput(
                    area.RegularPadId,
                    area.EffectiveArea)).ToList(),
                target.RegularPadIds)).ToList(),
            diagnostics,
            allocation?.TargetCoverageProjection);
    }

    public static NotchDisplayProjection Build(
        double toRegularRatio,
        double toFullRatio,
        double combinedRatio,
        bool isToFullEnabled,
        double? stage3Area,
        IReadOnlyList<NotchDisplayTargetInput>? targets,
        string? diagnostics,
        NotchV22TargetCoverageProjection? targetCoverageProjection = null)
    {
        var targetItems = targets ?? Array.Empty<NotchDisplayTargetInput>();
        var parsedEntries = ParseDiagnosticEntries(diagnostics);
        var fullReasonText = BuildToFullReasonText(parsedEntries, isToFullEnabled);
        var compactOwnerIds = parsedEntries
            .SelectMany(static entry => entry.OwnerCadPadIds)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
        var effectiveTargetCoverageProjection = targetCoverageProjection ??
                                                NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
                                                    Array.Empty<NotchV22TargetAllocation>(),
                                                    anchorIcIndex: null,
                                                    sourceDiffIndex: null,
                                                    NotchV22TargetAllocationAreaMode.SourceAreaDominant,
                                                    combinedRatio);
        var combinedValueText = BuildCombinedRatioValueText(
            effectiveTargetCoverageProjection.DisplayCombinedRatio,
            effectiveTargetCoverageProjection.HasCombinedOverflowRisk);
        IReadOnlySet<(int IcIndex, int DiffIndex)>? emittedTargetKeys =
            effectiveTargetCoverageProjection.RawCombinedPercent.HasValue
                ? effectiveTargetCoverageProjection.EmittedTargets
                    .Select(static target => (target.IcIndex, target.DiffIndex))
                    .ToHashSet()
                : null;

        return new NotchDisplayProjection(
            ToRegularRatioText: $"Undo NF (To Regular): {toRegularRatio:P2}",
            ToFullRatioText: $"To Full: {toFullRatio:P2}" + (isToFullEnabled ? " (enabled)" : " (disabled)"),
            CombinedRatioText: $"Combined ratio: {combinedValueText}",
            ToRegularValueText: toRegularRatio.ToString("P1", CultureInfo.InvariantCulture),
            ToFullValueText: $"{toFullRatio.ToString("P1", CultureInfo.InvariantCulture)} ({(isToFullEnabled ? "Enabled" : "Disabled")})",
            CombinedValueText: combinedValueText,
            HasCombinedOverflowRisk: effectiveTargetCoverageProjection.HasCombinedOverflowRisk,
            ToFullReasonShortText: BuildShortReasonText(fullReasonText),
            ToFullReasonFullText: fullReasonText,
            ToFullDiagnosticsText: FormatToFullDiagnosticsText(diagnostics),
            Stage3AreaText: stage3Area.HasValue
                ? $"Stage3 area: {stage3Area.Value:0.####} {PadInfoUnits.SquareMillimeter}"
                : "Stage3 area: -",
            TargetAllocationSummaryText: BuildTargetAllocationSummaryText(targets, emittedTargetKeys),
            TargetAllocationLines: BuildTargetAllocationLines(targetItems, emittedTargetKeys),
            TargetAllocationItems: BuildTargetAllocationItems(targetItems, emittedTargetKeys),
            OwnerSummaryText: BuildOwnerSummaryText(compactOwnerIds),
            OwnerCadIds: compactOwnerIds,
            DiagnosticEntries: parsedEntries);
    }

    private static string BuildTargetAllocationSummaryText(
        IReadOnlyList<NotchDisplayTargetInput>? targets,
        IReadOnlySet<(int IcIndex, int DiffIndex)>? emittedTargetKeys)
    {
        if (targets is null)
        {
            return "Targets: -";
        }

        if (targets.Count == 0)
        {
            return "Targets: 0";
        }

        var effectiveCount = targets.Count(target => IsEffectiveTarget(target, emittedTargetKeys));
        return $"Targets: {effectiveCount} effective / {targets.Count} total";
    }

    private static IReadOnlyList<string> BuildTargetAllocationLines(
        IReadOnlyList<NotchDisplayTargetInput> targets,
        IReadOnlySet<(int IcIndex, int DiffIndex)>? emittedTargetKeys)
    {
        if (targets.Count == 0)
        {
            return Array.Empty<string>();
        }

        var effective = targets
            .Where(target => IsEffectiveTarget(target, emittedTargetKeys))
            .OrderByDescending(static target => Math.Abs(target.Ratio))
            .ThenBy(static target => target.IcIndex)
            .ThenBy(static target => target.DiffIndex)
            .Take(10)
            .Select(static target =>
                $"IC{target.IcIndex + 1}/diff{target.DiffIndex}  " +
                $"A {target.EffectiveArea:0.####} {PadInfoUnits.SquareMillimeter}  " +
                $"R {target.RatioPercentRounded}%")
            .ToList();
        return effective;
    }

    private static IReadOnlyList<NotchDisplayTargetItem> BuildTargetAllocationItems(
        IReadOnlyList<NotchDisplayTargetInput> targets,
        IReadOnlySet<(int IcIndex, int DiffIndex)>? emittedTargetKeys)
    {
        if (targets.Count == 0)
        {
            return Array.Empty<NotchDisplayTargetItem>();
        }

        return targets
            .OrderBy(static target => target.IsAnchorDiff ? 0 : 1)
            .ThenByDescending(static target => Math.Abs(target.Ratio))
            .ThenBy(static target => target.IcIndex)
            .ThenBy(static target => target.DiffIndex)
            .SelectMany(target => BuildTargetItemsForDiff(target, emittedTargetKeys))
            .Take(20)
            .ToList();
    }

    private static IEnumerable<NotchDisplayTargetItem> BuildTargetItemsForDiff(
        NotchDisplayTargetInput target,
        IReadOnlySet<(int IcIndex, int DiffIndex)>? emittedTargetKeys)
    {
        var roleText = target.IsAnchorDiff
            ? "Self before To Full"
            : IsEffectiveTarget(target, emittedTargetKeys)
                ? "Target"
                : "Below gate";
        var ratioText = $"{target.RatioPercentRounded}%";
        var regularAreas = target.RegularAreas.Count > 0
            ? target.RegularAreas
            : target.RegularPadIds.Select(id => new NotchDisplayRegularAreaInput(id, target.EffectiveArea)).ToList();

        foreach (var regularArea in regularAreas)
        {
            var areaText = $"{regularArea.EffectiveArea:0.####} {PadInfoUnits.SquareMillimeter}";
            var targetText = $"REG {regularArea.RegularPadId}";
            var diffText = $"IC{target.IcIndex + 1}/diff{target.DiffIndex}";
            yield return new NotchDisplayTargetItem(
                RoleText: roleText,
                TargetText: targetText,
                DiffText: diffText,
                AreaText: areaText,
                RatioText: ratioText,
                RawText: $"{targetText} · {diffText} · {areaText} · {ratioText}",
                RegularPadIds: new[] { regularArea.RegularPadId });
        }
    }

    private static bool IsEffectiveTarget(
        NotchDisplayTargetInput target,
        IReadOnlySet<(int IcIndex, int DiffIndex)>? emittedTargetKeys)
    {
        return emittedTargetKeys?.Contains((target.IcIndex, target.DiffIndex)) ??
               (target.PassesStrictThreshold && !target.IsAnchorDiff);
    }

    private static string BuildOwnerSummaryText(int[] ownerCadIds)
    {
        if (ownerCadIds.Length == 0)
        {
            return string.Empty;
        }

        var preview = ownerCadIds
            .Take(4)
            .Select(static id => $"CAD {id}")
            .ToList();
        var suffix = ownerCadIds.Length > preview.Count
            ? $" (+{ownerCadIds.Length - preview.Count})"
            : string.Empty;
        return $"Owners {ownerCadIds.Length}: {string.Join(", ", preview)}{suffix}";
    }

    private static string BuildCombinedRatioValueText(double combinedRatio, bool hasOverflowRisk)
    {
        var text = combinedRatio.ToString("P2", CultureInfo.InvariantCulture);
        return hasOverflowRisk
            ? $"{text} (overflow risk >255%)"
            : text;
    }

    private static string BuildToFullReasonText(
        IReadOnlyList<NotchToFullDiagnosticEntry> parsedEntries,
        bool preferApplied)
    {
        if (parsedEntries.Count == 0)
        {
            return string.Empty;
        }

        var selected = preferApplied
            ? parsedEntries.FirstOrDefault(static entry => entry.IsToFull)
            : parsedEntries.FirstOrDefault(static entry => !entry.IsToFull);
        if (selected == default)
        {
            selected = parsedEntries[0];
        }

        return NotchToFullRuleText.BuildReasonWithCode(
            selected.RuleCode,
            selected.IsToFull,
            selected.IsBoundary);
    }

    private static string BuildShortReasonText(string fullReasonText)
    {
        if (string.IsNullOrWhiteSpace(fullReasonText))
        {
            return string.Empty;
        }

        var reason = fullReasonText.Trim();
        var codeStart = reason.IndexOf(" (", StringComparison.Ordinal);
        if (codeStart > 0)
        {
            reason = reason[..codeStart];
        }

        return reason;
    }

    private static IReadOnlyList<NotchToFullDiagnosticEntry> ParseDiagnosticEntries(string? diagnostics)
    {
        if (string.IsNullOrWhiteSpace(diagnostics))
        {
            return Array.Empty<NotchToFullDiagnosticEntry>();
        }

        var entries = new List<NotchToFullDiagnosticEntry>();
        var lines = diagnostics
            .Split(DiagnosticLineSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (TryParseDiagnosticLine(line, out var parsed))
            {
                entries.Add(parsed);
            }
        }

        return entries;
    }

    private static string FormatToFullDiagnosticsText(string? diagnostics)
    {
        if (string.IsNullOrWhiteSpace(diagnostics))
        {
            return string.Empty;
        }

        var lines = diagnostics
            .Split(DiagnosticLineSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.StartsWith('+'))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine().AppendLine();
                }

                builder.Append(line);
                continue;
            }

            if (!TryParseDiagnosticLine(line, out var parsed))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine().AppendLine();
                }

                builder.Append(line);
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            var ownersText = parsed.OwnerCadPadIds.Count == 0
                ? "-"
                : BuildCompactList(parsed.OwnerCadPadIds);

            builder.Append("IC")
                .Append(parsed.IcIndex)
                .Append("/diff")
                .Append(parsed.DiffIndex)
                .Append("  REG")
                .Append(parsed.RegularPadId)
                .AppendLine()
                .Append("Area  ov=")
                .Append(parsed.OverlapArea.ToString("0.######", CultureInfo.InvariantCulture))
                .Append(", src=")
                .Append(parsed.SourceArea.ToString("0.######", CultureInfo.InvariantCulture))
                .Append(", blk=")
                .Append(parsed.BlockedArea.ToString("0.######", CultureInfo.InvariantCulture))
                .Append(", reach=")
                .Append(parsed.ReachableArea.ToString("0.######", CultureInfo.InvariantCulture))
                .AppendLine()
                .Append("Owners ")
                .Append(parsed.OwnerCadPadIds.Count)
                .Append(" (")
                .Append(ownersText)
                .AppendLine(")")
                .Append("Share  ")
                .AppendLine(string.IsNullOrWhiteSpace(parsed.OwnerShareText) ? "-" : parsed.OwnerShareText)
                .Append("Flags  boundary=")
                .Append(parsed.IsBoundary ? "Y" : "N")
                .Append(", candidate=")
                .Append(parsed.IsCandidate ? "Y" : "N")
                .Append(", tofull=")
                .Append(parsed.IsToFull ? "Y" : "N")
                .Append(", blocker=")
                .Append(parsed.BlockerCount)
                .AppendLine()
                .Append("Rule   ")
                .Append(NotchToFullRuleText.BuildReasonWithCode(
                    parsed.RuleCode,
                    parsed.IsToFull,
                    parsed.IsBoundary));
        }

        return builder.ToString();
    }

    private static string BuildCompactList(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return "-";
        }

        if (values.Count <= 3)
        {
            return string.Join(", ", values);
        }

        return $"{values[0]}, {values[1]}, {values[2]} (+{values.Count - 3})";
    }

    private static bool TryParseDiagnosticLine(string line, out NotchToFullDiagnosticEntry parsed)
    {
        const string pattern =
            @"IC(?<ic>\d+)\/diff(?<diff>-?\d+)\s+reg(?<reg>\d+)\s*:\s*" +
            @"ov=(?<ov>[-\d.]+),\s*" +
            @"src=(?<src>[-\d.]+),\s*" +
            @"blk=(?<blk>[-\d.]+),\s*" +
            @"reach=(?<reach>[-\d.]+),\s*" +
            @"blocker=(?<blocker>\d+)," +
            @"(?:\s*owners=(?<owners>[^,]+),)?" +
            @"(?:\s*ownerShare=(?<ownershare>[^,]+),)?" +
            @"\s*boundary=(?<boundary>[YN])," +
            @"(?:\s*candidate=(?<candidate>[YN]),)?" +
            @"\s*tofull=(?<tofull>[YN])" +
            @"(?:\s*\((?<rule>[A-Z_]+)\))?";
        var match = Regex.Match(line, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            parsed = default;
            return false;
        }

        if (!int.TryParse(match.Groups["ic"].Value, out var icIndex) ||
            !int.TryParse(match.Groups["diff"].Value, out var diffIndex) ||
            !int.TryParse(match.Groups["reg"].Value, out var regularPadId) ||
            !double.TryParse(match.Groups["ov"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var overlapArea) ||
            !double.TryParse(match.Groups["src"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sourceArea) ||
            !double.TryParse(match.Groups["blk"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var blockedArea) ||
            !double.TryParse(match.Groups["reach"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var reachableArea) ||
            !int.TryParse(match.Groups["blocker"].Value, out var blockerCount))
        {
            parsed = default;
            return false;
        }

        var owners = new List<int>();
        var ownersToken = match.Groups["owners"].Success
            ? match.Groups["owners"].Value
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(ownersToken) && !string.Equals(ownersToken.Trim(), "-", StringComparison.Ordinal))
        {
            foreach (var token in ownersToken.Split(DiagnosticOwnerSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(token, out var ownerId))
                {
                    owners.Add(ownerId);
                }
            }
        }

        var ownerShareText = match.Groups["ownershare"].Success
            ? match.Groups["ownershare"].Value.Trim()
            : string.Empty;

        parsed = new NotchToFullDiagnosticEntry(
            IcIndex: icIndex,
            DiffIndex: diffIndex,
            RegularPadId: regularPadId,
            OverlapArea: overlapArea,
            SourceArea: sourceArea,
            BlockedArea: blockedArea,
            ReachableArea: reachableArea,
            BlockerCount: blockerCount,
            OwnerCadPadIds: owners,
            OwnerShareText: ownerShareText,
            IsBoundary: string.Equals(match.Groups["boundary"].Value, "Y", StringComparison.OrdinalIgnoreCase),
            IsCandidate: !match.Groups["candidate"].Success ||
                         string.Equals(match.Groups["candidate"].Value, "Y", StringComparison.OrdinalIgnoreCase),
            IsToFull: string.Equals(match.Groups["tofull"].Value, "Y", StringComparison.OrdinalIgnoreCase),
            RuleCode: match.Groups["rule"].Success ? match.Groups["rule"].Value : "UNKNOWN_RULE");
        return true;
    }
}

internal sealed record NotchDisplayProjection(
    string ToRegularRatioText,
    string ToFullRatioText,
    string CombinedRatioText,
    string ToRegularValueText,
    string ToFullValueText,
    string CombinedValueText,
    bool HasCombinedOverflowRisk,
    string ToFullReasonShortText,
    string ToFullReasonFullText,
    string ToFullDiagnosticsText,
    string Stage3AreaText,
    string TargetAllocationSummaryText,
    IReadOnlyList<string> TargetAllocationLines,
    IReadOnlyList<NotchDisplayTargetItem> TargetAllocationItems,
    string OwnerSummaryText,
    IReadOnlyList<int> OwnerCadIds,
    IReadOnlyList<NotchToFullDiagnosticEntry> DiagnosticEntries);

internal sealed record NotchDisplayTargetInput(
    int IcIndex,
    int DiffIndex,
    double EffectiveArea,
    double Ratio,
    int RatioPercentRounded,
    bool PassesStrictThreshold,
    bool IsAnchorDiff,
    int ToFullAppliedRegularCount,
    int RegularCount,
    IReadOnlyList<NotchDisplayRegularAreaInput> RegularAreas,
    IReadOnlyList<int> RegularPadIds);

internal sealed record NotchDisplayRegularAreaInput(
    int RegularPadId,
    double EffectiveArea);

internal sealed record NotchDisplayTargetItem(
    string RoleText,
    string TargetText,
    string DiffText,
    string AreaText,
    string RatioText,
    string RawText,
    IReadOnlyList<int> RegularPadIds);

internal readonly record struct NotchToFullDiagnosticEntry(
    int IcIndex,
    int DiffIndex,
    int RegularPadId,
    double OverlapArea,
    double SourceArea,
    double BlockedArea,
    double ReachableArea,
    int BlockerCount,
    IReadOnlyList<int> OwnerCadPadIds,
    string OwnerShareText,
    bool IsBoundary,
    bool IsCandidate,
    bool IsToFull,
    string RuleCode);
