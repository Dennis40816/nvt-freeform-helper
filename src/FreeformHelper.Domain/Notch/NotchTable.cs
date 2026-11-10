namespace FreeformHelper.Domain.Notch;

/// <summary>
/// Represents a complete notch table, which is a collection of <see cref="NotchTableRow"/> entries.
/// </summary>
public sealed class NotchTable
{
    /// <summary>
    /// Gets the read-only list of all <see cref="NotchTableRow"/> instances contained in this table.
    /// </summary>
    public IReadOnlyList<NotchTableRow> Rows { get; }

    /// <summary>
    /// Gets the ToFull target coverage audit attached to this table.
    /// </summary>
    public NotchToFullCoverageAudit ToFullCoverageAudit { get; }

    /// <summary>
    /// Gets the phase timing diagnostics attached to this table.
    /// </summary>
    public NotchGenerationPhaseTimings GenerationPhaseTimings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotchTable"/> class with the specified rows.
    /// </summary>
    /// <param name="rows">A read-only list of <see cref="NotchTableRow"/> objects.</param>
    /// <param name="toFullCoverageAudit">Optional ToFull target coverage audit metadata.</param>
    /// <param name="generationPhaseTimings">Optional generation phase timing metadata.</param>
    public NotchTable(
        IReadOnlyList<NotchTableRow> rows,
        NotchToFullCoverageAudit? toFullCoverageAudit = null,
        NotchGenerationPhaseTimings? generationPhaseTimings = null)
    {
        Rows = rows;
        ToFullCoverageAudit = toFullCoverageAudit ?? NotchToFullCoverageAudit.Empty;
        GenerationPhaseTimings = generationPhaseTimings ?? NotchGenerationPhaseTimings.Empty;
    }
}
