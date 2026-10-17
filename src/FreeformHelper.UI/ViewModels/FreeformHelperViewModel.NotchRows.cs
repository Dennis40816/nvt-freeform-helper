namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const int MaxNotchRowPreviewItems = 16;

    public IReadOnlyList<NotchRowPreview> GetLastNotchRowsForRegularPad(int regularPadId)
    {
        if (_notchCache.LastGeneratedTable is null || _notchCache.LastGeneratedTable.Rows.Count == 0)
        {
            return Array.Empty<NotchRowPreview>();
        }

        return _notchCache.LastGeneratedTable.Rows
            .Select((row, index) => new { row, index })
            .Where(item => item.row.RegularPadIndex == regularPadId)
            .OrderBy(item => item.index)
            .Take(MaxNotchRowPreviewItems)
            .Select(item => new NotchRowPreview(
                RowNumber: item.index + 1,
                Version: item.row.Version,
                IcIndex: item.row.IcIndex,
                DiffIndex: item.row.DiffIndex,
                RegularPadId: item.row.RegularPadIndex,
                CadPadId: item.row.CadPadId,
                ValuesText: string.Join(", ", item.row.Values ?? Array.Empty<int>()),
                Comment: item.row.Comment ?? string.Empty))
            .ToList();
    }

    public int GetLastNotchRowCountForRegularPad(int regularPadId)
    {
        if (_notchCache.LastGeneratedTable is null || _notchCache.LastGeneratedTable.Rows.Count == 0)
        {
            return 0;
        }

        return _notchCache.LastGeneratedTable.Rows.Count(row => row.RegularPadIndex == regularPadId);
    }
}
