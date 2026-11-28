// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Existing workflow operations and result owners used by ordered dispatch.</summary>
internal interface IWorkflowDispatchPort
{
    WorkflowStateSnapshot BuildWorkflowStateSnapshot();
    string? StatusText { get; }
    bool CanAdvanceTo(WorkflowStepId step);
    void ApplyExpansion(WorkflowStepExpansionState expansion);
    void SetStatus(string message);
    void InvalidateCanvas();
    void ClearStep1Result();
    void ClearStep2Result();
    void ClearStep3Result();
    void ClearStep4Result();
    void ClearStep5Result();
    void RefreshStep3Preview();
    Task MatchAsync();
    Task AutoDetectFreeformsAsync();
    Task AnalyzeIndexMappingAsync();
}
