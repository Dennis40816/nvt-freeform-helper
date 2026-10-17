using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// The per-CAD-pad index tables of one CAD load: the Step 4 output FW diff index, the stable
/// display index, the inferred IC index and the latest FW diff assignment decisions. The view model
/// stores one instance. The three dictionaries are cleared and refilled in place. The decisions are
/// replaced as a whole.
/// </summary>
internal sealed class CadIndexMaps
{
    /// <summary>
    /// Gets the CAD pad ID to Step 4 CAD output FW diff index table.
    /// </summary>
    public Dictionary<int, int> OutputFwDiffIndexByCadId { get; } = new();

    /// <summary>
    /// Gets the CAD pad ID to stable display index table (left to right, top to bottom, 1-based).
    /// </summary>
    public Dictionary<int, int> DisplayIndexByCadId { get; } = new();

    /// <summary>
    /// Gets the CAD pad ID to inferred IC index table (grid partition based).
    /// </summary>
    public Dictionary<int, int> IcIndexByCadId { get; } = new();

    /// <summary>
    /// Gets or sets the latest CAD output FW diff assignment decisions by CAD pad ID.
    /// </summary>
    public IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> LatestAssignmentDecisionsByCadId { get; set; } =
        new Dictionary<int, CadOutputFwDiffAssignmentDecision>();
}
