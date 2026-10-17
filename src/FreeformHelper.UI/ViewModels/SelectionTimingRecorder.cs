namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Keeps the durations of the last selection update as one value. The view model stores one
/// recorder. A reader always sees the four durations of the same update.
/// </summary>
internal sealed class SelectionTimingRecorder
{
    private SelectionTimings _last = SelectionTimings.Empty;

    /// <summary>
    /// Gets the durations of the last recorded selection update.
    /// </summary>
    public SelectionTimings Last => Volatile.Read(ref _last);

    /// <summary>
    /// Replaces the recorded durations.
    /// </summary>
    public void Record(long summaryMs, long inspectorMs, long notchPreviewMs, long totalMs) =>
        Volatile.Write(ref _last, new SelectionTimings(summaryMs, inspectorMs, notchPreviewMs, totalMs));
}

/// <summary>
/// The durations of one selection update in milliseconds.
/// </summary>
internal sealed record SelectionTimings(long SummaryMs, long InspectorMs, long NotchPreviewMs, long TotalMs)
{
    /// <summary>
    /// Gets the value before the first selection update.
    /// </summary>
    public static SelectionTimings Empty { get; } = new(0, 0, 0, 0);
}
