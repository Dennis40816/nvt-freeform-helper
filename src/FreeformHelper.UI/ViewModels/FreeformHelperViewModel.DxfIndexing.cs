namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool HasCadOutputFwDiffIndexOverride(int cadPadId)
    {
        return _projectFile.CadOutputFwDiffIndexOverrides.ContainsKey(cadPadId);
    }

    public int? GetCadOutputFwDiffIndexOverride(int cadPadId)
    {
        return _projectFile.CadOutputFwDiffIndexOverrides.TryGetValue(cadPadId, out var index)
            ? index
            : null;
    }

    public void SetCadOutputFwDiffIndexOverride(int cadPadId, int targetIndex)
    {
        var clampedIndex = Math.Clamp(targetIndex, 0, 1_000_000);
        var overrides = _projectFile.CadOutputFwDiffIndexOverrides;
        var targetIc = _cadIcIndexByCadId.TryGetValue(cadPadId, out var inferredIc)
            ? inferredIc
            : (int?)null;
        var hadPrevious = overrides.TryGetValue(cadPadId, out var previousIndex);
        if (hadPrevious && previousIndex == clampedIndex)
        {
            return;
        }

        var replaced = overrides
            .Where(kv =>
                kv.Key != cadPadId &&
                kv.Value == clampedIndex &&
                IsSameIcForOverrideConflict(kv.Key, targetIc))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var cadId in replaced.Keys)
        {
            overrides.Remove(cadId);
        }

        overrides[cadPadId] = clampedIndex;
        PushUndo(() =>
        {
            if (hadPrevious)
            {
                overrides[cadPadId] = previousIndex;
            }
            else
            {
                overrides.Remove(cadPadId);
            }

            foreach (var pair in replaced)
            {
                overrides[pair.Key] = pair.Value;
            }

            UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        }, "CAD Output FW Diff override");

        UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        InvalidateDownstreamFromStep4(showStatus: false);
        NotifySimulationWorkspaceSourceChanged();
        MarkUnsaved();
        SetStatus(
            replaced.Count == 0
                ? BuildOverrideSavedMessage(cadPadId, clampedIndex, targetIc)
                : $"{BuildOverrideSavedMessage(cadPadId, clampedIndex, targetIc)} Replaced {replaced.Count} conflicting override(s) in same IC.");
    }

    public void ClearCadOutputFwDiffIndexOverride(int cadPadId)
    {
        var overrides = _projectFile.CadOutputFwDiffIndexOverrides;
        if (!overrides.TryGetValue(cadPadId, out var previousIndex))
        {
            SetStatus($"CAD Output FW Diff override clear: CAD {cadPadId} has no override.");
            return;
        }

        overrides.Remove(cadPadId);
        PushUndo(() =>
        {
            overrides[cadPadId] = previousIndex;
            UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        }, "CAD Output FW Diff override clear");

        UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        InvalidateDownstreamFromStep4(showStatus: false);
        NotifySimulationWorkspaceSourceChanged();
        MarkUnsaved();
        SetStatus($"CAD Output FW Diff override cleared: CAD {cadPadId}.");
    }

    public bool IsCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
    {
        if (!_cadIcIndexByCadId.TryGetValue(cadPadId, out var icIndex))
        {
            return false;
        }

        return _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.TryGetValue(icIndex, out var anchorCadId) &&
               anchorCadId == cadPadId;
    }

    public void SetCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
    {
        if (!_cadIcIndexByCadId.TryGetValue(cadPadId, out var icIndex))
        {
            SetStatus($"CAD Output FW Diff anchor ignored: CAD {cadPadId} is not visible.");
            return;
        }

        if (_projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.TryGetValue(icIndex, out var currentAnchor) &&
            currentAnchor == cadPadId)
        {
            return;
        }

        CadOutputFwDiffIndexAnchorCadId = cadPadId;
    }

    public void ClearCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
    {
        if (!_cadIcIndexByCadId.TryGetValue(cadPadId, out var icIndex))
        {
            SetStatus($"CAD Output FW Diff anchor clear ignored: CAD {cadPadId} is not visible.");
            return;
        }

        if (!_projectFile.CadOutputFwDiffIndexAnchorCadPadByIc.Remove(icIndex))
        {
            SetStatus($"CAD Output FW Diff anchor clear: IC {icIndex + 1} has no anchor.");
            return;
        }

        UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        InvalidateDownstreamFromStep4(showStatus: false);
        NotifySimulationWorkspaceSourceChanged();
        MarkUnsaved();
        SetStatus($"CAD Output FW Diff anchor cleared: IC {icIndex + 1} uses scan-order first CAD.");
    }

    private bool IsSameIcForOverrideConflict(int cadPadId, int? targetIc)
    {
        if (!targetIc.HasValue)
        {
            return true;
        }

        if (_cadIcIndexByCadId.TryGetValue(cadPadId, out var otherIc))
        {
            return otherIc == targetIc.Value;
        }

        return false;
    }

    private static string BuildOverrideSavedMessage(int cadPadId, int index, int? targetIc)
    {
        return targetIc.HasValue
            ? $"CAD Output FW Diff override saved: IC {targetIc.Value + 1}, CAD {cadPadId} -> {index}."
            : $"CAD Output FW Diff override saved: CAD {cadPadId} -> {index}.";
    }
}
