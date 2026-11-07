using System.Collections.ObjectModel;
using System.Windows.Input;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages the items
/// displayed in the workspace header, particularly toggles for various view options.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Initializes the collection of <see cref="WorkspaceHeaderItem"/>s that appear in the
    /// application's header/menu. These items typically represent view toggles.
    /// </summary>
    private void InitializeWorkspaceHeaderItems()
    {
        _workspaceHeaderItemMap.Clear(); // Clear existing map to avoid duplicates on reinitialization.
        WorkspaceHeaderItems = new ObservableCollection<WorkspaceHeaderItem>
        {
            CreateWorkspaceToggle("highlightUnmatched", "Highlight unmatched regular", () => HighlightUnmatched, v => HighlightUnmatched = v),
            CreateWorkspaceToggle("highlightFreeform", "Highlight freeform regular", () => HighlightFreeform, v => HighlightFreeform = v),
            // Add other workspace header items here as needed.
        };
    }

    /// <summary>
    /// Helper method to create a <see cref="WorkspaceHeaderItem"/> that acts as a boolean toggle
    /// and registers it in the internal map.
    /// </summary>
    /// <param name="id">A unique identifier for the item.</param>
    /// <param name="label">The display label for the item.</param>
    /// <param name="getter">A function to get the current boolean state of the item.</param>
    /// <param name="setter">An action to set the boolean state of the item.</param>
    /// <returns>A new <see cref="WorkspaceHeaderItem"/> instance.</returns>
    private WorkspaceHeaderItem CreateWorkspaceToggle(string id, string label, Func<bool> getter, Action<bool> setter)
    {
        var item = new WorkspaceHeaderItem(id, label, getter(), setter);
        _workspaceHeaderItemMap[id] = item; // Store in map for easy lookup.
        return item;
    }

    /// <summary>
    /// Synchronizes the checked state of a <see cref="WorkspaceHeaderItem"/> with an external boolean value.
    /// This is typically called when the underlying ViewModel property changes to update the UI item.
    /// </summary>
    /// <param name="id">The unique identifier of the <see cref="WorkspaceHeaderItem"/> to sync.</param>
    /// <param name="value">The boolean value to set the item's checked state to.</param>
    private void SyncWorkspaceToggle(string id, bool value)
    {
        if (_workspaceHeaderItemMap.TryGetValue(id, out var item))
        {
            item.SetCheckedFromSource(value); // Update the item's checked state without triggering its own setter logic.
        }
    }
    private WorkspaceWorkStep? WorkspaceActionStep => GetWorkflowStateSnapshot().HasCad
        ? _inspectorPanel.WorkStep
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
        null => OpenDxfCommand,
        WorkspaceWorkStep.Step1Match => MatchCommand,
        WorkspaceWorkStep.Step2Freeform => AutoDetectFreeformsCommand,
        _ => ExportNotchCommand,
    };

    public string WorkspaceWorkHint => WorkspaceActionStep switch
    {
        null => "Open DXF or use File > Load Project to begin.",
        WorkspaceWorkStep.Step1Match => "Step 1 · Geometry match",
        WorkspaceWorkStep.Step2Freeform => "Step 2 · Freeform tagging",
        _ => "Step 3 preview → Step 5 · Notch export",
    };

    public string WorkspaceMatchSummary => GetWorkflowStateSnapshot().HasStep1Result
        ? "Overlap links are ready."
        : "Build CAD↔Regular overlap links with Match overlaps.";

    public string ManualOverrideHint => SelectionSummaryResult.RegularCount == 0
        ? "Select a Regular pad first."
        : "Applies to selected Regular; Step 1 links stay unchanged.";

    public string ToFullPreviewToggleHint => CanShowToFullPreviewToggle
        ? "Display only; show or hide the To Full overlay."
        : "Enable To Full in Settings > Step 3 to show its preview.";

    public string DxfCheckHeaderText => IsCheckingDxfOverlap ? "Checking…"
        : HasDxfOverlapIssues ? $"{DxfOverlapIssueCount} overlaps"
        : DxfOverlapSummary == DxfOverlapResult.NotRunSummary ? "Not checked" : "Checked";

    public string DxfEditsHeaderText =>
        $"Hidden {DeletedCadPadCount} · Merged {CombinedCadPadCount}";

    public string DxfRotationScopeHint
    {
        get
        {
            if (!IsDxfEditRotationLayerScope || _cad is null) return "Applies to selected CAD pads.";
            var targets = GetRotationTargetCadIds(_cad);
            return targets.SetEquals(_selectedCadIds)
                ? "Applies to the chosen layer."
                : $"Layer scope · {targets.Count} target pads; selection is unchanged.";
        }
    }

    public string ValidationTraceHint => HasNotchValidationItems
        ? "Trace ready; expand a category to inspect its rows."
        : NotchValidationSummaryText;

    private void NotifyWorkspaceLayoutChanged(string? propertyName)
    {
        if (propertyName is nameof(CadPads) or nameof(IsStep1Expanded) or nameof(IsStep2Expanded)
            or nameof(IsStep3Expanded) or nameof(IsStep5Expanded))
        {
            OnPropertyChanged(nameof(WorkspacePrimaryActionText));
            OnPropertyChanged(nameof(WorkspacePrimaryActionCommand));
            OnPropertyChanged(nameof(WorkspaceWorkHint));
        }
        if (propertyName == nameof(MatchLinksForCanvas))
        {
            OnPropertyChanged(nameof(WorkspaceMatchSummary));
        }
        if (propertyName == nameof(SelectionSummaryResult))
        {
            OnPropertyChanged(nameof(ManualOverrideHint));
        }
        if (propertyName is nameof(NotchValidationSummaryText) or nameof(HasNotchValidationItems))
        {
            OnPropertyChanged(nameof(ValidationTraceHint));
        }
        if (propertyName == nameof(CanShowToFullPreviewToggle))
        {
            OnPropertyChanged(nameof(ToFullPreviewToggleHint));
        }
        if (propertyName is nameof(DxfOverlapResult) or nameof(IsCheckingDxfOverlap))
        {
            OnPropertyChanged(nameof(DxfCheckHeaderText));
        }
        if (propertyName is nameof(DeletedCadPadCount) or nameof(CombinedCadPadCount))
        {
            OnPropertyChanged(nameof(DxfEditsHeaderText));
        }
        if (propertyName is nameof(SelectedDxfEditRotationScopeOption) or nameof(DxfEditRotationLayerName)
            or nameof(CanRotateSelectedCadPads))
        {
            OnPropertyChanged(nameof(DxfRotationScopeHint));
        }
    }

    private void SelectWorkspaceWorkStep(WorkspaceWorkStep step)
    {
        _inspectorPanel = _inspectorPanel with { WorkStep = step };
        MoveToWorkflowStep(step);
    }

    partial void OnIsStep1ExpandedChanged(bool value)
    {
        if (value) SelectWorkspaceWorkStep(WorkspaceWorkStep.Step1Match);
    }

    partial void OnIsStep2ExpandedChanged(bool value)
    {
        if (value) SelectWorkspaceWorkStep(WorkspaceWorkStep.Step2Freeform);
    }

    partial void OnIsStep3ExpandedChanged(bool value)
    {
        if (value) SelectWorkspaceWorkStep(WorkspaceWorkStep.Step3NotchPreview);
    }

    partial void OnIsStep5ExpandedChanged(bool value)
    {
        if (value) SelectWorkspaceWorkStep(WorkspaceWorkStep.Step5Export);
    }

}
