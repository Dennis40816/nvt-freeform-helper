// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels;

internal delegate void IndexMappingLog(string message, params object[] arguments);

internal delegate bool IndexMappingLocate(
    IReadOnlyCollection<int> cadIds,
    IReadOnlyCollection<int> regularIndices,
    bool focusSelection,
    string notVisibleStatus,
    Func<int, int, string> successStatusBuilder);

internal readonly record struct IndexMappingNotifications(Action<string> Changing, Action<string> Changed);

// Readers resolve the current project and accepted inputs at invocation time.
internal readonly record struct IndexAssignmentInputs(
    Func<ProjectFile> Project,
    Func<RegularGrid?> Grid,
    Func<IReadOnlyList<CadPad>> CadPads,
    Func<IReadOnlyList<RegularPad>> RegularPads,
    Func<IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>>> MatchLinks,
    Func<IReadOnlySet<int>?> ActiveMask,
    Func<ScanOrder> ScanOrder,
    Func<CadOutputFwDiffAutoMode> AutoMode,
    Func<IReadOnlyCollection<int>> SelectedCadIds);

internal readonly record struct IndexMappingEffects(
    Action<string> Status,
    Action MarkUnsaved,
    Action InvalidateDownstream,
    Action NotifySourceChanged,
    Action<Action, string> PushUndo,
    IndexMappingLocate Locate,
    IndexMappingLog Info,
    IndexMappingLog Warn);

internal readonly record struct IndexAssignmentPorts(
    Func<DxfDuplicateGroupInputs> ReadSnapshot,
    Action<ObservableCollection<CadPad>> SetCadPads,
    Action<CadPadSet> SetFilteredCad,
    Action BumpCadFingerprint,
    Action BumpIndexFingerprint,
    Action InvalidateSnapshot,
    Action<ObservableCollection<int>> SetOverrideHints,
    Action<ObservableCollection<int>> SetAnchorHints,
    Action<string> SetAssignmentSummary,
    Func<decimal> ReadAnchor,
    Action<decimal> SetAnchor,
    Func<IDisposable> SuppressAnchorChange,
    Func<IDisposable> SuppressUndo);

internal readonly record struct IndexMappingAnalysisInput(CadPadSet Cad, RegularGrid Grid);

internal readonly record struct IndexMappingPorts(
    Func<Action, IndexMappingAnalysisInput?> PrepareInputs,
    Func<Action, Func<Action<double>, Task>, Task> RunAsync,
    Action ApplyUiSettings,
    Func<CadPadSet, RegularGrid, Action<double>, Task<DxfRegularMappingResult>> AnalyzeAsync,
    Func<bool> CanOpenReport,
    Func<DxfRegularMappingReport, Task> OpenReportAsync,
    Action CompleteWorkflow);
