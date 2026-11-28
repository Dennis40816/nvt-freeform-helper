// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

internal sealed record SimulationSessionInputs(CadPadSet Cad, RegularGrid Grid);

/// <summary>References accepted generation outputs without recalculating or copying their contents.</summary>
internal sealed record SimulationSessionGeneration(
    NotchTable Table,
    RegularGrid RawGrid,
    RegularGrid CadOutputFwDiffGrid,
    ProjectSettings ExportSettings,
    int SourceRevision);

internal readonly record struct SimulationSessionDisplay(bool ShowRegular, bool HighlightUnmatched, bool HighlightFreeform);

internal sealed record SimulationDuplicateDiffWarning(int IcIndex, int DiffIndex, IReadOnlyList<int> CadPadIds, int GroupCount);

/// <summary>Typed access to the host's inputs, generation pipeline, and UI operation boundary.</summary>
internal sealed record SimulationSessionPorts(
    Func<bool, SimulationSessionInputs?> ReadInputs,
    Func<WorkflowDataSnapshot> ReadWorkflowSnapshot,
    Func<SimulationSessionInputs, WorkflowDataSnapshot, bool, Action<string>?, Task<SimulationSessionGeneration?>> GenerateAsync,
    Action<WorkflowDataSnapshot> RefreshDuplicateGroups,
    Func<SimulationDuplicateDiffWarning?> ReadDuplicateWarning,
    Func<SimulationSessionDisplay> ReadDisplay,
    Func<string, UiOperationStatusReporter.Scope> CreateStatus,
    Func<string, Func<Task>, Action<Exception>, bool, Task> RunAsync,
    Action<string> SetStatus);
