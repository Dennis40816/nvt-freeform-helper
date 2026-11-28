// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Grid;

internal delegate bool GridMatchInputs(string missingStatus, out CadPadSet cad, out RegularGrid grid);
internal delegate void GridLog(string message, params object?[] args);

internal readonly record struct GridStatusPorts(
    Action<string> ReportProgress,
    Action<string> ReportSuccess,
    Action<string, Exception> ReportError);

internal readonly record struct GridStatePorts(
    Func<RegularGrid?> Grid,
    Action<RegularGrid?> SetGrid,
    Func<bool> HasCad,
    Func<int> RegularPadCount,
    Func<IReadOnlyList<CadPad>> VisibleCad,
    Func<bool> IsLoading,
    Func<bool> HasSelectedCad,
    Func<IReadOnlyCollection<int>> SelectedRegularIndices,
    Func<PadMatchResult> Match,
    Action<PadMatchResult> SetMatch);

internal readonly record struct GridEffectPorts(
    Action ApplyUiSettings,
    Action BumpGridFingerprint,
    Action BumpIndexFingerprint,
    Action InvalidateCompensation,
    Action RefreshPreview,
    Action SourceChanged,
    Action InvalidateCanvas,
    Action FitCanvas,
    Action MarkUnsaved,
    Action<string> SetStatus,
    Action<string> Changed,
    GridLog Info,
    GridLog Debug);

internal readonly record struct GridWorkflowPorts(
    Func<bool> EnsureStep2,
    Action<bool, bool> CompleteMatch,
    Action ChangeFreeform,
    Action<bool> CompleteFreeform,
    Action ResetFromMatch);

internal readonly record struct GridMatchPorts(
    GridMatchInputs FilteredInputs,
    GridMatchInputs ActiveInputs,
    Func<string, GridStatusPorts> CreateStatus,
    Func<Action, Func<Action<double>, Task>, Action<Exception>, Task> RunMatchAsync,
    Func<Func<Task>, Task> RunAutoDetectAsync,
    Func<CadPadSet, RegularGrid, Action<double>, Task<PadMatchResult>> MatchAsync,
    Func<MatchingSettings> MatchingSettings,
    Func<bool> AutoReplayStep2,
    Func<CadPadSet, RegularGrid, MatchingSettings, Task<IReadOnlyDictionary<int, FreeformType>>> BuildAssignmentsAsync,
    Action<RegularGrid, IReadOnlyDictionary<int, FreeformType>> ApplyAssignments,
    Action<RegularGrid> ApplyOverrides,
    Func<RegularGrid, IReadOnlyCollection<int>, FreeformType, int> ApplyManual,
    Action<IEnumerable<RegularPad>, FreeformType> SyncOverrides,
    Action<IReadOnlyList<CadPad>> RebuildIndexes,
    Action<IReadOnlyList<PadMatchLink>> SetCanvasLinks,
    Action<FreeformAutoDetectResult> SetStats,
    Func<string> StatsSummary,
    Action<Exception> LogMatchError);

internal readonly record struct GridBuildOutcome(
    bool Success, RegularGrid? Grid, string StatusText, string? CanvasHint, string? DxfRegularSourceHint);

internal readonly record struct GridBuildPorts(
    Func<Func<Task>, Task> RunAsync,
    Func<Task<GridBuildOutcome>> BuildAsync,
    Action<string> SetCanvasHint,
    Action<string> SetSourceHint,
    Action<ObservableCollection<RegularPad>> SetRegularPads,
    Action<RegularGrid> RefreshMask,
    Action ClearMask,
    Action<IReadOnlyList<CadPad>> UpdateIndexes,
    Action RefreshSourceHint,
    Action<string> ResetStats,
    Action<IReadOnlyList<PadMatchLink>> SetCanvasLinks);

internal readonly record struct GridSelectionPorts(
    Func<bool> PreserveSelection,
    Action ConsumePreservation,
    Action ClearSelection,
    Action RefreshSummary);

internal readonly record struct GridStartupPorts(
    Func<bool> InitialGridBuiltMarked,
    Action MarkInitialGridBuilt,
    Action<string, string> MarkPerformance,
    Func<bool> FitAfterNextBuild,
    Action ConsumeFit);

internal readonly record struct GridMatchingPorts(
    GridStatePorts State, GridEffectPorts Effects, GridWorkflowPorts Workflow, GridMatchPorts Match);

internal readonly record struct GridRebuildPorts(
    GridStatePorts State, GridEffectPorts Effects, GridWorkflowPorts Workflow,
    GridBuildPorts Build, GridSelectionPorts Selection, GridStartupPorts Startup);
