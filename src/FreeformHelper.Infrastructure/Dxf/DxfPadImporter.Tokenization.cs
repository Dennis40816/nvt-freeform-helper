using System.Globalization;

namespace FreeformHelper.Infrastructure.Dxf;

public sealed partial class DxfPadImporter
{
    private static long _tokenizationCount;

    internal static long TokenizationCount => Interlocked.Read(ref _tokenizationCount);

    /// <summary>
    /// Reads DXF group code-value pairs from a <see cref="TextReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="TextReader"/> providing DXF content.</param>
    /// <param name="requireCompletePairs">Whether an unmatched group code must reject a CAD load.</param>
    /// <returns>An enumerable of (int code, string value) tuples.</returns>
    internal static IEnumerable<(int code, string value)> ReadPairs(TextReader reader, bool requireCompletePairs = false)
    {
        Interlocked.Increment(ref _tokenizationCount);
        while (true)
        {
            var codeLine = reader.ReadLine();
            if (codeLine is null)
            {
                yield break; // End of stream
            }

            var valueLine = ReadGroupValue(reader, requireCompletePairs);
            if (valueLine is null)
            {
                yield break; // Malformed DXF (odd number of lines)
            }

            // Attempt to parse the group code. Skip if not a valid integer.
            if (!int.TryParse(codeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                continue;
            }

            var value = valueLine.Trim();
            yield return (code, value);

            // Anything after the EOF marker is ignored, as CAD applications do.
            if (requireCompletePairs && code == 0 && string.Equals(value, "EOF", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Parses a string to a double, ensuring that the invariant culture is used
    /// (important for DXF which uses '.' as a decimal separator universally).
    /// </summary>
    /// <param name="s">The string representation of the double.</param>
    /// <param name="groupCode">The DXF group code associated with the value.</param>
    /// <returns>The parsed double value.</returns>
    private static double ParseDouble(string s, int groupCode)
    {
        // "Infinity", "NaN" and out-of-range text such as "1E400" parse as non-finite doubles.
        // Treat them as invalid numbers so a damaged corner never shrinks a pad silently.
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value))
        {
            return value;
        }

        var badText = s[..Math.Min(s.Length, 40)];
        throw new InvalidDataException(FormattableString.Invariant($"Invalid DXF number for group code {groupCode}: '{badText}'."));
    }

    private static int ParseInt(string s, int fallback)
    {
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
