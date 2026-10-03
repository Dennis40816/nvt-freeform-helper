namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private const int MaxNotchRowPreviewItems = 16;

    public IReadOnlyList<NotchRowPreview> GetLastNotchRowsForRegularPad(int regularPadId)
    {
        if (_lastGeneratedNotchTable is null || _lastGeneratedNotchTable.Rows.Count == 0)
        {
            return Array.Empty<NotchRowPreview>();
        }

        return _lastGeneratedNotchTable.Rows
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
        if (_lastGeneratedNotchTable is null || _lastGeneratedNotchTable.Rows.Count == 0)
        {
            return 0;
        }

        return _lastGeneratedNotchTable.Rows.Count(row => row.RegularPadIndex == regularPadId);
    }
}
