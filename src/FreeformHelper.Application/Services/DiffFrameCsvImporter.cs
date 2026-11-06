using System.Globalization;
using System.Text;

namespace FreeformHelper.Application.Services;

public static class DiffFrameCsvImporter
{
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
        List<string> lines,
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
                FinalizeFrameIfNeeded(sourcePath, frames, state);
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

            if (state.RowCount > 0 && IsSectionHeaderLine(line))
            {
                FinalizeFrameIfNeeded(sourcePath, frames, state);
                ResetAfterSectionHeader(state);
                continue;
            }

            RecordMarkerWithoutPayload(state, line, lineNumber, resolvedOptions.DiffDataMarker, sourcePath, diagnostics);
        }

        FinalizeFrameIfNeeded(sourcePath, frames, state);

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

    private static void BeginFrame(FrameParseState state, string line, int lineNumber, string diffDataMarker)
    {
        var timestampText = TryExtractNamedField(line, "timestamp");
        state.BeginFrame(
            lineNumber,
            timestampText,
            TryParseTimestamp(timestampText),
            TryParseIntField(line, "Frame index"),
            TryParseIntField(line, "Break point index"));

        if (TryParseInlineCsvRowAfterMarker(line, diffDataMarker, out var inlineRow))
        {
            state.AddMarkerRow(lineNumber, inlineRow);
        }
    }

    private static bool TryAppendMarkerRow(FrameParseState state, string line, int lineNumber, string diffDataMarker)
    {
        if (state.MarkerFound || !TryParseInlineCsvRowAfterMarker(line, diffDataMarker, out var markerRow))
        {
            return false;
        }

        state.AddMarkerRow(lineNumber, markerRow);
        return true;
    }

    private static bool TryAppendNumericRow(FrameParseState state, string line, int lineNumber)
    {
        if (!TryParseNumericRow(line, out var numericRow))
        {
            return false;
        }

        state.AddDataRow(lineNumber, numericRow);
        return true;
    }

    private static void ResetAfterSectionHeader(FrameParseState state)
    {
        state.ClearFrameData();
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

        state.RecordMarkerLine(lineNumber);
        diagnostics.Add($"[{Path.GetFileName(sourcePath)}:{lineNumber}] DiffData marker found without inline numeric payload.");
    }

    private static void FinalizeFrameIfNeeded(
        string sourcePath,
        List<DiffFrameCsvFrame> frames,
        FrameParseState state)
    {
        if (!state.InFrame)
        {
            return;
        }

        if (state.RowCount == 0)
        {
            throw new InvalidDataException(
                $"[{Path.GetFileName(sourcePath)}] Frame {state.FrameSequence} at line {state.CurrentHeaderLine} does not contain numeric rows.");
        }

        var expectedWidth = state.RowWidth(0);
        for (var rowIndex = 1; rowIndex < state.RowCount; rowIndex++)
        {
            if (state.RowWidth(rowIndex) != expectedWidth)
            {
                throw new InvalidDataException(
                    $"[{Path.GetFileName(sourcePath)}] Frame {state.FrameSequence} has inconsistent row width at row {rowIndex}: {state.RowWidth(rowIndex)} != {expectedWidth}.");
            }
        }

        frames.Add(new DiffFrameCsvFrame(
            state.FrameSequence,
            state.CurrentFrameIndex,
            state.CurrentBreakpointIndex,
            state.CurrentTimestampText,
            state.CurrentTimestamp,
            state.CurrentHeaderLine,
            state.CurrentMarkerLine,
            state.CurrentDataStartLine,
            state.CurrentDataEndLine,
            state.SnapshotRows()));

        state.EndFrame();
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
        private List<IReadOnlyList<double>> _rows = new();

        public int RowCount => _rows.Count;

        public int CurrentHeaderLine { get; private set; } = -1;

        public int? CurrentMarkerLine { get; private set; }

        public int? CurrentDataStartLine { get; private set; }

        public int? CurrentDataEndLine { get; private set; }

        public string? CurrentTimestampText { get; private set; }

        public TimeSpan? CurrentTimestamp { get; private set; }

        public int? CurrentFrameIndex { get; private set; }

        public int? CurrentBreakpointIndex { get; private set; }

        public int FrameSequence { get; private set; } = -1;

        public bool InFrame { get; private set; }

        public bool MarkerFound { get; private set; }

        public int RowWidth(int rowIndex) => _rows[rowIndex].Count;

        public IReadOnlyList<double>[] SnapshotRows() => _rows.ToArray();

        public void BeginFrame(
            int headerLine,
            string? timestampText,
            TimeSpan? timestamp,
            int? frameIndex,
            int? breakpointIndex)
        {
            ClearFrameData();
            CurrentHeaderLine = headerLine;
            CurrentTimestampText = timestampText;
            CurrentTimestamp = timestamp;
            CurrentFrameIndex = frameIndex;
            CurrentBreakpointIndex = breakpointIndex;
            FrameSequence++;
            InFrame = true;
        }

        public void AddMarkerRow(int lineNumber, IReadOnlyList<double> row)
        {
            RecordMarkerLine(lineNumber);
            AddDataRow(lineNumber, row);
        }

        public void AddDataRow(int lineNumber, IReadOnlyList<double> row)
        {
            CurrentDataStartLine ??= lineNumber;
            CurrentDataEndLine = lineNumber;
            _rows.Add(row);
        }

        public void RecordMarkerLine(int lineNumber)
        {
            MarkerFound = true;
            CurrentMarkerLine = lineNumber;
        }

        public void ClearFrameData()
        {
            _rows = new List<IReadOnlyList<double>>();
            CurrentHeaderLine = -1;
            CurrentMarkerLine = null;
            CurrentDataStartLine = null;
            CurrentDataEndLine = null;
            CurrentTimestampText = null;
            CurrentTimestamp = null;
            CurrentFrameIndex = null;
            CurrentBreakpointIndex = null;
            MarkerFound = false;
        }

        public void EndFrame()
        {
            InFrame = false;
        }
    }
}
