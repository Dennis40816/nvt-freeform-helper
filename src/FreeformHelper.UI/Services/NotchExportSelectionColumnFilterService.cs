using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

public static class NotchExportSelectionColumnFilterService
{
    public static void ApplySelection(
        IDictionary<NotchExportColumnFilterField, HashSet<string>> columnFilters,
        NotchExportColumnFilterField field,
        IReadOnlyList<string>? selectedKeys,
        IReadOnlyList<string>? allKeys)
    {
        ArgumentNullException.ThrowIfNull(columnFilters);
        if (selectedKeys is null || allKeys is null || allKeys.Count == 0)
        {
            columnFilters.Remove(field);
            return;
        }

        if (selectedKeys.Count == allKeys.Count)
        {
            columnFilters.Remove(field);
            return;
        }

        columnFilters[field] = selectedKeys
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
    }

    public static bool TryParseField(string? columnKey, out NotchExportColumnFilterField field)
    {
        field = NotchExportColumnFilterField.RowNumber;
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            return false;
        }

        switch (columnKey.Trim().ToLowerInvariant())
        {
            case "row":
                field = NotchExportColumnFilterField.RowNumber;
                return true;
            case "ic":
                field = NotchExportColumnFilterField.Ic;
                return true;
            case "diff":
                field = NotchExportColumnFilterField.Diff;
                return true;
            case "match":
                field = NotchExportColumnFilterField.Match;
                return true;
            case "map":
                field = NotchExportColumnFilterField.Mapping;
                return true;
            case "status":
                field = NotchExportColumnFilterField.Status;
                return true;
            default:
                return false;
        }
    }

    public static string BuildDialogTitle(NotchExportColumnFilterField field)
    {
        return field switch
        {
            NotchExportColumnFilterField.RowNumber => "Filter #",
            NotchExportColumnFilterField.Ic => "Filter IC",
            NotchExportColumnFilterField.Diff => "Filter Diff",
            NotchExportColumnFilterField.Match => "Filter Match",
            NotchExportColumnFilterField.Mapping => "Filter REG -> CAD",
            NotchExportColumnFilterField.Status => "Filter Status",
            _ => "Column filter",
        };
    }
}
