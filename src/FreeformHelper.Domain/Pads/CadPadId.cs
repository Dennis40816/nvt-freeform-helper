using System.Globalization;

namespace FreeformHelper.Domain.Pads;

/// <summary>
/// Identifies a CAD pad independently of its position in a pad list.
/// </summary>
public readonly record struct CadPadId(int Value) : IComparable<CadPadId>
{
    public int CompareTo(CadPadId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static bool operator <(CadPadId left, CadPadId right) => left.CompareTo(right) < 0;
    public static bool operator >(CadPadId left, CadPadId right) => left.CompareTo(right) > 0;
    public static bool operator <=(CadPadId left, CadPadId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(CadPadId left, CadPadId right) => left.CompareTo(right) >= 0;
}
