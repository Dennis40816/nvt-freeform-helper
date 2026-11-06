using System.Globalization;
using System.Text;

namespace FreeformHelper.Application.Services;

public static class DiffFrameCsvImporter
{
    public static DiffFrameCsvSourceFile Import(string sourcePath, DiffFrameCsvImportOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var lines = File.ReadAllLines(sourcePath);
        return ImportLines(sourcePath, lines, options);
    }

    public static DiffFrameCsvSourceFile ImportContent(string sourceName, string content, DiffFrameCsvImportOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(content);

        var lines = new List<string>();
        using (var reader = new StringReader(content))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                lines.Add(line);
            }
        }

        return ImportLines(sourceName, lines, options);
    }

    private static DiffFrameCsvSourceFile ImportLines(
        string sourcePath,
        IReadOnlyList<string> lines,
        DiffFrameCsvImportOptions? options)
    {
        var resolvedOptions = options ?? new DiffFrameCsvImportOptions();
        var headerFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<string>();
        var frames = new List<DiffFrameCsvFrame>();
        var state = new FrameParseState();

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var lineNumber = lineIndex + 1;
            var line = lines[lineIndex];

            if (IsTimestampLine(line, resolvedOptions.TimestampPrefix))
            {
                FinalizeCurrentFrame(sourcePath, frames, state);
                BeginFrame(state, line, lineNumber, resolvedOptions.DiffDataMarker);
                continue;
            }

            if (!state.InFrame)
            {
                foreach (var pair in ParseNamedFields(line))
                {
                    headerFields[pair.Key] = pair.Value;
                }

                continue;
            }

            if (TryAppendMarkerRow(state, line, lineNumber, resolvedOptions.DiffDataMarker) ||
                TryAppendNumericRow(state, line, lineNumber))
            {
                continue;
            }

            if (state.CurrentRows.Count > 0 && IsSectionHeaderLine(line))
            {
                FinalizeCurrentFrame(sourcePath, frames, state);
                ResetAfterSectionHeader(state);
                continue;
            }

            RecordMarkerWithoutPayload(state, line, lineNumber, resolvedOptions.DiffDataMarker, sourcePath, diagnostics);
        }

        FinalizeCurrentFrame(sourcePath, frames, state);

        headerFields.TryGetValue("Xch", out var declaredColsRaw);
        headerFields.TryGetValue("Ych", out var declaredRowsRaw);
        var declaredCols = TryParseInvariantInt(declaredColsRaw);
        var declaredRows = TryParseInvariantInt(declaredRowsRaw);

        return new DiffFrameCsvSourceFile(
            sourcePath,
            declaredCols,
            declaredRows,
            headerFields,
            frames,
            diagnostics);
    }

    private static void FinalizeCurrentFrame(
        string sourcePath,
        List<DiffFrameCsvFrame> frames,
        FrameParseState state)
    {
        FinalizeFrameIfNeeded(
            sourcePath,
            frames,
            state.CurrentRows,
            ref state.InFrame,
            ref state.FrameSequence,
            state.CurrentHeaderLine,
            state.CurrentMarkerLine,
            state.CurrentDataStartLine,
            state.CurrentDataEndLine,
            state.CurrentTimestampText,
            state.CurrentTimestamp,
            state.CurrentFrameIndex,
            state.CurrentBreakpointIndex);
    }

    private static void BeginFrame(FrameParseState state, string line, int lineNumber, string diffDataMarker)
    {
        state.CurrentRows = new List<IReadOnlyList<double>>();
        state.CurrentHeaderLine = lineNumber;
        state.CurrentMarkerLine = null;
        state.CurrentDataStartLine = null;
        state.CurrentDataEndLine = null;
        state.CurrentTimestampText = TryExtractNamedField(line, "timestamp");
        state.CurrentTimestamp = TryParseTimestamp(state.CurrentTimestampText);
        state.CurrentFrameIndex = TryParseIntField(line, "Frame index");
        state.CurrentBreakpointIndex = TryParseIntField(line, "Break point index");
        state.FrameSequence++;
        state.InFrame = true;
        state.MarkerFound = false;

        if (TryParseInlineCsvRowAfterMarker(line, diffDataMarker, out var inlineRow))
        {
            state.MarkerFound = true;
            state.CurrentMarkerLine = lineNumber;
            state.CurrentDataStartLine = lineNumber;
            state.CurrentDataEndLine = lineNumber;
            state.CurrentRows.Add(inlineRow);
        }
    }

    private static bool TryAppendMarkerRow(FrameParseState state, string line, int lineNumber, string diffDataMarker)
    {
        if (state.MarkerFound || !TryParseInlineCsvRowAfterMarker(line, diffDataMarker, out var markerRow))
        {
            return false;
        }

        state.MarkerFound = true;
        state.CurrentMarkerLine = lineNumber;
        state.CurrentDataStartLine ??= lineNumber;
        state.CurrentDataEndLine = lineNumber;
        state.CurrentRows.Add(markerRow);
        return true;
    }

    private static bool TryAppendNumericRow(FrameParseState state, string line, int lineNumber)
    {
        if (!TryParseNumericRow(line, out var numericRow))
        {
            return false;
        }

        state.CurrentDataStartLine ??= lineNumber;
        state.CurrentDataEndLine = lineNumber;
        state.CurrentRows.Add(numericRow);
        return true;
    }

    private static void ResetAfterSectionHeader(FrameParseState state)
    {
        state.CurrentRows = new List<IReadOnlyList<double>>();
        state.CurrentHeaderLine = -1;
        state.CurrentMarkerLine = null;
        state.CurrentDataStartLine = null;
        state.CurrentDataEndLine = null;
        state.CurrentTimestampText = null;
        state.CurrentTimestamp = null;
        state.CurrentFrameIndex = null;
        state.CurrentBreakpointIndex = null;
        state.MarkerFound = false;
    }

    private static void RecordMarkerWithoutPayload(
        FrameParseState state,
        string line,
        int lineNumber,
        string diffDataMarker,
        string sourcePath,
        List<string> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(line) ||
            state.MarkerFound ||
            !line.Contains(diffDataMarker, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        state.CurrentMarkerLine = lineNumber;
        state.MarkerFound = true;
        diagnostics.Add($"[{Path.GetFileName(sourcePath)}:{lineNumber}] DiffData marker found without inline numeric payload.");
    }

    private static void FinalizeFrameIfNeeded(
        string sourcePath,
        List<DiffFrameCsvFrame> frames,
        List<IReadOnlyList<double>> currentRows,
        ref bool inFrame,
        ref int frameSequence,
        int currentHeaderLine,
        int? currentMarkerLine,
        int? currentDataStartLine,
        int? currentDataEndLine,
        string? currentTimestampText,
        TimeSpan? currentTimestamp,
        int? currentFrameIndex,
        int? currentBreakpointIndex)
    {
        if (!inFrame)
        {
            return;
        }

        if (currentRows.Count == 0)
        {
            throw new InvalidDataException(
                $"[{Path.GetFileName(sourcePath)}] Frame {frameSequence} at line {currentHeaderLine} does not contain numeric rows.");
        }

        var expectedWidth = currentRows[0].Count;
        for (var rowIndex = 1; rowIndex < currentRows.Count; rowIndex++)
        {
            if (currentRows[rowIndex].Count != expectedWidth)
            {
                throw new InvalidDataException(
                    $"[{Path.GetFileName(sourcePath)}] Frame {frameSequence} has inconsistent row width at row {rowIndex}: {currentRows[rowIndex].Count} != {expectedWidth}.");
            }
        }

        frames.Add(new DiffFrameCsvFrame(
            frameSequence,
            currentFrameIndex,
            currentBreakpointIndex,
            currentTimestampText,
            currentTimestamp,
            currentHeaderLine,
            currentMarkerLine,
            currentDataStartLine,
            currentDataEndLine,
            currentRows.ToArray()));

        inFrame = false;
    }

    private static bool IsTimestampLine(string line, string timestampPrefix) =>
        line.TrimStart().StartsWith(timestampPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsSectionHeaderLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 0 &&
               trimmed.EndsWith(':') &&
               !trimmed.Contains(',', StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ParseNamedFields(string line)
    {
        var fields = ParseCsvFields(line);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var separatorIndex = FindNamedFieldSeparator(field);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = field[..separatorIndex].Trim().Trim('"');
            var value = field[(separatorIndex + 1)..].Trim().Trim('"');
            if (key.Length == 0)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    private static int FindNamedFieldSeparator(string field)
    {
        var parenthesisDepth = 0;
        for (var index = 0; index < field.Length; index++)
        {
            var ch = field[index];
            switch (ch)
            {
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    if (parenthesisDepth > 0)
                    {
                        parenthesisDepth--;
                    }

                    break;
                case ':':
                    if (parenthesisDepth == 0)
                    {
                        return index;
                    }

                    break;
            }
        }

        return -1;
    }

    private static string? TryExtractNamedField(string line, string fieldPrefix)
    {
        foreach (var pair in ParseNamedFields(line))
        {
            if (pair.Key.StartsWith(fieldPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static int? TryParseIntField(string line, string fieldName)
    {
        var value = TryExtractNamedField(line, fieldName);
        return TryParseInvariantInt(value);
    }

    private static int? TryParseInvariantInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static TimeSpan? TryParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (3 or 4))
        {
            return null;
        }

        if (!parts.All(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
        {
            return null;
        }

        var values = parts
            .Select(part => int.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();

        return values.Length == 3
            ? TimeSpan.FromMinutes(values[0]) + TimeSpan.FromSeconds(values[1]) + TimeSpan.FromMilliseconds(values[2])
            : TimeSpan.FromHours(values[0]) + TimeSpan.FromMinutes(values[1]) + TimeSpan.FromSeconds(values[2]) + TimeSpan.FromMilliseconds(values[3]);
    }

    private static bool TryParseNumericRow(string line, out IReadOnlyList<double> numericRow)
    {
        numericRow = Array.Empty<double>();

        var fields = ParseCsvFields(line);
        if (fields.Count == 0)
        {
            return false;
        }

        var values = new List<double>(fields.Count);
        foreach (var field in fields)
        {
            var token = field.Trim();
            if (token.Length == 0)
            {
                continue;
            }

            if (!double.TryParse(token, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            values.Add(value);
        }

        if (values.Count == 0)
        {
            return false;
        }

        numericRow = values;
        return true;
    }

    private static bool TryParseInlineCsvRowAfterMarker(string line, string marker, out IReadOnlyList<double> numericRow)
    {
        numericRow = Array.Empty<double>();
        var fields = ParseCsvFields(line);
        if (fields.Count == 0)
        {
            return false;
        }

        var markerIndex = -1;
        for (var i = 0; i < fields.Count; i++)
        {
            var token = fields[i].Trim().Trim('"');
            if (string.Equals(token, marker, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, marker + ":", StringComparison.OrdinalIgnoreCase))
            {
                markerIndex = i;
                break;
            }
        }

        if (markerIndex < 0)
        {
            return false;
        }

        var values = new List<double>(Math.Max(0, fields.Count - markerIndex - 1));
        for (var i = markerIndex + 1; i < fields.Count; i++)
        {
            var token = fields[i].Trim().Trim('"');
            if (token.Length == 0)
            {
                continue;
            }

            if (!double.TryParse(token, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            {
                break;
            }

            values.Add(value);
        }

        if (values.Count == 0)
        {
            return false;
        }

        numericRow = values;
        return true;
    }

    private static List<string> ParseCsvFields(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private sealed class FrameParseState
    {
        public List<IReadOnlyList<double>> CurrentRows = new();
        public int CurrentHeaderLine = -1;
        public int? CurrentMarkerLine;
        public int? CurrentDataStartLine;
        public int? CurrentDataEndLine;
        public string? CurrentTimestampText;
        public TimeSpan? CurrentTimestamp;
        public int? CurrentFrameIndex;
        public int? CurrentBreakpointIndex;
        public int FrameSequence = -1;
        public bool InFrame;
        public bool MarkerFound;
    }
}
