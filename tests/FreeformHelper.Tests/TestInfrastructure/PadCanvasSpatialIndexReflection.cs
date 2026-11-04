using System.Reflection;
using FreeformHelper.UI.Controls;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Reads the spatial index fields of <see cref="PadCanvas"/>. Their types are private nested classes, which a
/// compile-time bound accessor cannot name (UnsafeAccessorType is rejected on a ref return), so this stays on
/// reflection in one place.
/// </summary>
internal static class PadCanvasSpatialIndexReflection
{
    public static object? CadIndex(PadCanvas canvas) => ReadField(canvas, "_cadIndex");

    public static object? RegularIndex(PadCanvas canvas) => ReadField(canvas, "_regularIndex");

    private static object? ReadField(PadCanvas canvas, string fieldName)
    {
        var field = typeof(PadCanvas).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(nameof(PadCanvas), fieldName);
        return field.GetValue(canvas);
    }
}
