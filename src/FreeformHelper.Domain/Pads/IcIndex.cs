using System.Globalization;

namespace FreeformHelper.Domain.Pads;

/// <summary>
/// An IC mapping index preserving all Int32 values, including legacy sentinels.
/// </summary>
public readonly record struct IcIndex(int Value) : IComparable<IcIndex>
{
    public int CompareTo(IcIndex other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static bool operator <(IcIndex left, IcIndex right) => left.CompareTo(right) < 0;
    public static bool operator >(IcIndex left, IcIndex right) => left.CompareTo(right) > 0;
    public static bool operator <=(IcIndex left, IcIndex right) => left.CompareTo(right) <= 0;
    public static bool operator >=(IcIndex left, IcIndex right) => left.CompareTo(right) >= 0;
}
