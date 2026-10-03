namespace FreeformHelper.Application.Services;

public sealed record DiffFrameCsvImportOptions(
    string TimestampPrefix = "timestamp(",
    string DiffDataMarker = "DiffData");

public sealed record DiffFrameCsvSourceFile(
    string SourcePath,
    int? DeclaredCols,
    int? DeclaredRows,
    IReadOnlyDictionary<string, string> HeaderFields,
    IReadOnlyList<DiffFrameCsvFrame> Frames,
    IReadOnlyList<string> Diagnostics);

public sealed record DiffFrameCsvFrame(
    int SequenceIndex,
    int? FrameIndex,
    int? BreakpointIndex,
    string? TimestampText,
    TimeSpan? Timestamp,
    int HeaderLineNumber,
    int? MarkerLineNumber,
    int? DataStartLineNumber,
    int? DataEndLineNumber,
    IReadOnlyList<IReadOnlyList<double>> Rows)
{
    public int RowCount => Rows.Count;
    public int ColCount => Rows.Count > 0 ? Rows[0].Count : 0;
}

public enum DiffFrameGridRowOrigin
{
    TopRowIsRegularRow0 = 0,
    TopRowIsRegularLastRow = 1,
}

public sealed record DiffFrameGridCellValue(
    int FrameRow,
    int FrameCol,
    int RegularRow,
    int RegularCol,
    int RegularPadId,
    double Value);

public sealed record DiffFrameGridProjectionResult(
    bool IsCompatible,
    string? Diagnostic,
    IReadOnlyList<DiffFrameGridCellValue> Cells)
{
    public static DiffFrameGridProjectionResult Incompatible(string diagnostic) =>
        new(false, diagnostic, Array.Empty<DiffFrameGridCellValue>());
}
