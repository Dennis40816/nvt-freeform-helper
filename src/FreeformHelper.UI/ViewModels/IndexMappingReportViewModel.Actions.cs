namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingReportViewModel
{
    private List<IndexMappingDecisionRowViewModel> GetSelectedSegmentRepairRows()
    {
        if (SelectedDecision is null || !SelectedDecision.HasSegmentScope)
        {
            return new List<IndexMappingDecisionRowViewModel>();
        }

        return _allDecisionSeedRows
            .Where(row =>
                row.CanApplyDiffOverride &&
                row.IcIndex == SelectedDecision.IcIndex &&
                row.RowIndex == SelectedDecision.RowIndex &&
                row.SegmentIndex == SelectedDecision.SegmentIndex)
            .ToList();
    }

    private List<IndexMappingDecisionRowViewModel> GetVisibleRepairRows()
    {
        return DecisionRows
            .Where(static row => row.CanApplyDiffOverride)
            .ToList();
    }

    private string BuildSelectedSegmentPreviewText()
    {
        if (SelectedDecision is null)
        {
            return "Segment preview: select a row.";
        }

        if (!SelectedDecision.HasSegmentScope)
        {
            return "Segment preview: selected row has no segment metadata.";
        }

        var repairRows = GetSelectedSegmentRepairRows();
        var memberCount = Math.Max(SelectedDecision.SegmentMemberCount, repairRows.Count);
        return $"Segment preview: {SelectedDecision.SegmentKey}, repair rows {repairRows.Count}/{memberCount}.";
    }

    private void MoveDecision(int direction)
    {
        var navigationRows = DecisionRows.Count > 0 ? DecisionRows : AggregateDecisionRows;
        if (navigationRows.Count == 0)
        {
            ActionStatus = "No diagnostic rows.";
            return;
        }

        var currentIndex = -1;
        if (SelectedDecision is not null)
        {
            for (var i = 0; i < navigationRows.Count; i++)
            {
                if (ReferenceEquals(navigationRows[i], SelectedDecision))
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        if (currentIndex < 0)
        {
            currentIndex = direction >= 0 ? -1 : 0;
        }

        var targetIndex = Math.Clamp(currentIndex + direction, 0, navigationRows.Count - 1);
        SelectedDecision = navigationRows[targetIndex];
    }

    private void SelectDecision(IndexMappingDecisionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedDecision = row;
        if (_locateTarget is not null && row.CanLocate)
        {
            LocateSelectedDecision();
        }
    }

    private void LocateSelectedDecision()
    {
        if (SelectedDecision is null)
        {
            ActionStatus = "Select a row first.";
            return;
        }

        if (!SelectedDecision.CanLocate)
        {
            ActionStatus = "Selected row has no locate target.";
            return;
        }

        if (_locateTarget is null)
        {
            ActionStatus = "Locate handler not available.";
            return;
        }

        var located = _locateTarget(SelectedDecision.CadPadId, SelectedDecision.LocateRegularPadIndex);
        ActionStatus = located ? "Diagnostic row focused on canvas." : "Diagnostic target is not visible.";
    }

    private void ApplyOverrideForSelectedDecision()
    {
        if (SelectedDecision is null)
        {
            ActionStatus = "Select a row first.";
            return;
        }

        if (!SelectedDecision.CanApplyOverride || SelectedDecision.CadPadId is not int cadId || SelectedDecision.ApplyRegularPadIndex is not int regularPadIndex)
        {
            ActionStatus = "Selected row cannot create an override.";
            return;
        }

        if (_applyOverride is null)
        {
            ActionStatus = "Override handler not available.";
            return;
        }

        var applied = _applyOverride(cadId, regularPadIndex);
        if (applied)
        {
            SelectedDecision.SetOverride(regularPadIndex);
            SelectedIssue?.SetOverride(regularPadIndex);
            ActionStatus = "Mapping override applied.";
        }
        else
        {
            ActionStatus = "Failed to apply mapping override.";
        }
    }

    private void ApplyDiffOverrideForSelectedDecision()
    {
        if (SelectedDecision is null)
        {
            ActionStatus = "Select a row first.";
            return;
        }

        if (!SelectedDecision.CanApplyDiffOverride ||
            SelectedDecision.CadPadId is not int cadId ||
            SelectedDecision.ApplyDiffIndex is not int diffIndex)
        {
            ActionStatus = "Selected row has no diff repair suggestion.";
            return;
        }

        if (_applyDiffOverride is null)
        {
            ActionStatus = "Diff override handler not available.";
            return;
        }

        var applied = _applyDiffOverride(cadId, diffIndex);
        ActionStatus = applied
            ? "Diff repair override applied."
            : "Failed to apply diff repair override.";
    }

    private void ApplySegmentDiffOverridesForSelectedDecision()
    {
        if (SelectedDecision is null)
        {
            ActionStatus = "Select a row first.";
            return;
        }

        if (!SelectedDecision.HasSegmentScope)
        {
            ActionStatus = "Selected row has no segment metadata.";
            return;
        }

        if (_applyDiffOverride is null)
        {
            ActionStatus = "Diff override handler not available.";
            return;
        }

        var segmentRows = GetSelectedSegmentRepairRows();
        if (segmentRows.Count == 0)
        {
            ActionStatus = "Selected segment has no diff repair suggestions.";
            return;
        }

        var appliedCount = 0;
        foreach (var row in segmentRows)
        {
            if (row.CadPadId is not int cadId || row.ApplyDiffIndex is not int diffIndex)
            {
                continue;
            }

            if (_applyDiffOverride(cadId, diffIndex))
            {
                appliedCount++;
            }
        }

        ActionStatus = $"Segment diff repair applied for {appliedCount}/{segmentRows.Count} rows.";
    }

    private void ApplyCadOutputFwDiffOverrides()
    {
        if (_applyDiffOverride is null)
        {
            ActionStatus = "Diff override handler not available.";
            return;
        }

        var visibleRows = GetVisibleRepairRows();
        if (visibleRows.Count == 0)
        {
            ActionStatus = "No visible rows have diff repair suggestions.";
            return;
        }

        var appliedCount = 0;
        foreach (var row in visibleRows)
        {
            if (row.CadPadId is not int cadId || row.ApplyDiffIndex is not int diffIndex)
            {
                continue;
            }

            if (_applyDiffOverride(cadId, diffIndex))
            {
                appliedCount++;
            }
        }

        ActionStatus = $"Visible diff repairs applied for {appliedCount}/{visibleRows.Count} rows.";
    }

    private void ClearOverrideForSelectedDecision()
    {
        if (SelectedDecision is null)
        {
            ActionStatus = "Select a row first.";
            return;
        }

        if (!SelectedDecision.CanClearOverride || SelectedDecision.CadPadId is not int cadId)
        {
            ActionStatus = "Selected row has no CAD override to clear.";
            return;
        }

        if (_clearOverride is null)
        {
            ActionStatus = "Override clear handler not available.";
            return;
        }

        var cleared = _clearOverride(cadId);
        if (cleared)
        {
            SelectedDecision.SetOverride(null);
            SelectedIssue?.SetOverride(null);
            ActionStatus = "Mapping override cleared.";
        }
        else
        {
            ActionStatus = "Failed to clear mapping override.";
        }
    }
}
