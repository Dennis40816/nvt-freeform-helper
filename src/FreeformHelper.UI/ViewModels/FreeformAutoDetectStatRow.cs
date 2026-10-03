using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed class FreeformAutoDetectStatRow
{
    public FreeformAutoDetectStatRow(string type, int count, int total)
    {
        Type = type;
        Count = count;
        Total = total;
    }

    public string Type { get; }

    public int Count { get; }

    public int Total { get; }

    public string CountText => Count.ToString(CultureInfo.InvariantCulture);

    public string RatioText => Total <= 0 ? "0.0%" : $"{(double)Count / Total:P1}";
}
