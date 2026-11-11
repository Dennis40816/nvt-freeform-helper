// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Selection;

// These projections carry existing results across the service boundary without recomputing them.
internal readonly record struct SelectionSummaryFields(
    string CadSelectionText,
    string RegularSelectionText,
    string SelectionDetailText,
    string ManualRowsRange,
    string ManualColsRange,
    string ManualRangeError,
    decimal PendingWidth,
    decimal PendingHeight,
    bool WidthMixed,
    bool HeightMixed);

internal readonly record struct SelectionSummaryPorts<TSummary>(
    TSummary Initial,
    Func<RegularGrid?, IReadOnlyCollection<int>, IReadOnlyCollection<int>, TSummary> Compute,
    Func<TSummary, SelectionSummaryFields> Project,
    Action<string> PropertyChanging,
    Action<string> PropertyChanged);

internal readonly record struct SelectionInteractionPorts(
    Func<RegularGrid?> ReadGrid,
    Func<IReadOnlyCollection<RegularPad>> ReadRegularPads,
    Action<IReadOnlyList<int>, IReadOnlyList<int>> ApplyCanvasSelection,
    Func<bool> CanClearSelection,
    Action ClearSelection,
    Func<string> ReadQuickFocusQuery,
    Action<string> WriteQuickFocusQuery,
    Action NotifyDxfEditActions,
    Action RefreshPreview,
    Action ClearPreview,
    Action<SelectionTimings> LogTimings);

internal readonly record struct SelectionManualPorts(
    Func<(string Rows, string Cols)> ReadRanges,
    Action<string> SetRows,
    Action<string> SetCols,
    Func<(decimal Width, decimal Height)> ReadSizes,
    Action<decimal> SetWidth,
    Action<decimal> SetHeight,
    Action<string> SetRangeError,
    Action<bool> SetWidthMixed,
    Action<bool> SetHeightMixed,
    Func<IDisposable> SuppressManualSizing,
    Func<IDisposable> SuppressUndo,
    Action UpdateRangeStatus);

internal readonly record struct SelectionInspectorPorts(
    Func<long> NextSelectionRevision,
    Func<bool> HasMultiSelection,
    Action<bool> CancelCad,
    Action<bool> CancelRegular,
    Func<int, PadInspectorSnapshot?> BuildCad,
    Func<int, PadInspectorSnapshot?> BuildRegular,
    Action<int, long> QueueCad,
    Action<int, long> QueueRegular,
    Action<PadInspectorSnapshot?> SetSnapshot);

internal readonly record struct SelectionLocatorPorts(
    Func<IReadOnlyCollection<CadPad>> ReadCadPads,
    Func<IReadOnlyCollection<RegularPad>> ReadRegularPads,
    Func<PadMatchResult> ReadMatch,
    Func<IReadOnlyDictionary<int, int>> ReadCadIcIndexes,
    Func<IReadOnlyDictionary<int, int>> ReadCadOutputDiffIndexes,
    Func<string> ReadQuery,
    Func<string, SelectionLocateParseResult> ParseQuery,
    Action<IReadOnlyCollection<int>, IReadOnlyCollection<int>> ApplySelection,
    Action<double> Focus,
    Action<string> SetStatus);

internal enum SelectionLocateTargetKind
{
    Auto,
    Cad,
    Regular,
    Diff,
}

internal readonly record struct SelectionLocateQuery(SelectionLocateTargetKind TargetKind, int Value, int? IcIndex);

internal readonly record struct SelectionLocateParseResult(bool Success, SelectionLocateQuery Query, string Error);

/// <summary>Keeps the existing Regular index membership and one-build preservation flag together. UI thread only.</summary>
internal sealed class SelectionState
{
    internal HashSet<int> RegularIndices { get; } = new();
    internal bool SuppressClearOnRebuild { get; set; }
}
