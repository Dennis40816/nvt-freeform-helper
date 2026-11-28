using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public int? GetCadOutputFwDiffIndexOverride(int cadPadId)
        => IndexMapping.Assignment.GetCadOutputFwDiffIndexOverride(cadPadId);

    public void SetCadOutputFwDiffIndexOverride(int cadPadId, int targetIndex)
        => IndexMapping.Assignment.SetCadOutputFwDiffIndexOverride(cadPadId, targetIndex);

    public void ClearCadOutputFwDiffIndexOverride(int cadPadId)
        => IndexMapping.Assignment.ClearCadOutputFwDiffIndexOverride(cadPadId);

    public bool IsCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
        => IndexMapping.Assignment.IsCadOutputFwDiffIndexAnchorCadPad(cadPadId);

    public void SetCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
        => IndexMapping.Assignment.SetCadOutputFwDiffIndexAnchorCadPad(cadPadId);

    public void ClearCadOutputFwDiffIndexAnchorCadPad(int cadPadId)
        => IndexMapping.Assignment.ClearCadOutputFwDiffIndexAnchorCadPad(cadPadId);

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
    private CadIndexMaps _cadIndexMaps => IndexMapping.Assignment.Maps;

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
        => IndexMapping.Assignment.TryGetCadOutputFwDiffIndex(cadPadId, out dxfIndex);

    public bool TryGetCadIcIndex(int cadPadId, out int icIndex)
        => IndexMapping.Assignment.TryGetCadIcIndex(cadPadId, out icIndex);

    public bool TryGetCadDisplayIndex(int cadPadId, out int displayIndex)
        => IndexMapping.Assignment.TryGetCadDisplayIndex(cadPadId, out displayIndex);

    private void SetLatestCadOutputFwDiffAssignmentDecisions(IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> decisions)
        => IndexMapping.Assignment.SetLatestCadOutputFwDiffAssignmentDecisions(decisions);

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
