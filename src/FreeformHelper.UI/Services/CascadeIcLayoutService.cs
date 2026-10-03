namespace FreeformHelper.UI.Services;

internal readonly record struct CascadeIcLayoutRow(
    int IcIndex,
    int XChannels,
    int YChannels);

internal sealed class CascadeIcLayout
{
    public CascadeIcLayout(IReadOnlyList<CascadeIcLayoutRow> rows)
    {
        Rows = rows ?? Array.Empty<CascadeIcLayoutRow>();
    }

    public IReadOnlyList<CascadeIcLayoutRow> Rows { get; }

    public int TotalXChannels => Rows.Sum(static row => row.XChannels);

    public int TotalYChannels => Rows.Count == 0
        ? 1
        : Rows.Max(static row => row.YChannels);

    public IReadOnlyList<int> PerIcXChannels => Rows
        .Select(static row => row.XChannels)
        .ToArray();

    public IReadOnlyList<int> PerIcYChannels => Rows
        .Select(static row => row.YChannels)
        .ToArray();
}

internal static class CascadeIcLayoutService
{
    public static CascadeIcLayout Build(
        int cascadeNum,
        int fallbackXChannels,
        int fallbackYChannels,
        IEnumerable<int>? perIcXChannels,
        IEnumerable<int>? perIcYChannels)
    {
        var cascade = Math.Max(1, cascadeNum);
        var xChannels = NormalizeChannels(perIcXChannels, cascade, Math.Max(1, fallbackXChannels));
        var yChannels = NormalizeChannels(perIcYChannels, cascade, Math.Max(1, fallbackYChannels));
        var rows = new CascadeIcLayoutRow[cascade];
        for (var index = 0; index < cascade; index++)
        {
            rows[index] = new CascadeIcLayoutRow(index + 1, xChannels[index], yChannels[index]);
        }

        return new CascadeIcLayout(rows);
    }

    public static bool HasSameChannels(
        IEnumerable<int>? leftXChannels,
        IEnumerable<int>? leftYChannels,
        IEnumerable<int>? rightXChannels,
        IEnumerable<int>? rightYChannels)
    {
        return NormalizeForCompare(leftXChannels).SequenceEqual(NormalizeForCompare(rightXChannels)) &&
               NormalizeForCompare(leftYChannels).SequenceEqual(NormalizeForCompare(rightYChannels));
    }

    private static List<int> NormalizeChannels(IEnumerable<int>? channels, int cascade, int fallback)
    {
        var normalized = channels?
            .Select(static value => Math.Max(1, value))
            .ToList() ?? new List<int>();

        if (normalized.Count == 0)
        {
            normalized.AddRange(Enumerable.Repeat(Math.Max(1, fallback), cascade));
            return normalized;
        }

        if (normalized.Count > cascade)
        {
            normalized.RemoveRange(cascade, normalized.Count - cascade);
            return normalized;
        }

        var fill = normalized[^1];
        while (normalized.Count < cascade)
        {
            normalized.Add(fill);
        }

        return normalized;
    }

    private static int[] NormalizeForCompare(IEnumerable<int>? channels)
    {
        return channels?
            .Select(static value => Math.Max(1, value))
            .ToArray() ?? Array.Empty<int>();
    }
}
