using System.Text;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

public readonly record struct NotchExportSelectionQuickFilterAction(
    bool ShouldUpdateSearchKeyword,
    string SearchKeyword,
    bool ShouldUpdateRowDisplayMode,
    NotchExportRowDisplayMode RowDisplayMode);

public static class NotchExportSelectionQuickFilterService
{
    private static readonly NotchExportRowDisplayMode[] StatusFilterCycle =
    {
        NotchExportRowDisplayMode.TransferOnly,
        NotchExportRowDisplayMode.LinkedOnly,
        NotchExportRowDisplayMode.WarningOnly,
        NotchExportRowDisplayMode.NoCadOnly,
        NotchExportRowDisplayMode.LegacyOnly,
        NotchExportRowDisplayMode.AllRows,
    };

    public static NotchExportSelectionQuickFilterAction ResolveAction(
        string? target,
        string? currentSearchKeyword,
        NotchExportRowDisplayMode currentRowDisplayMode)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return default;
        }

        switch (target.Trim().ToLowerInvariant())
        {
            case "row":
                return BuildSearchKeywordAction(currentSearchKeyword, "row=", currentRowDisplayMode);
            case "ic":
                return BuildSearchKeywordAction(currentSearchKeyword, "ic=", currentRowDisplayMode);
            case "diff":
                return BuildSearchKeywordAction(currentSearchKeyword, "diff=", currentRowDisplayMode);
            case "match":
                return BuildSearchKeywordAction(currentSearchKeyword, "match=", currentRowDisplayMode);
            case "map":
                return BuildSearchKeywordAction(currentSearchKeyword, "reg=", currentRowDisplayMode);
            case "status":
                return new NotchExportSelectionQuickFilterAction(
                    ShouldUpdateSearchKeyword: false,
                    SearchKeyword: currentSearchKeyword ?? string.Empty,
                    ShouldUpdateRowDisplayMode: true,
                    RowDisplayMode: ResolveNextStatusMode(currentRowDisplayMode));
            default:
                return default;
        }
    }

    private static NotchExportSelectionQuickFilterAction BuildSearchKeywordAction(
        string? currentSearchKeyword,
        string token,
        NotchExportRowDisplayMode currentRowDisplayMode)
    {
        return new NotchExportSelectionQuickFilterAction(
            ShouldUpdateSearchKeyword: true,
            SearchKeyword: ToggleSearchToken(currentSearchKeyword, token),
            ShouldUpdateRowDisplayMode: false,
            RowDisplayMode: currentRowDisplayMode);
    }

    private static string ToggleSearchToken(string? currentSearchKeyword, string token)
    {
        var trimmed = (currentSearchKeyword ?? string.Empty).Trim();
        if (trimmed.Equals(token, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return token;
        }

        var terms = SplitSearchTerms(trimmed);
        if (terms.Any(term => term.Equals(token, StringComparison.OrdinalIgnoreCase)))
        {
            terms = terms.Where(term => !term.Equals(token, StringComparison.OrdinalIgnoreCase)).ToList();
            return string.Join(' ', terms);
        }

        return $"{trimmed} {token}".Trim();
    }

    private static List<string> SplitSearchTerms(string query)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return terms;
        }

        var current = new StringBuilder();
        var inQuote = false;
        foreach (var ch in query.Trim())
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                continue;
            }

            if (!inQuote && char.IsWhiteSpace(ch))
            {
                Flush();
                continue;
            }

            current.Append(ch);
        }

        Flush();
        return terms;

        void Flush()
        {
            if (current.Length == 0)
            {
                return;
            }

            terms.Add(current.ToString());
            current.Clear();
        }
    }

    private static NotchExportRowDisplayMode ResolveNextStatusMode(NotchExportRowDisplayMode currentRowDisplayMode)
    {
        var index = Array.IndexOf(StatusFilterCycle, currentRowDisplayMode);
        if (index < 0)
        {
            return StatusFilterCycle[0];
        }

        return StatusFilterCycle[(index + 1) % StatusFilterCycle.Length];
    }
}
