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
    /// <returns>An enumerable of (int code, string value) tuples.</returns>
    internal static IEnumerable<(int code, string value)> ReadPairs(TextReader reader)
    {
        Interlocked.Increment(ref _tokenizationCount);
        while (true)
        {
            var codeLine = reader.ReadLine();
            if (codeLine is null)
            {
                yield break; // End of stream
            }

            var valueLine = reader.ReadLine();
            if (valueLine is null)
            {
                yield break; // Malformed DXF (odd number of lines)
            }

            // Attempt to parse the group code. Skip if not a valid integer.
            if (!int.TryParse(codeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                continue;
            }

            yield return (code, valueLine.Trim());
        }
    }

    /// <summary>
    /// Parses a string to a double, ensuring that the invariant culture is used
    /// (important for DXF which uses '.' as a decimal separator universally).
    /// </summary>
    /// <param name="s">The string representation of the double.</param>
    /// <returns>The parsed double value.</returns>
    private static double ParseDouble(string s)
    {
        return double.Parse(s, CultureInfo.InvariantCulture);
    }

    private static int ParseInt(string s, int fallback)
    {
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
