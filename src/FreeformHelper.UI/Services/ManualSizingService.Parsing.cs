using System.Globalization;

namespace FreeformHelper.UI.Services;

public sealed partial class ManualSizingService
{
    public bool TryParseRowRange(string input, int totalRows, out List<int> rows, out string error)
    {
        error = string.Empty;
        rows = new List<int>();

        if (totalRows <= 0)
        {
            error = "Grid not built.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return false; // No input, no rows.
        }

        // Parse the range in display row format (top-to-bottom, 0-indexed).
        if (!TryParseIndexRange(input, 0, totalRows - 1, out var displayRows, out error))
        {
            return false;
        }

        // Convert display rows to actual row indices (bottom-to-top, 0-indexed).
        rows = displayRows.Select(r => ToActualRow(r, totalRows)).Distinct().OrderBy(r => r).ToList();
        return rows.Count > 0;
    }

    /// <summary>
    /// Parses a string input representing a range of columns (e.g., "0", "1-5", "2,4,6")
    /// and converts them into column indices.
    /// </summary>
    /// <param name="input">The string input from the user.</param>
    /// <param name="totalCols">The total number of columns in the grid.</param>
    /// <param name="cols">Output: A list of parsed column indices.</param>
    /// <param name="error">Output: An error message if parsing fails.</param>
    /// <returns>True if parsing was successful and at least one column was found, false otherwise.</returns>
    public bool TryParseColRange(string input, int totalCols, out List<int> cols, out string error)
    {
        error = string.Empty;
        cols = new List<int>();

        if (totalCols <= 0)
        {
            error = "Grid not built.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Parse the range directly as column indices.
        if (!TryParseIndexRange(input, 0, totalCols - 1, out var parsed, out error))
        {
            return false;
        }

        cols = parsed.Distinct().OrderBy(c => c).ToList();
        return cols.Count > 0;
    }

    /// <summary>
    /// Parses a generic string input into a list of unique, sorted integer indices,
    /// supporting single indices (e.g., "5") and ranges (e.g., "2-7").
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <param name="min">The minimum valid index (inclusive).</param>
    /// <param name="max">The maximum valid index (inclusive).</param>
    /// <param name="indices">Output: A list of parsed indices.</param>
    /// <param name="error">Output: An error message if parsing fails.</param>
    /// <returns>True if parsing was successful and at least one index was found, false otherwise.</returns>
    public static bool TryParseIndexRange(string input, int min, int max, out List<int> indices, out string error)
    {
        indices = new List<int>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in parts)
        {
            var part = raw.Trim();
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            if (part.Contains('-')) // Handle range (e.g., "1-5")
            {
                var rangeParts = part.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (rangeParts.Length != 2)
                {
                    error = $"Invalid range format: '{part}'. Expected 'start-end'.";
                    return false;
                }

                if (!int.TryParse(rangeParts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
                    !int.TryParse(rangeParts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
                {
                    error = $"Invalid numbers in range: '{part}'.";
                    return false;
                }

                // Ensure start is less than or equal to end.
                if (start > end)
                {
                    (start, end) = (end, start);
                }

                for (var i = start; i <= end; i++)
                {
                    if (i < min || i > max)
                    {
                        error = $"Index {i} in range '{part}' is out of bounds ({min}-{max}).";
                        return false;
                    }
                    indices.Add(i);
                }
            }
            else // Handle single index (e.g., "3")
            {
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                {
                    error = $"Invalid index format: '{part}'.";
                    return false;
                }

                if (index < min || index > max)
                {
                    error = $"Index {index} is out of bounds ({min}-{max}).";
                    return false;
                }

                indices.Add(index);
            }
        }

        // Remove duplicates and sort for consistency.
        indices = indices.Distinct().OrderBy(i => i).ToList();
        return indices.Count > 0;
    }

    /// <summary>
    /// Converts an actual internal row index (bottom-to-top) to a display row index (top-to-bottom).
    /// </summary>
    /// <param name="actualRow">The internal zero-based row index.</param>
    /// <param name="totalRows">The total number of rows.</param>
    /// <returns>The zero-based display row index.</returns>
    public static int ToDisplayRow(int actualRow, int totalRows)
    {
        if (totalRows <= 0)
        {
            return actualRow;
        }

        return Math.Clamp((totalRows - 1) - actualRow, 0, totalRows - 1);
    }

    /// <summary>
    /// Converts a display row index (top-to-bottom) to an actual internal row index (bottom-to-top).
    /// </summary>
    /// <param name="displayRow">The zero-based display row index.</param>
    /// <param name="totalRows">The total number of rows.</param>
    /// <returns>The zero-based actual internal row index.</returns>
    public static int ToActualRow(int displayRow, int totalRows)
    {
        if (totalRows <= 0)
        {
            return displayRow;
        }

        return Math.Clamp((totalRows - 1) - displayRow, 0, totalRows - 1);
    }

}
