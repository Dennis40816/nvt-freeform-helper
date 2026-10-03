using System.Globalization;
using System.Text.RegularExpressions;

namespace FreeformHelper.UI.Services;

internal enum QuickFocusTargetKind
{
    Auto,
    Cad,
    Regular,
    Diff,
}

internal readonly record struct QuickFocusQuery(
    QuickFocusTargetKind TargetKind,
    int Value,
    int? IcIndex);

internal static partial class QuickFocusQueryParser
{
    public static bool TryParse(string? input, out QuickFocusQuery query, out string error)
    {
        query = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Quick focus: input a CAD id, REG id, or FW diff.";
            return false;
        }

        var tokens = TokenRegex()
            .Matches(input)
            .Select(static match => match.Value.ToLowerInvariant())
            .ToArray();
        if (tokens.Length == 0)
        {
            error = "Quick focus: input a CAD id, REG id, or FW diff.";
            return false;
        }

        var targetKind = QuickFocusTargetKind.Auto;
        int? value = null;
        int? icIndex = null;

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (TryParseInt(token, out var number))
            {
                value ??= number;
                continue;
            }

            if (IsIcAlias(token))
            {
                if (TryReadNextInt(tokens, index + 1, out var oneBasedIc))
                {
                    icIndex = Math.Max(0, oneBasedIc - 1);
                }

                continue;
            }

            if (TryResolveTargetKind(token, out var explicitKind))
            {
                targetKind = explicitKind;
                if (TryReadNextInt(tokens, index + 1, out var explicitValue))
                {
                    value = explicitValue;
                }
            }
        }

        if (!value.HasValue)
        {
            error = "Quick focus: missing numeric id. Use CAD 4809, REG 4616, or DIFF 72.";
            return false;
        }

        query = new QuickFocusQuery(targetKind, Math.Clamp(value.Value, 0, 2_000_000_000), icIndex);
        return true;
    }

    private static bool TryReadNextInt(string[] tokens, int startIndex, out int value)
    {
        for (var index = startIndex; index < tokens.Length; index++)
        {
            if (TryParseInt(tokens[index], out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryParseInt(string token, out int value)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsIcAlias(string token)
    {
        return token is "ic";
    }

    private static bool TryResolveTargetKind(string token, out QuickFocusTargetKind kind)
    {
        kind = token switch
        {
            "cad" or "c" => QuickFocusTargetKind.Cad,
            "reg" or "regular" or "r" => QuickFocusTargetKind.Regular,
            "diff" or "d" or "fw" or "fwidx" or "fwoutput" => QuickFocusTargetKind.Diff,
            _ => QuickFocusTargetKind.Auto,
        };

        return kind != QuickFocusTargetKind.Auto;
    }

    [GeneratedRegex("[A-Za-z]+|\\d+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
