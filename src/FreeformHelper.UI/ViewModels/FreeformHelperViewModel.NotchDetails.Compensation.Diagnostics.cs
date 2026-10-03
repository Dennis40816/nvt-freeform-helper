using System.Globalization;
using System.Text;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private static string? BuildToFullDiagnosticsText(NotchV22CompensationResult compensation)
    {
        if (compensation.RegularDebugInfos.Count == 0)
        {
            return null;
        }

        const int maxRows = 8;
        var builder = new StringBuilder();
        var rows = compensation.RegularDebugInfos
            .OrderBy(info => info.IcIndex)
            .ThenBy(info => info.DiffIndex)
            .ThenBy(info => info.RegularPadId)
            .Take(maxRows)
            .ToList();
        foreach (var info in rows)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("IC")
                .Append(info.IcIndex + 1)
                .Append("/diff")
                .Append(info.DiffIndex)
                .Append(" reg")
                .Append(info.RegularPadId)
                .Append(" : ov=")
                .Append(info.OverlapArea.ToString("0.######", CultureInfo.InvariantCulture))
                .Append(", src=")
                .Append(info.SourceArea.ToString("0.######", CultureInfo.InvariantCulture))
                .Append(", blk=")
                .Append(info.BlockedArea.ToString("0.######", CultureInfo.InvariantCulture))
                .Append(", reach=")
                .Append(info.ReachableArea.ToString("0.######", CultureInfo.InvariantCulture))
                .Append(", blocker=")
                .Append(info.BlockerCandidateCount)
                .Append(", owners=")
                .Append(BuildOwnerCadIdsText(info))
                .Append(", ownerCnt=")
                .Append(info.OwnerCadPadCount)
                .Append(", ownerShare=")
                .Append('-')
                .Append(", boundary=")
                .Append(info.IsBoundaryRegular ? "Y" : "N")
                .Append(", candidate=")
                .Append(info.IsToFullBoundaryCandidate ? "Y" : "N")
                .Append(", tofull=")
                .Append(info.IsToFullApplied ? "Y" : "N")
                .Append(" (")
                .Append(ResolveToFullRuleCode(info))
                .Append(')');
        }

        var hidden = compensation.RegularDebugInfos.Count - rows.Count;
        if (hidden > 0)
        {
            builder.AppendLine()
                .Append('+')
                .Append(hidden)
                .Append(" more regular(s)");
        }

        return builder.ToString();
    }

    private static string BuildOwnerCadIdsText(NotchV22RegularDebugInfo info)
    {
        if (info.OwnerCadPadIds.Count == 0)
        {
            return "-";
        }

        const int maxShown = 12;
        var display = info.OwnerCadPadIds.Take(maxShown);
        var text = string.Join(";", display);
        if (info.OwnerCadPadIds.Count > maxShown)
        {
            text += $";+{info.OwnerCadPadIds.Count - maxShown}";
        }

        return text;
    }

    private static IReadOnlyList<(int CadId, double RatioPercent)> ComputeOwnerInfosForRegular(
        RegularPad regular,
        List<CadPad> visibleCadPads,
        double strictOverlapRatio)
    {
        if (visibleCadPads.Count == 0)
        {
            return Array.Empty<(int CadId, double RatioPercent)>();
        }

        var threshold = Math.Max(1e-12, regular.Area * strictOverlapRatio);
        var ratios = visibleCadPads
            .Where(cad => cad.Bounds.Intersects(regular.Bounds))
            .Select(cad => new
            {
                CadId = cad.Id,
                Area = Polygon2.IntersectionAreaWithRect(cad.Polygon, regular.Bounds),
            })
            .Where(x => x.Area > threshold)
            .Select(x => (x.CadId, RatioPercent: (x.Area / Math.Max(regular.Area, 1e-12)) * 100.0))
            .OrderByDescending(x => x.RatioPercent)
            .ThenBy(x => x.CadId)
            .ToList();
        return ratios;
    }

    private static string ResolveToFullRuleCode(NotchV22RegularDebugInfo info)
    {
        return string.IsNullOrWhiteSpace(info.ToFullRuleCode)
            ? "UNKNOWN_RULE"
            : info.ToFullRuleCode;
    }

}
