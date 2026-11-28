// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfDuplicateGroupBuilder
{
    public static DxfDuplicateGroupBuildResult Build(
        IReadOnlyList<CadPad> orderedPads,
        DxfDuplicateGroupInputs workflowSnapshot,
        RegularGrid? grid)
    {
        if (orderedPads.Count == 0 || workflowSnapshot.CadOutputFwDiffIndexByCadId.Count == 0)
        {
            return new(Array.Empty<Step4DuplicateDiffGroupViewModel>(), "Duplicate CAD Output FW Diff: none.");
        }

        var (rawSeedByCadId, maskedSeedByCadId) = BuildSeeds(orderedPads, workflowSnapshot, grid);
        var duplicateGroups = CadOutputFwDiffIndexDuplicateReviewService.BuildGroups(
            orderedPads,
            workflowSnapshot.CadOutputFwDiffIndexByCadId,
            workflowSnapshot.CadIcIndexByCadId,
            rawSeedByCadId,
            maskedSeedByCadId);
        var groups = duplicateGroups.Select(static group => BuildGroup(group)).ToArray();
        return new(groups, BuildStep4DuplicateDiffSummary(groups));
    }

    private static (IReadOnlyDictionary<int, CadBestMatchSeed>? Raw, IReadOnlyDictionary<int, CadBestMatchSeed>? Masked) BuildSeeds(
        IReadOnlyList<CadPad> orderedPads,
        DxfDuplicateGroupInputs workflowSnapshot,
        RegularGrid? grid)
    {
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
        else if (grid is not null && workflowSnapshot.CadToRegularByCadId.Count > 0)
        {
            var regularPadById = grid.Pads.ToDictionary(static pad => pad.RegularPadId);
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

        return (rawSeedByCadId, maskedSeedByCadId);
    }

    private static Step4DuplicateDiffGroupViewModel BuildGroup(CadOutputFwDiffIndexDuplicateReviewGroup group)
    {
        return new Step4DuplicateDiffGroupViewModel(
            group.IcIndex,
            group.DiffIndex,
            group.Entries.Select(static entry => entry.CadPadId).ToArray(),
            TitleText: FormattableString.Invariant($"IC{group.IcIndex + 1} · diff {group.DiffIndex}"),
            CadSummaryText: string.Join(Environment.NewLine, group.Entries.Select(static entry => BuildCadSummaryLine(entry))),
            SuggestionText: BuildDuplicateSuggestion(group));
    }

    private static string BuildCadSummaryLine(CadOutputFwDiffIndexDuplicateReviewEntry entry)
    {
        var rawText = entry.RawBestDiffIndex.HasValue
            ? $"raw {entry.RawBestDiffIndex}"
            : "raw -";
        var maskedText = entry.MaskedBestDiffIndex.HasValue
            ? $"masked {entry.MaskedBestDiffIndex}"
            : "masked -";
        return FormattableString.Invariant(
            $"CAD {entry.CadPadId} [{entry.LayerName}] -> current {entry.CurrentDiffIndex}, {rawText}, {maskedText}");
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
}

internal sealed record DxfDuplicateGroupInputs(
    IReadOnlyDictionary<int, int> CadOutputFwDiffIndexByCadId,
    IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> CadOutputFwDiffAssignmentDecisionsByCadId,
    IReadOnlyDictionary<int, int> CadIcIndexByCadId,
    IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> CadToRegularByCadId,
    IReadOnlySet<int>? ActiveRegularVisibilityMaskPadIds);

internal sealed record DxfDuplicateGroupBuildResult(Step4DuplicateDiffGroupViewModel[] Groups, string Summary);
