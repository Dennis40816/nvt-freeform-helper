using System.Text.RegularExpressions;

namespace FreeformHelper.UI.Services;

internal static class NotchExportErrorFormatter
{
    private const string CombineOverflowToken = "Combine ratio exceeds 255%";
    private const string CombineOverflowGuidance =
        "Suggested action: lower To Regular/To Full or disable To Full EN, then export again.";

    public static string Format(string exportKind, Exception ex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportKind);
        ArgumentNullException.ThrowIfNull(ex);

        if (TryFindCombineOverflow(ex, out var detail))
        {
            return $"{exportKind} export failed: combine ratio overflow (>255%). {detail} {CombineOverflowGuidance}";
        }

        return $"{exportKind} export failed: {ex.Message}";
    }

    private static bool TryFindCombineOverflow(Exception ex, out string detail)
    {
        for (var cursor = ex; cursor is not null; cursor = cursor.InnerException)
        {
            if (cursor.Message.Contains(CombineOverflowToken, StringComparison.OrdinalIgnoreCase))
            {
                if (TryFormatGeneratorOverflowDetail(cursor.Message, out detail) ||
                    TryFormatExporterOverflowDetail(cursor.Message, out detail))
                {
                    return true;
                }

                detail = cursor.Message.Trim();
                return true;
            }
        }

        detail = string.Empty;
        return false;
    }

    private static bool TryFormatGeneratorOverflowDetail(string message, out string detail)
    {
        var match = Regex.Match(
            message,
            @"Combine ratio exceeds 255% at IC(?<ic>\d+)\/diff(?<diff>-?\d+)\s+\(CAD\s+(?<cad>\d+)\):\s*toRegular=(?<toreg>[-\d.]+)%\,\s*toFull=(?<tofull>[-\d.]+)%\,\s*raw=(?<raw>[-\d.]+)%\.?",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            detail = string.Empty;
            return false;
        }

        detail =
            $"IC{match.Groups["ic"].Value}/diff{match.Groups["diff"].Value}, " +
            $"CAD {match.Groups["cad"].Value}, " +
            $"To Regular {match.Groups["toreg"].Value}%, " +
            $"To Full {match.Groups["tofull"].Value}%, " +
            $"raw {match.Groups["raw"].Value}%.";
        return true;
    }

    private static bool TryFormatExporterOverflowDetail(string message, out string detail)
    {
        var match = Regex.Match(
            message,
            @"v2\.2\s+Combine ratio exceeds 255% at IC(?<ic>\d+)\s+diff(?<diff>-?\d+):\s*(?<raw>[-\d.]+)%\.?",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            detail = string.Empty;
            return false;
        }

        detail = $"IC{match.Groups["ic"].Value}/diff{match.Groups["diff"].Value}, raw {match.Groups["raw"].Value}%.";
        return true;
    }
}
