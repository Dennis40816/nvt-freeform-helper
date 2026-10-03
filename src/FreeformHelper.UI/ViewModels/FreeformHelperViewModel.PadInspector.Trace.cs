using System.Globalization;
using System.Text.RegularExpressions;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private static List<PadInspectorRuleTraceEntry> BuildCadRuleTrace(
        int? dxfIndex,
        string cadOutputFwDiffAssignmentModeText,
        IReadOnlyList<PadInspectorMatchedRegularSnapshot> matchedRegularDetails,
        PadInspectorNotchSnapshot? notch,
        NotchCadRowEligibility notchEligibility)
    {
        var notchEligibilityPending = string.Equals(
            notchEligibility.Reason,
            "Computing in background.",
            StringComparison.Ordinal);
        var trace = new List<PadInspectorRuleTraceEntry>
        {
            new(
                Rule: "Diff idx",
                Outcome: cadOutputFwDiffAssignmentModeText,
                Detail: dxfIndex.HasValue
                    ? $"Diff idx value: {dxfIndex.Value}"
                    : "Diff idx unresolved (-).")
        };

        if (matchedRegularDetails.Count == 0)
        {
            trace.Add(new(
                Rule: "Match",
                Outcome: "No matched regular pad",
                Detail: "Best overlap target: none."));
        }
        else
        {
            var best = matchedRegularDetails
                .OrderByDescending(detail => detail.CadCoverage)
                .ThenBy(detail => detail.RegularPadId)
                .First();
            var bestKey = best.IcIndex.HasValue && best.DiffIndex.HasValue
                ? $"IC{best.IcIndex.Value + 1}/diff{best.DiffIndex.Value}"
                : $"REG{best.RegularPadId}";
            trace.Add(new(
                Rule: "Match",
                Outcome: $"Matched regular pad count: {matchedRegularDetails.Count}",
                Detail: $"Best target: {bestKey}. CAD coverage: {best.CadCoverage:P1}. Regular coverage: {best.RegularCoverage:P1}."));
        }

        var notchOutcome = notchEligibilityPending
            ? "Computing..."
            : notchEligibility.HasRows
                ? $"Eligible ({string.Join(", ", notchEligibility.EligibleVersions.Select(v => v.ToDisplayLabel()))})"
                : "No row";
        var notchDetail = notchEligibilityPending
            ? "Deferred notch-row eligibility is running."
            : notchEligibility.Reason;
        trace.Add(new(
            Rule: "Notch export",
            Outcome: notchOutcome,
            Detail: notchDetail));

        if (notch is null)
        {
            trace.Add(new(
                Rule: "Notch 2.2",
                Outcome: "Not available",
                Detail: "Grid is not ready."));
            return trace;
        }

        trace.Add(new(
            Rule: "To Regular",
            Outcome: notch.ToRegularRatio.ToString("P1", CultureInfo.InvariantCulture),
            Detail: "Undo NF ratio from overlap-area / regular-area sum."));
        trace.Add(new(
            Rule: "To Full",
            Outcome: notch.IsToFullEnabled ? "Enabled" : "Disabled",
            Detail: BuildToFullDetail(notch.IsToFullEnabled, notch.Diagnostics, notch.ToFullRatio)));
        return trace;
    }

    private static List<PadInspectorRuleTraceEntry> BuildRegularRuleTrace(
        RegularPad regularPad,
        List<PadInspectorMatchedCadSnapshot> matchedCadDetails,
        string diffSource,
        string freeformSource,
        string freeformSourceDetail)
    {
        var trace = new List<PadInspectorRuleTraceEntry>
        {
            new(
                Rule: "Identity",
                Outcome: $"IC{regularPad.IcIndex + 1}/diff{regularPad.DiffIndex}",
                Detail: $"R{regularPad.Row}  C{regularPad.Col}  REG{regularPad.RegularPadId}")
        };

        if (matchedCadDetails.Count == 0)
        {
            trace.Add(new(
                Rule: "Match",
                Outcome: "No matched CAD pad",
                Detail: "Best overlap target: none."));
        }
        else
        {
            var best = matchedCadDetails
                .OrderByDescending(detail => detail.CadCoverage)
                .ThenBy(detail => detail.CadPadId)
                .First();
            var bestKey = best.DxfIndex.HasValue
                ? $"D{best.DxfIndex.Value}"
                : $"CAD{best.CadPadId}";
            trace.Add(new(
                Rule: "Match",
                Outcome: $"Matched CAD pad count: {matchedCadDetails.Count}",
                Detail: $"Best target: {bestKey}. Regular coverage: {best.RegularCoverage:P1}. CAD coverage: {best.CadCoverage:P1}."));
        }

        trace.Add(new(
            Rule: "Diff source",
            Outcome: diffSource,
            Detail: string.Equals(diffSource, "Strict", StringComparison.OrdinalIgnoreCase)
                ? "Exact CAD Output FW Diff match: Yes."
                : "Exact CAD Output FW Diff match: No."));
        trace.Add(new(
            Rule: "Freeform",
            Outcome: regularPad.Freeform.ToString(),
            Detail: $"Source: {freeformSource}{Environment.NewLine}{freeformSourceDetail}"));
        return trace;
    }

    private static string BuildToFullDetail(bool isToFullEnabled, string? diagnostics, double toFullRatio)
    {
        var ratioText = toFullRatio.ToString("P1", CultureInfo.InvariantCulture);
        var switchText = isToFullEnabled ? "ON" : "OFF";
        if (string.IsNullOrWhiteSpace(diagnostics))
        {
            return $"To Full ratio: {ratioText}{Environment.NewLine}" +
                   $"Switch: {switchText}{Environment.NewLine}" +
                   "Diagnostics: unavailable.";
        }

        var firstLine = diagnostics
            .Split(ToFullDiagnosticsLineSeparators, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return $"To Full ratio: {ratioText}{Environment.NewLine}" +
                   $"Switch: {switchText}{Environment.NewLine}" +
                   "Diagnostics: unavailable.";
        }

        if (!TryParseToFullDiagnosticRow(firstLine, out var parsed))
        {
            return $"To Full ratio: {ratioText}{Environment.NewLine}" +
                   $"Switch: {switchText}{Environment.NewLine}" +
                   $"Raw: {firstLine}";
        }

        var ownersText = parsed.OwnerCadPadIds.Count == 0
            ? "-"
            : BuildCompactList(parsed.OwnerCadPadIds);

        return $"To Full ratio: {ratioText}{Environment.NewLine}" +
               $"Switch: {switchText}{Environment.NewLine}" +
               $"Target: IC {parsed.IcIndex} / Diff idx {parsed.DiffIndex} / REG {parsed.RegularPadId}{Environment.NewLine}" +
               $"Area: ov={parsed.OverlapArea:0.######}, src={parsed.SourceArea:0.######}, blk={parsed.BlockedArea:0.######}, reach={parsed.ReachableArea:0.######}{Environment.NewLine}" +
               $"Owners: {parsed.OwnerCadPadIds.Count} ({ownersText}){Environment.NewLine}" +
               $"Owner share: {(string.IsNullOrWhiteSpace(parsed.OwnerShareText) ? "-" : parsed.OwnerShareText)}{Environment.NewLine}" +
               $"Boundary: {ToYesNo(parsed.IsBoundary)}, Candidate: {ToYesNo(parsed.IsBoundaryCandidate)}, Blockers: {parsed.BlockerCount}{Environment.NewLine}" +
               $"Applied: {ToYesNo(parsed.IsToFullApplied)}{Environment.NewLine}" +
               $"Rule: {parsed.ReasonText}" +
               (string.IsNullOrWhiteSpace(parsed.ReasonCode) ? string.Empty : $" ({parsed.ReasonCode})");
    }

    private static bool TryParseToFullDiagnosticRow(string row, out ToFullDiagnosticRow parsed)
    {
        const string Pattern =
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
            @"(?:\s*\((?<reason>[A-Z_]+)\))?";
        var match = Regex.Match(row, Pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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
            foreach (var token in ownersToken.Split(ToFullDiagnosticsOwnerSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
        var isBoundary = string.Equals(match.Groups["boundary"].Value, "Y", StringComparison.OrdinalIgnoreCase);
        var isBoundaryCandidate = !match.Groups["candidate"].Success ||
                                  string.Equals(match.Groups["candidate"].Value, "Y", StringComparison.OrdinalIgnoreCase);
        var isToFullApplied = string.Equals(match.Groups["tofull"].Value, "Y", StringComparison.OrdinalIgnoreCase);
        var reasonCode = match.Groups["reason"].Success
            ? match.Groups["reason"].Value
            : string.Empty;

        parsed = new ToFullDiagnosticRow(
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
            IsBoundary: isBoundary,
            IsBoundaryCandidate: isBoundaryCandidate,
            IsToFullApplied: isToFullApplied,
            ReasonCode: reasonCode,
            ReasonText: ResolveToFullReasonText(reasonCode, isBoundary, isToFullApplied));
        return true;
    }

    private static string ResolveToFullReasonText(string reasonCode, bool isBoundary, bool isToFullApplied)
    {
        return NotchToFullRuleText.ResolveReasonText(
            reasonCode,
            isToFullApplied,
            isBoundary);
    }

    private static string ToYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static IReadOnlyList<PadInspectorRuleTraceEntry> BuildDeferredRuleTracePlaceholder()
    {
        return
        [
            new PadInspectorRuleTraceEntry(
                Rule: "Computing",
                Outcome: "Queued",
                Detail: "Detailed trace is loading in background.")
        ];
    }

    private readonly record struct ToFullDiagnosticRow(
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
        bool IsBoundaryCandidate,
        bool IsToFullApplied,
        string ReasonCode,
        string ReasonText);
}
