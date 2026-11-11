// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Sizing;

internal readonly record struct ManualRangePlan(
    bool HasError,
    string Error,
    bool SyncSelection,
    bool ApplySizing,
    IReadOnlyList<int> Rows,
    IReadOnlyList<int> Cols,
    decimal? Width,
    decimal? Height);

internal readonly record struct SizingMutationOutcome(bool Success, string? Error, Action Restore);

internal delegate bool ManualRangeParser(string input, out List<int> indices, out string error);
internal delegate void SizingLog(string message, params object?[] args);

internal readonly record struct ManualInputPorts(
    Action<string> Changing,
    Action<string> Changed,
    Func<bool> IsLoading,
    Func<bool> IsApplySuppressed,
    Action<decimal, decimal, Action<decimal>, string> TrackDecimalUndo,
    Action<string, string, Action<string>, string> TrackStringUndo);

internal readonly record struct ManualRangePorts(
    ManualRangeParser ParseRows,
    ManualRangeParser ParseCols,
    Func<IEnumerable<int>, string> FormatRanges,
    Func<int, int> ToDisplayRow,
    Func<RegularGrid, string, string, decimal, decimal, ManualRangePlan> Evaluate,
    Action ClearSelection,
    Action<IReadOnlyList<int>, IReadOnlyList<int>> SelectRange);

internal readonly record struct SizingEditPorts(
    Action ApplyUiSettings,
    Func<IReadOnlyList<int>, IReadOnlyList<int>, decimal?, decimal?, SizingMutationOutcome> Apply,
    Func<IReadOnlyList<int>, IReadOnlyList<int>, SizingMutationOutcome> Reset,
    Func<Dictionary<int, double>> CustomValues,
    Action<Action, string> PushUndo,
    Func<bool, Task> RebuildAsync,
    Action PreserveSelection,
    Action MarkUnsaved,
    Action<string> SetStatus,
    SizingLog Debug,
    SizingLog Info,
    SizingLog Warn);

internal readonly record struct ManualSizingPorts(
    Func<RegularGrid?> Grid,
    Func<decimal> XChannels,
    Func<decimal> YChannels,
    Func<decimal> ActiveAreaWidth,
    Func<decimal> ActiveAreaHeight,
    ManualInputPorts Input,
    ManualRangePorts Range,
    SizingEditPorts Edit);
