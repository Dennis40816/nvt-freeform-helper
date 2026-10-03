using System.Globalization;
using System.Text;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal static class NotchExportSelectionProjectionBuilder
{
    public static NotchExportSelectionProjectionSnapshot Build(
        IReadOnlyList<NotchExportRowItemViewModel> allRows,
        NotchExportSelectionFilterState filterState,
        IReadOnlyList<int> workspaceCadIds,
        IReadOnlyList<int> workspaceRegularIndices)
    {
        ArgumentNullException.ThrowIfNull(allRows);

        var visibleRows = BuildScopedRows(allRows, filterState);
        return new NotchExportSelectionProjectionSnapshot(
            visibleRows,
            BuildGroupSnapshots(visibleRows),
            BuildWorkspaceLinkedRows(visibleRows, workspaceCadIds, workspaceRegularIndices),
            BuildFilterViewState(filterState, visibleRows.Count));
    }

    public static IReadOnlyList<string> BuildColumnFilterValues(
        IReadOnlyList<NotchExportRowItemViewModel> allRows,
        NotchExportSelectionFilterState filterState,
        NotchExportColumnFilterField field)
    {
        ArgumentNullException.ThrowIfNull(allRows);

        return BuildScopedRows(allRows, filterState, field)
            .Select(row => GetColumnFilterValue(row, field))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<NotchExportRowItemViewModel> BuildWorkspaceLinkedRows(
        IEnumerable<NotchExportRowItemViewModel> visibleRows,
        IReadOnlyList<int> workspaceCadIds,
        IReadOnlyList<int> workspaceRegularIndices)
    {
        ArgumentNullException.ThrowIfNull(visibleRows);

        var cadIds = workspaceCadIds ?? Array.Empty<int>();
        var regularIndices = workspaceRegularIndices ?? Array.Empty<int>();
        if (cadIds.Count == 0 && regularIndices.Count == 0)
        {
            return Array.Empty<NotchExportRowItemViewModel>();
        }

        var cadIdSet = cadIds.Count > 0
            ? cadIds.ToHashSet()
            : null;
        var regularIndexSet = regularIndices.Count > 0
            ? regularIndices.ToHashSet()
            : null;

        return visibleRows
            .Where(row => IsWorkspaceLinked(row, cadIdSet, regularIndexSet))
            .OrderBy(row => row.RowIndex)
            .ToList();
    }

    private static List<NotchExportRowItemViewModel> BuildScopedRows(
        IReadOnlyList<NotchExportRowItemViewModel> allRows,
        NotchExportSelectionFilterState filterState,
        NotchExportColumnFilterField? excludeColumnFilterField = null)
    {
        return allRows
            .Where(row => MatchesSelectedVersion(row, filterState.SelectedVersion))
            .Where(row => MatchesDisplayModeFilter(row, filterState.RowDisplayMode))
            .Where(row => MatchesSelectedOnlyFilter(row, filterState.ShowSelectedOnly))
            .Where(row => MatchesSearchQuery(row, filterState.SearchKeyword))
            .Where(row => MatchesColumnValueFilters(row, filterState.ColumnFilters, excludeColumnFilterField))
            .OrderBy(row => BuildSortKey(row, filterState.SortMode))
            .ThenBy(row => row.RowIndex)
            .ToList();
    }

    private static List<NotchExportSelectionIcGroupProjection> BuildGroupSnapshots(
        IReadOnlyList<NotchExportRowItemViewModel> visibleRows)
    {
        return visibleRows
            .GroupBy(row => row.Row.IcIndex)
            .OrderBy(group => group.Key)
            .Select(group => new NotchExportSelectionIcGroupProjection(group.Key, group.ToList()))
            .ToList();
    }

    private static NotchExportSelectionFilterViewState BuildFilterViewState(
        NotchExportSelectionFilterState filterState,
        int visibleCount)
    {
        var trimmedSearchKeyword = filterState.SearchKeyword?.Trim() ?? string.Empty;
        var hasSearchKeyword = !string.IsNullOrWhiteSpace(trimmedSearchKeyword);
        var hasActiveColumnFilters = filterState.ColumnFilters.Count > 0;
        var activeColumnFilterSummaryText = BuildActiveColumnFilterSummaryText(filterState.ColumnFilters);
        var hasModeFilterChip = filterState.RowDisplayMode != NotchExportRowDisplayMode.AllRows;
        var hasSelectionFilterChip = filterState.ShowSelectedOnly;
        var hasHeaderFilterChip = hasActiveColumnFilters;
        var hasResettableViewFilters =
            hasSearchKeyword ||
            hasSelectionFilterChip ||
            hasActiveColumnFilters ||
            hasModeFilterChip;
        var selectedScopeText = filterState.ShowSelectedOnly ? "selected-only" : "all";
        var headerScopeText = hasActiveColumnFilters ? activeColumnFilterSummaryText : "None";

        return new NotchExportSelectionFilterViewState(
            HasSearchKeyword: hasSearchKeyword,
            HasActiveColumnFilters: hasActiveColumnFilters,
            IsRowColumnFilterActive: filterState.ColumnFilters.ContainsKey(NotchExportColumnFilterField.RowNumber),
            IsIcColumnFilterActive: filterState.ColumnFilters.ContainsKey(NotchExportColumnFilterField.Ic),
            IsDiffColumnFilterActive: filterState.ColumnFilters.ContainsKey(NotchExportColumnFilterField.Diff),
            IsMatchColumnFilterActive: filterState.ColumnFilters.ContainsKey(NotchExportColumnFilterField.Match),
            IsMappingColumnFilterActive: filterState.ColumnFilters.ContainsKey(NotchExportColumnFilterField.Mapping),
            IsStatusColumnFilterActive: filterState.ColumnFilters.ContainsKey(NotchExportColumnFilterField.Status),
            HasSearchFilterChip: hasSearchKeyword,
            SearchFilterChipText: hasSearchKeyword ? $"Search: {trimmedSearchKeyword}" : string.Empty,
            HasModeFilterChip: hasModeFilterChip,
            ModeFilterChipText: $"Mode: {filterState.RowDisplayModeLabel}",
            HasSelectionFilterChip: hasSelectionFilterChip,
            SelectionFilterChipText: "Selection: Selected only",
            HasHeaderFilterChip: hasHeaderFilterChip,
            HeaderFilterChipText: $"Header: {activeColumnFilterSummaryText}",
            HasNoActiveFilterChips: !hasSearchKeyword && !hasModeFilterChip && !hasSelectionFilterChip && !hasHeaderFilterChip,
            HasResettableViewFilters: hasResettableViewFilters,
            ActiveColumnFilterSummaryText: activeColumnFilterSummaryText,
            CurrentSearchScopeText:
                $"Active filter: text={(hasSearchKeyword ? trimmedSearchKeyword : "*")} | " +
                $"mode={filterState.RowDisplayModeLabel} | " +
                $"selected={selectedScopeText} | " +
                $"header={headerScopeText} | " +
                $"shown={visibleCount}.",
            VisibleRowsSummaryText: $"Showing {visibleCount} rows");
    }

    private static bool MatchesSelectedVersion(
        NotchExportRowItemViewModel row,
        NotchAlgorithmVersion? selectedVersion)
    {
        return !selectedVersion.HasValue || row.Row.Version == selectedVersion.Value;
    }

    private static bool MatchesDisplayModeFilter(
        NotchExportRowItemViewModel row,
        NotchExportRowDisplayMode rowDisplayMode)
    {
        return rowDisplayMode switch
        {
            NotchExportRowDisplayMode.TransferOnly => row.IsTransferRow,
            NotchExportRowDisplayMode.AllRows => true,
            NotchExportRowDisplayMode.LinkedOnly => row.IsStatusLinked,
            NotchExportRowDisplayMode.WarningOnly => row.IsStatusWarning,
            NotchExportRowDisplayMode.NoCadOnly => row.IsStatusNoCad,
            NotchExportRowDisplayMode.LegacyOnly => row.IsStatusLegacy,
            _ => true,
        };
    }

    private static bool MatchesSelectedOnlyFilter(NotchExportRowItemViewModel row, bool showSelectedOnly)
    {
        return !showSelectedOnly || row.IsSelected;
    }

    private static int BuildSortKey(
        NotchExportRowItemViewModel row,
        NotchExportSortMode sortMode)
    {
        return sortMode switch
        {
            NotchExportSortMode.RowId => row.RowIndex,
            NotchExportSortMode.DiffAscending => row.Row.DiffIndex,
            NotchExportSortMode.MatchDescending => -ExtractMatchSortValue(row),
            NotchExportSortMode.IcThenDiff => (row.Row.IcIndex * 100000) + row.Row.DiffIndex,
            _ => row.RowIndex,
        };
    }

    private static int ExtractMatchSortValue(NotchExportRowItemViewModel row)
    {
        if (row.Row.V22Node is NotchV22Node node)
        {
            return node.CombinePercent;
        }

        return 0;
    }

    private static bool MatchesSearchQuery(NotchExportRowItemViewModel row, string? searchKeyword)
    {
        if (string.IsNullOrWhiteSpace(searchKeyword))
        {
            return true;
        }

        var terms = SplitSearchTerms(searchKeyword);
        if (terms.Count == 0)
        {
            return true;
        }

        var tokenText = row.BuildSearchTokenText();
        foreach (var term in terms)
        {
            if (!TryParseFieldTerm(term, out var field, out var value))
            {
                if (!tokenText.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                continue;
            }

            if (!MatchesField(row, field, value))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> SplitSearchTerms(string query)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return terms;
        }

        var sb = new StringBuilder();
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

            sb.Append(ch);
        }

        Flush();
        return terms;

        void Flush()
        {
            if (sb.Length == 0)
            {
                return;
            }

            terms.Add(sb.ToString());
            sb.Clear();
        }
    }

    private static bool TryParseFieldTerm(string term, out string field, out string value)
    {
        field = string.Empty;
        value = string.Empty;
        var index = term.IndexOf('=');
        if (index <= 0 || index >= term.Length - 1)
        {
            return false;
        }

        field = term[..index].Trim().ToLowerInvariant();
        value = term[(index + 1)..].Trim().Trim('"');
        return !string.IsNullOrWhiteSpace(field) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool MatchesField(NotchExportRowItemViewModel row, string field, string value)
    {
        return field switch
        {
            "#" or "row" or "id" => MatchesNumber(row.RowIndex + 1, value),
            "ic" => MatchesNumber(row.Row.IcIndex + 1, value),
            "diff" or "d" => MatchesNumber(row.Row.DiffIndex, value),
            "reg" or "regular" or "r" => MatchesNumber(row.Row.RegularPadIndex, value),
            "cad" or "c" => MatchesCad(row, value),
            "status" or "st" => MatchesStatus(row, value),
            "v" or "ver" or "version" => Contains(row.VersionText, value),
            "a" or "anchor" => Contains(row.PayloadAnchorText, value),
            "match" or "m" or "combine" => Contains(row.MatchSummaryText, value) || Contains(row.PayloadCombineText, value),
            "t1" or "target1" => Contains(row.PayloadTarget1Text, value),
            "t2" or "target2" => Contains(row.PayloadTarget2Text, value),
            "text" or "comment" or "note" => Contains(row.CommentText, value),
            _ => Contains(row.BuildSearchTokenText(), value),
        };
    }

    private static bool MatchesCad(NotchExportRowItemViewModel row, string value)
    {
        var normalized = value.Trim();
        if (normalized.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("-", StringComparison.OrdinalIgnoreCase))
        {
            return !row.IsCadLinked;
        }

        if (!row.Row.CadPadId.HasValue)
        {
            return false;
        }

        return MatchesNumber(row.Row.CadPadId.Value, normalized);
    }

    private static bool MatchesNumber(int actual, string expected)
    {
        if (int.TryParse(expected, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return actual == number;
        }

        return actual.ToString(CultureInfo.InvariantCulture)
            .Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string? text, string keyword)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        return text.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStatus(NotchExportRowItemViewModel row, string expected)
    {
        var query = NormalizeToken(expected);
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return NormalizeToken(row.StatusText).Contains(query, StringComparison.Ordinal) ||
               NormalizeToken(row.MappingStatusText).Contains(query, StringComparison.Ordinal);
    }

    private static bool MatchesColumnValueFilters(
        NotchExportRowItemViewModel row,
        IReadOnlyDictionary<NotchExportColumnFilterField, HashSet<string>>? columnFilters,
        NotchExportColumnFilterField? excludeField = null)
    {
        if (columnFilters is null || columnFilters.Count == 0)
        {
            return true;
        }

        foreach (var (field, values) in columnFilters)
        {
            if (excludeField.HasValue && field == excludeField.Value)
            {
                continue;
            }

            if (values.Count == 0)
            {
                return false;
            }

            var value = GetColumnFilterValue(row, field);
            if (!values.Contains(value))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetColumnFilterValue(NotchExportRowItemViewModel row, NotchExportColumnFilterField field)
    {
        return field switch
        {
            NotchExportColumnFilterField.RowNumber => row.RowNumberText,
            NotchExportColumnFilterField.Ic => row.IcText,
            NotchExportColumnFilterField.Diff => row.DiffText,
            NotchExportColumnFilterField.Match => row.MatchSummaryText,
            NotchExportColumnFilterField.Mapping => row.MappingText,
            NotchExportColumnFilterField.Status => row.StatusText,
            _ => string.Empty,
        };
    }

    private static bool IsWorkspaceLinked(
        NotchExportRowItemViewModel row,
        HashSet<int>? workspaceCadIds,
        HashSet<int>? workspaceRegularIndices)
    {
        if (workspaceCadIds is not null &&
            row.Row.CadPadId.HasValue &&
            workspaceCadIds.Contains(row.Row.CadPadId.Value))
        {
            return true;
        }

        return workspaceRegularIndices is not null &&
               workspaceRegularIndices.Contains(row.Row.RegularPadIndex);
    }

    private static string BuildActiveColumnFilterSummaryText(
        IReadOnlyDictionary<NotchExportColumnFilterField, HashSet<string>> columnFilters)
    {
        if (columnFilters.Count == 0)
        {
            return "None";
        }

        return string.Join(
            ", ",
            columnFilters
                .OrderBy(static pair => pair.Key)
                .Select(static pair => $"{ToColumnFilterLabel(pair.Key)}({pair.Value.Count})"));
    }

    private static string ToColumnFilterLabel(NotchExportColumnFilterField field)
    {
        return field switch
        {
            NotchExportColumnFilterField.RowNumber => "#",
            NotchExportColumnFilterField.Ic => "IC",
            NotchExportColumnFilterField.Diff => "Diff",
            NotchExportColumnFilterField.Match => "Match",
            NotchExportColumnFilterField.Mapping => "REG->CAD",
            NotchExportColumnFilterField.Status => "Status",
            _ => "Field",
        };
    }

    private static string NormalizeToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return sb.ToString();
    }
}

internal readonly record struct NotchExportSelectionFilterState(
    NotchAlgorithmVersion? SelectedVersion,
    NotchExportRowDisplayMode RowDisplayMode,
    string RowDisplayModeLabel,
    NotchExportSortMode SortMode,
    string SearchKeyword,
    bool ShowSelectedOnly,
    IReadOnlyDictionary<NotchExportColumnFilterField, HashSet<string>> ColumnFilters);

internal sealed record NotchExportSelectionProjectionSnapshot(
    IReadOnlyList<NotchExportRowItemViewModel> VisibleRows,
    IReadOnlyList<NotchExportSelectionIcGroupProjection> IcGroups,
    IReadOnlyList<NotchExportRowItemViewModel> WorkspaceLinkedRows,
    NotchExportSelectionFilterViewState FilterViewState);

internal sealed record NotchExportSelectionIcGroupProjection(
    int IcIndex,
    IReadOnlyList<NotchExportRowItemViewModel> Rows);

internal sealed record NotchExportSelectionFilterViewState(
    bool HasSearchKeyword,
    bool HasActiveColumnFilters,
    bool IsRowColumnFilterActive,
    bool IsIcColumnFilterActive,
    bool IsDiffColumnFilterActive,
    bool IsMatchColumnFilterActive,
    bool IsMappingColumnFilterActive,
    bool IsStatusColumnFilterActive,
    bool HasSearchFilterChip,
    string SearchFilterChipText,
    bool HasModeFilterChip,
    string ModeFilterChipText,
    bool HasSelectionFilterChip,
    string SelectionFilterChipText,
    bool HasHeaderFilterChip,
    string HeaderFilterChipText,
    bool HasNoActiveFilterChips,
    bool HasResettableViewFilters,
    string ActiveColumnFilterSummaryText,
    string CurrentSearchScopeText,
    string VisibleRowsSummaryText);
