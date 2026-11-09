using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public int? GetCadOutputFwDiffIndexOverride(int cadPadId)
    {
        return _projectFile.CadOutputFwDiffIndexOverrides.TryGetValue(cadPadId, out var index)
            ? index
            : null;
    }

    public void SetCadOutputFwDiffIndexOverride(int cadPadId, int targetIndex)
    {
        var overrides = _projectFile.CadOutputFwDiffIndexOverrides;
        var result = DxfOverrideService.SetOverride(cadPadId, targetIndex, overrides, _cadIcIndexByCadId);
        if (!result.Changed)
        {
            return;
        }

        PushUndo(() =>
        {
            if (result.HadPrevious)
            {
                overrides[cadPadId] = result.PreviousIndex;
            }
            else
            {
                overrides.Remove(cadPadId);
            }

            foreach (var pair in result.ReplacedOverrides)
            {
                overrides[pair.Key] = pair.Value;
            }

            UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        }, "CAD Output FW Diff override");

        UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        InvalidateDownstreamFromStep4(showStatus: false);
        NotifySimulationWorkspaceSourceChanged();
        MarkUnsaved();
        SetStatus(result.Message);
    }

    public void ClearCadOutputFwDiffIndexOverride(int cadPadId)
    {
        var overrides = _projectFile.CadOutputFwDiffIndexOverrides;
        var result = DxfOverrideService.ClearOverride(cadPadId, overrides);
        if (!result.HadPrevious)
        {
            SetStatus($"CAD Output FW Diff override clear: CAD {cadPadId} has no override.");
            return;
        }

        PushUndo(() =>
        {
            overrides[cadPadId] = result.PreviousIndex;
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
        return DxfOverrideService.IsAnchor(cadPadId, _cadIcIndexByCadId, _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc);
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

        // Keep the stored selection in step with the project anchors, so selecting the same CAD again commits it.
        if (CadOutputFwDiffIndexAnchorCadId == cadPadId)
        {
            using (_settingsSuppression.EnterDxfIndexAnchorChange())
            {
                using (_undoSuppression.Enter())
                {
                    CadOutputFwDiffIndexAnchorCadId = -1;
                }
            }
        }

        UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        InvalidateDownstreamFromStep4(showStatus: false);
        NotifySimulationWorkspaceSourceChanged();
        MarkUnsaved();
        SetStatus($"CAD Output FW Diff anchor cleared: IC {icIndex + 1} uses scan-order first CAD.");
    }

    /// <summary>
    /// Gets a read-only list of <see cref="CadOutputFwDiffAutoModeOption"/> objects for Step 4 auto CAD Output FW Diff mode.
    /// </summary>
    public IReadOnlyList<CadOutputFwDiffAutoModeOption> CadOutputFwDiffAutoModeOptions { get; }

    /// <summary>
    /// Gets or sets the selected CAD auto CAD Output FW Diff assignment mode option.
    /// </summary>
    public CadOutputFwDiffAutoModeOption SelectedCadOutputFwDiffAutoModeOption
    {
        get => CadOutputFwDiffAutoModeOptions.FirstOrDefault(option => option.Value == CadOutputFwDiffAutoMode);
        set
        {
            if (_settingsSuppression.IsCadOutputFwDiffAutoModeSelectionSyncActive || value.Value == CadOutputFwDiffAutoMode)
            {
                return;
            }

            using (_settingsSuppression.EnterCadOutputFwDiffAutoModeSelectionSync())
            {
                CadOutputFwDiffAutoMode = value.Value;
            }

            if (!_isLoadingSettings)
            {
                ApplySettingsOrchestration(nameof(CadOutputFwDiffAutoMode));
            }

            OnPropertyChanged(nameof(SelectedCadOutputFwDiffAutoModeOption));
        }
    }

    // The four per-CAD-pad index tables are stored once. The members below keep the old names and
    // read through it: members that carry analyzer debt read them, and renaming them would change
    // their ledger fingerprints.
    private readonly CadIndexMaps _cadIndexMaps = new();

    internal CadIndexMaps CadIndexMaps => _cadIndexMaps;

    // Maps CAD pad ID -> Step 4 CAD output FW diff idx.
    private Dictionary<int, int> _cadOutputFwDiffIndexByCadId => _cadIndexMaps.OutputFwDiffIndexByCadId;

    // Maps CAD pad ID -> stable display index (L->R, T->B, 1-based).
    private Dictionary<int, int> _cadDisplayIndexByCadId => _cadIndexMaps.DisplayIndexByCadId;

    // Maps CAD pad ID -> inferred IC index (grid partition based).
    private Dictionary<int, int> _cadIcIndexByCadId => _cadIndexMaps.IcIndexByCadId;

    private IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> _latestCadOutputFwDiffAssignmentDecisionsByCadId
    {
        get => _cadIndexMaps.LatestAssignmentDecisionsByCadId;
        set => _cadIndexMaps.LatestAssignmentDecisionsByCadId = value;
    }

    public bool TryGetCadOutputFwDiffIndex(int cadPadId, out int dxfIndex)
    {
        return _cadOutputFwDiffIndexByCadId.TryGetValue(cadPadId, out dxfIndex);
    }

    public bool TryGetCadIcIndex(int cadPadId, out int icIndex)
    {
        return _cadIcIndexByCadId.TryGetValue(cadPadId, out icIndex);
    }

    public bool TryGetCadDisplayIndex(int cadPadId, out int displayIndex)
    {
        return _cadDisplayIndexByCadId.TryGetValue(cadPadId, out displayIndex);
    }

    private void SetLatestCadOutputFwDiffAssignmentDecisions(IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> decisions)
    {
        _latestCadOutputFwDiffAssignmentDecisionsByCadId = decisions;
        InvalidateWorkflowDataSnapshot();
    }

    private static List<CadOutputFwDiffAutoModeOption> BuildCadOutputFwDiffAutoModeOptions() =>
        new List<CadOutputFwDiffAutoModeOption>
        {
            new(
                CadOutputFwDiffAutoMode.BestMatchDirect,
                "Geometry seed",
                "Use each CAD pad's geometry-seeded regular diff directly. Choose this when hardware behavior should follow the primary regular one-to-one."),
            new(
                CadOutputFwDiffAutoMode.StrictUnique,
                "Geometry seed (unique per IC)",
                "Use geometry seed directly, but keep diff idx unique within each IC. Conflicts stay unresolved instead of shifting other CAD pads."),
        };

    partial void OnShowDiffIndexOverlayChanging(bool value) => TrackUndo(ShowDiffIndexOverlay, value, v => ShowDiffIndexOverlay = v, "Toggle CAD Output FW Diff overlay");
    partial void OnCadOutputFwDiffIndexStartChanging(decimal value) => TrackUndo(CadOutputFwDiffIndexStart, value, v => CadOutputFwDiffIndexStart = v, "CAD Output FW Diff start");
    partial void OnCadOutputFwDiffAutoModeChanging(CadOutputFwDiffAutoMode value) => TrackUndo(CadOutputFwDiffAutoMode, value, v => CadOutputFwDiffAutoMode = v, "CAD Output FW Diff auto mode");
    partial void OnCadOutputFwDiffIndexAnchorCadIdChanging(decimal value) => TrackUndo(CadOutputFwDiffIndexAnchorCadId, value, v => CadOutputFwDiffIndexAnchorCadId = v, "CAD Output FW Diff anchor");
}
