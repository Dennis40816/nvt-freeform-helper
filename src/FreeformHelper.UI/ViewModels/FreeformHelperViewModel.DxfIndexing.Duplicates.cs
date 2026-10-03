using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private Step4DuplicateDiffGroupViewModel[] _step4DuplicateDiffGroups = Array.Empty<Step4DuplicateDiffGroupViewModel>();

    public IReadOnlyList<Step4DuplicateDiffGroupViewModel> Step4DuplicateDiffGroups => _step4DuplicateDiffGroups;

    private void RefreshStep4DuplicateDiffGroups(
        IReadOnlyList<CadPad>? orderedVisiblePads = null,
        WorkflowDataSnapshot? workflowSnapshot = null)
    {
        var orderedPads = orderedVisiblePads ?? CadPads.ToList();
        workflowSnapshot ??= BuildWorkflowDataSnapshot();
        if (orderedPads.Count == 0 || workflowSnapshot.CadOutputFwDiffIndexByCadId.Count == 0)
        {
            _step4DuplicateDiffGroups = Array.Empty<Step4DuplicateDiffGroupViewModel>();
            HasStep4DuplicateDiffGroups = false;
            Step4DuplicateDiffSummary = "Duplicate CAD Output FW Diff: none.";
            OnPropertyChanged(nameof(Step4DuplicateDiffGroups));
            return;
        }

        IReadOnlyDictionary<int, CadBestMatchSeed>? rawSeedByCadId = null;
        IReadOnlyDictionary<int, CadBestMatchSeed>? maskedSeedByCadId = null;
        if (workflowSnapshot.CadOutputFwDiffAssignmentDecisionsByCadId.Count > 0)
        {
            rawSeedByCadId = workflowSnapshot.CadOutputFwDiffAssignmentDecisionsByCadId.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.RawSeed);
            maskedSeedByCadId = workflowSnapshot.CadOutputFwDiffAssignmentDecisionsByCadId.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.MaskedSeed);
        }
        else if (_grid is not null && workflowSnapshot.CadToRegularByCadId.Count > 0)
        {
            var regularPadById = _grid.Pads.ToDictionary(static pad => pad.RegularPadId);
            rawSeedByCadId = CadBestMatchSeedService.BuildSeedDetailsByCadId(
                orderedPads,
                workflowSnapshot.CadToRegularByCadId,
                regularPadById,
                workflowSnapshot.CadIcIndexByCadId,
                activeRegularPadIds: null);
            maskedSeedByCadId = CadBestMatchSeedService.BuildSeedDetailsByCadId(
                orderedPads,
                workflowSnapshot.CadToRegularByCadId,
                regularPadById,
                workflowSnapshot.CadIcIndexByCadId,
                workflowSnapshot.ActiveRegularVisibilityMaskPadIds);
        }

        var duplicateGroups = CadOutputFwDiffIndexDuplicateReviewService.BuildGroups(
            orderedPads,
            workflowSnapshot.CadOutputFwDiffIndexByCadId,
            workflowSnapshot.CadIcIndexByCadId,
            rawSeedByCadId,
            maskedSeedByCadId);

        _step4DuplicateDiffGroups = duplicateGroups
            .Select(static group => new Step4DuplicateDiffGroupViewModel(
                group.IcIndex,
                group.DiffIndex,
                group.Entries.Select(static entry => entry.CadPadId).ToArray(),
                TitleText: FormattableString.Invariant($"IC{group.IcIndex + 1} · diff {group.DiffIndex}"),
                CadSummaryText: string.Join(
                    Environment.NewLine,
                    group.Entries.Select(static entry =>
                    {
                        var rawText = entry.RawBestDiffIndex.HasValue
                            ? $"raw {entry.RawBestDiffIndex}"
                            : "raw -";
                        var maskedText = entry.MaskedBestDiffIndex.HasValue
                            ? $"masked {entry.MaskedBestDiffIndex}"
                            : "masked -";
                        return FormattableString.Invariant(
                            $"CAD {entry.CadPadId} [{entry.LayerName}] -> current {entry.CurrentDiffIndex}, {rawText}, {maskedText}");
                    })),
                SuggestionText: BuildDuplicateSuggestion(group)))
            .ToArray();

        HasStep4DuplicateDiffGroups = _step4DuplicateDiffGroups.Length > 0;
        Step4DuplicateDiffSummary = BuildStep4DuplicateDiffSummary(_step4DuplicateDiffGroups);
        OnPropertyChanged(nameof(Step4DuplicateDiffGroups));
    }

    private static string BuildDuplicateSuggestion(CadOutputFwDiffIndexDuplicateReviewGroup group)
    {
        var rawDiffs = group.Entries
            .Select(static entry => entry.RawBestDiffIndex)
            .Distinct()
            .OrderBy(static diff => diff ?? int.MaxValue)
            .Select(static diff => diff?.ToString(CultureInfo.InvariantCulture) ?? "-")
            .ToArray();
        var maskedDiffs = group.Entries
            .Select(static entry => entry.MaskedBestDiffIndex)
            .Distinct()
            .OrderBy(static diff => diff ?? int.MaxValue)
            .Select(static diff => diff?.ToString(CultureInfo.InvariantCulture) ?? "-")
            .ToArray();
        return FormattableString.Invariant(
            $"Use Select to load these CAD pads into the shared diff-shift workflow. Raw candidates: {string.Join("/", rawDiffs)} · Masked candidates: {string.Join("/", maskedDiffs)}.");
    }

    private static string BuildStep4DuplicateDiffSummary(Step4DuplicateDiffGroupViewModel[] groups)
    {
        if (groups.Length == 0)
        {
            return "Duplicate CAD Output FW Diff: none.";
        }

        var impactedCadCount = groups.Sum(static group => group.CadPadIds.Count);
        var sample = groups[0];
        return FormattableString.Invariant(
            $"Duplicate CAD Output FW Diff: {groups.Length} group(s), {impactedCadCount} CAD pad(s). Sample: IC{sample.IcIndex + 1}/diff{sample.DiffIndex} -> CAD {string.Join(", ", sample.CadPadIds)}.");
    }

    private void SelectStep4DuplicateDiffGroup(Step4DuplicateDiffGroupViewModel? group)
    {
        if (group is null || group.CadPadIds.Count == 0)
        {
            return;
        }

        TryLocateSelection(
            group.CadPadIds,
            Array.Empty<int>(),
            focusSelection: true,
            notVisibleStatus: $"Duplicate CAD Output FW Diff IC{group.IcIndex + 1}/diff{group.DiffIndex} is not visible.",
            successStatusBuilder: (cadCount, _) =>
                $"Duplicate CAD Output FW Diff focused: IC{group.IcIndex + 1}/diff{group.DiffIndex} ({cadCount} CAD pad(s)).");
    }
}
