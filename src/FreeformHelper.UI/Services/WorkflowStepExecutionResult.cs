// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

internal readonly record struct WorkflowStepExecutionResult(
    bool IsSupported,
    bool IsReady,
    WorkflowStepId Step,
    WorkflowStateSnapshot Snapshot,
    string? StatusText)
{
    public static WorkflowStepExecutionResult Unsupported(
        WorkflowStepId step,
        WorkflowStateSnapshot snapshot,
        string? statusText)
    {
        return new WorkflowStepExecutionResult(
            IsSupported: false,
            IsReady: false,
            Step: step,
            Snapshot: snapshot,
            StatusText: statusText);
    }
}
