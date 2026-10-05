using System.Globalization;

namespace FreeformHelper.Domain.Pads;

/// <summary>
/// Identifies a regular pad independently of its position in a pad list.
/// </summary>
public readonly record struct RegularPadId(int Value) : IComparable<RegularPadId>
{
    public int CompareTo(RegularPadId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static bool operator <(RegularPadId left, RegularPadId right) => left.CompareTo(right) < 0;
    public static bool operator >(RegularPadId left, RegularPadId right) => left.CompareTo(right) > 0;
    public static bool operator <=(RegularPadId left, RegularPadId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(RegularPadId left, RegularPadId right) => left.CompareTo(right) >= 0;
}
