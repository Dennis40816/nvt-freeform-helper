using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

public sealed record SimulationWorkspaceSession(
    RegularGrid Grid,
    NotchTable Table,
    int NullDiffValue,
    int SourceRevision,
    IReadOnlySet<int> ActiveRegularPadIds,
    bool DefaultShowRegular = true,
    bool DefaultHighlightUnmatched = true,
    bool DefaultHighlightFreeform = true,
    IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision>? CadOutputFwDiffAssignmentDecisionByCadId = null,
    RegularGrid? CadOutputFwDiffGridProjection = null,
    IReadOnlyList<CadPad>? CadPadsForCopperProjection = null,
    IReadOnlyDictionary<int, int>? CadOutputFwDiffIndexByCadId = null,
    IReadOnlyDictionary<int, int>? CadIcIndexByCadId = null,
    NotchComputationMode ComputationMode = NotchComputationMode.CadAllocation)
{
    public RegularGrid FwDiffGrid => Grid;
    public RegularGrid CadOutputFwDiffGrid => CadOutputFwDiffGridProjection ?? Grid;
    // Backward-compatible alias for callers that still use the old simulation naming.
    public RegularGrid CadLayerDiffGrid => CadOutputFwDiffGrid;
    public int RowCount => Grid.Rows;
    public int ColumnCount => Grid.Cols;
    public int PadCount => Grid.Pads.Count;
    public IReadOnlyList<CadPad> CadPads => CadPadsForCopperProjection ?? Array.Empty<CadPad>();
    public IReadOnlyDictionary<int, int> CadOutputFwDiffIndices => CadOutputFwDiffIndexByCadId ?? EmptyIntMap;
    public IReadOnlyDictionary<int, int> CadIcIndices => CadIcIndexByCadId ?? EmptyIntMap;
    public IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> CadOutputFwDiffAssignmentDecisions =>
        CadOutputFwDiffAssignmentDecisionByCadId ?? EmptyDecisionMap;

    private static readonly IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> EmptyDecisionMap =
        new Dictionary<int, CadOutputFwDiffAssignmentDecision>();
    private static readonly IReadOnlyDictionary<int, int> EmptyIntMap =
        new Dictionary<int, int>();
}

