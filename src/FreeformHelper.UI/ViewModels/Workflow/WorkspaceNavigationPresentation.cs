// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Windows.Input;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Computes header presentation from the current result and navigation owners.</summary>
internal sealed class WorkspaceNavigationPresentation(
    IWorkspaceNavigationSource source,
    Action<WorkspaceWorkStep> moveToStep,
    Action<string> propertyChanged)
{
    private WorkspaceWorkStep? WorkspaceActionStep => source.HasCad
        ? source.WorkStep
        : null;

    public string WorkspacePrimaryActionText => WorkspaceActionStep switch
    {
        null => "Open DXF",
        WorkspaceWorkStep.Step1Match => "Match overlaps",
        WorkspaceWorkStep.Step2Freeform => "Detect freeform",
        _ => "Export",
    };

    public ICommand WorkspacePrimaryActionCommand => WorkspaceActionStep switch
    {
        null => source.OpenDxfCommand,
        WorkspaceWorkStep.Step1Match => source.MatchCommand,
        WorkspaceWorkStep.Step2Freeform => source.AutoDetectFreeformsCommand,
        _ => source.ExportNotchCommand,
    };

    public string WorkspaceWorkHint => WorkspaceActionStep switch
    {
        null => "Open DXF or use File > Load Project to begin.",
        WorkspaceWorkStep.Step1Match => "Step 1 · Geometry match",
        WorkspaceWorkStep.Step2Freeform => "Step 2 · Freeform tagging",
        _ => "Step 3 preview → Step 5 · Notch export",
    };

    public string WorkspaceMatchSummary => source.HasStep1Result
        ? "Overlap links are ready."
        : "Build CAD↔Regular overlap links with Match overlaps.";

    public string ManualOverrideHint => source.SelectedRegularCount == 0
        ? "Select a Regular pad first."
        : "Applies to selected Regular; Step 1 links stay unchanged.";

    public string ToFullPreviewToggleHint => source.CanShowToFullPreviewToggle
        ? "Display only; show or hide the To Full overlay."
        : "Enable To Full in Settings > Step 3 to show its preview.";

    public string DxfCheckHeaderText => source.IsCheckingDxfOverlap ? "Checking…"
        : source.HasDxfOverlapIssues ? $"{source.DxfOverlapIssueCount} overlaps"
        : source.IsDxfOverlapNotRun ? "Not checked" : "Checked";

    public string ValidationTraceHint => source.HasNotchValidationItems
        ? "Trace ready; expand a category to inspect its rows."
        : source.NotchValidationSummaryText;

    internal void NotifyWorkspaceLayoutChanged(string? propertyName)
    {
        if (propertyName is "CadPads" or "IsStep1Expanded" or "IsStep2Expanded"
            or "IsStep3Expanded" or "IsStep5Expanded")
        {
            propertyChanged(nameof(WorkspacePrimaryActionText));
            propertyChanged(nameof(WorkspacePrimaryActionCommand));
            propertyChanged(nameof(WorkspaceWorkHint));
        }
        if (propertyName == "MatchLinksForCanvas")
        {
            propertyChanged(nameof(WorkspaceMatchSummary));
        }
        if (propertyName == "SelectionSummaryResult")
        {
            propertyChanged(nameof(ManualOverrideHint));
        }
        if (propertyName is "NotchValidationSummaryText" or "HasNotchValidationItems")
        {
            propertyChanged(nameof(ValidationTraceHint));
        }
        if (propertyName == "CanShowToFullPreviewToggle")
        {
            propertyChanged(nameof(ToFullPreviewToggleHint));
        }
        if (propertyName is "DxfOverlapResult" or "IsCheckingDxfOverlap")
        {
            propertyChanged(nameof(DxfCheckHeaderText));
        }
    }

    internal void SelectWorkspaceWorkStep(WorkspaceWorkStep step)
    {
        source.WorkStep = step;
        moveToStep(step);
    }
}
