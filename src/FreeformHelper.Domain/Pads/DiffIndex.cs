using System.Globalization;

namespace FreeformHelper.Domain.Pads;

/// <summary>
/// A per-IC FW diff index preserving all Int32 values, including legacy sentinels.
/// </summary>
public readonly record struct DiffIndex(int Value) : IComparable<DiffIndex>
{
    public int CompareTo(DiffIndex other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static bool operator <(DiffIndex left, DiffIndex right) => left.CompareTo(right) < 0;
    public static bool operator >(DiffIndex left, DiffIndex right) => left.CompareTo(right) > 0;
    public static bool operator <=(DiffIndex left, DiffIndex right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DiffIndex left, DiffIndex right) => left.CompareTo(right) >= 0;
}
