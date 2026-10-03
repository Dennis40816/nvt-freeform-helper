using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

internal static class PadInfoTextFormatter
{
    public static string DistinctText(IEnumerable<string> values)
    {
        var list = values.Distinct().Take(3).ToList();
        if (list.Count == 0) return "-";
        if (list.Count == 1) return list[0];
        return $"{string.Join(", ", list)} (+)";
    }

    public static string RangeText(IEnumerable<int> values)
    {
        var list = values.Distinct().OrderBy(static value => value).ToList();
        if (list.Count == 0) return "-";
        if (list.Count == 1) return list[0].ToString(CultureInfo.InvariantCulture);
        return $"{list.First().ToString(CultureInfo.InvariantCulture)} - {list.Last().ToString(CultureInfo.InvariantCulture)}";
    }

    public static string CompactList(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return "-";
        }

        if (values.Count <= 3)
        {
            return string.Join(", ", values);
        }

        return $"{values[0]}, {values[1]}, {values[2]} (+{values.Count - 3})";
    }
}
