using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void LocateCadByQuickInput()
    {
        var cadPadId = NormalizeQuickLocateInput(QuickLocateCadPadId);
        using (_undoSuppression.Enter())
        {
            QuickLocateCadPadId = cadPadId;
        }

        TryLocateSelection(
            new[] { cadPadId },
            Array.Empty<int>(),
            focusSelection: true,
            notVisibleStatus: $"CAD {cadPadId} is not visible.",
            successStatusBuilder: (cadCount, _) => $"CAD {cadPadId} focused (CAD output={cadCount}).");
    }

    private void LocateRegularByQuickInput()
    {
        var regularIndex = NormalizeQuickLocateInput(QuickLocateRegularPadIndex);
        using (_undoSuppression.Enter())
        {
            QuickLocateRegularPadIndex = regularIndex;
        }

        var regularPad = RegularPads.FirstOrDefault(pad => pad.Index == regularIndex);
        if (regularPad is null)
        {
            SetStatus($"Regular index {regularIndex} is not visible.");
            return;
        }

        TryLocateSelection(
            Array.Empty<int>(),
            new[] { regularPad.RegularPadId },
            focusSelection: true,
            notVisibleStatus: $"Regular index {regularIndex} is not visible.",
            successStatusBuilder: (_, regularCount) =>
                $"Regular index {regularIndex} focused (visible REG={regularCount}).");
    }

    private void ExecuteQuickFocus()
    {
        if (!QuickFocusQueryParser.TryParse(QuickFocusQueryText, out var query, out var error))
        {
            SetStatus(error);
            return;
        }

        var resolved = query.TargetKind switch
        {
            QuickFocusTargetKind.Cad => TryFocusCadPadById(query.Value, query.IcIndex),
            QuickFocusTargetKind.Regular => TryFocusRegularPadById(query.Value, query.IcIndex),
            QuickFocusTargetKind.Diff => TryFocusFwDiff(query.Value, query.IcIndex),
            _ => TryAutoFocusByIdOrDiff(query.Value, query.IcIndex),
        };

        if (!resolved)
        {
            SetStatus(BuildQuickFocusMissingStatus(query));
        }
    }

    public void FocusRegularMatch(int cadPadId, int regularPadId)
    {
        TryLocateSelection(
            new[] { cadPadId },
            new[] { regularPadId },
            focusSelection: true,
            notVisibleStatus: "Match locate: target not visible.",
            successStatusBuilder: (cadCount, regularCount) =>
                $"Match focused: CAD {cadPadId}, Regular {regularPadId} (CAD output={cadCount}, Regular={regularCount}).");
    }

    private bool TryAutoFocusByIdOrDiff(int value, int? icIndex)
    {
        return TryFocusCadPadById(value, icIndex, "Auto focus") ||
               TryFocusRegularPadById(value, icIndex, "Auto focus") ||
               TryFocusFwDiff(value, icIndex, "Auto focus");
    }

    private bool TryFocusCadPadById(int cadPadId, int? icIndex, string statusPrefix = "Quick focus")
    {
        var visibleCadIds = CadPads
            .Where(pad => pad.Id == cadPadId && IsCadInIc(pad.Id, icIndex))
            .Select(pad => pad.Id)
            .ToList();
        if (visibleCadIds.Count == 0)
        {
            return false;
        }

        return TryLocateSelection(
            visibleCadIds,
            Array.Empty<int>(),
            focusSelection: true,
            notVisibleStatus: BuildQuickFocusMissingStatus(new QuickFocusQuery(QuickFocusTargetKind.Cad, cadPadId, icIndex)),
            successStatusBuilder: (cadCount, _) =>
                $"{statusPrefix}: {FormatQuickFocusIc(icIndex)}CAD {cadPadId} focused (CAD output={cadCount}).");
    }

    private bool TryFocusRegularPadById(int regularPadId, int? icIndex, string statusPrefix = "Quick focus")
    {
        var visibleRegularIds = RegularPads
            .Where(pad => pad.RegularPadId == regularPadId && IsRegularInIc(pad, icIndex))
            .Select(pad => pad.RegularPadId)
            .ToList();
        if (visibleRegularIds.Count == 0)
        {
            return false;
        }

        return TryLocateSelection(
            Array.Empty<int>(),
            visibleRegularIds,
            focusSelection: true,
            notVisibleStatus: BuildQuickFocusMissingStatus(new QuickFocusQuery(QuickFocusTargetKind.Regular, regularPadId, icIndex)),
            successStatusBuilder: (_, regularCount) =>
                $"{statusPrefix}: {FormatQuickFocusIc(icIndex)}REG {regularPadId} focused (visible REG={regularCount}).");
    }

    private bool TryFocusFwDiff(int diffIndex, int? icIndex, string statusPrefix = "Quick focus")
    {
        var regularIds = RegularPads
            .Where(pad => pad.DiffIndex == diffIndex && IsRegularInIc(pad, icIndex))
            .Select(pad => pad.RegularPadId)
            .Distinct()
            .ToList();
        var cadIds = CadPads
            .Where(pad => IsCadInIc(pad.Id, icIndex) &&
                          _cadOutputFwDiffIndexByCadId.TryGetValue(pad.Id, out var cadOutputFwDiffIndex) &&
                          cadOutputFwDiffIndex == diffIndex)
            .Select(pad => pad.Id)
            .Distinct()
            .ToList();

        if (regularIds.Count == 0 && cadIds.Count == 0)
        {
            return false;
        }

        return TryLocateSelection(
            cadIds,
            regularIds,
            focusSelection: true,
            notVisibleStatus: BuildQuickFocusMissingStatus(new QuickFocusQuery(QuickFocusTargetKind.Diff, diffIndex, icIndex)),
            successStatusBuilder: (cadCount, regularCount) =>
                $"{statusPrefix}: {FormatQuickFocusIc(icIndex)}FW diff {diffIndex} focused (CAD output={cadCount}, REG={regularCount}).");
    }

    private static bool IsRegularInIc(RegularPad regularPad, int? icIndex)
    {
        return !icIndex.HasValue || regularPad.IcIndex == icIndex.Value;
    }

    private bool IsCadInIc(int cadPadId, int? icIndex)
    {
        if (!icIndex.HasValue)
        {
            return true;
        }

        return _cadIcIndexByCadId.TryGetValue(cadPadId, out var cadIcIndex) && cadIcIndex == icIndex.Value;
    }

    private static string FormatQuickFocusIc(int? icIndex)
    {
        return icIndex.HasValue ? $"IC {icIndex.Value + 1} " : string.Empty;
    }

    private static string BuildQuickFocusMissingStatus(QuickFocusQuery query)
    {
        var icText = FormatQuickFocusIc(query.IcIndex);
        return query.TargetKind switch
        {
            QuickFocusTargetKind.Cad => $"Quick focus: {icText}CAD {query.Value} is not visible.",
            QuickFocusTargetKind.Regular => $"Quick focus: {icText}REG {query.Value} is not visible.",
            QuickFocusTargetKind.Diff => $"Quick focus: {icText}FW diff {query.Value} is not visible.",
            _ => $"Quick focus: {icText}{query.Value} did not match visible CAD, REG, or FW diff.",
        };
    }

    public IReadOnlyList<int> GetMatchedRegularPadIds(int cadPadId)
    {
        if (_latestPadMatchResult.CadToRegular.TryGetValue(cadPadId, out var links) && links.Count > 0)
        {
            return links
                .Select(link => link.RegularPadId)
                .Distinct()
                .ToList();
        }

        return RegularPads
            .Where(regularPad => regularPad.MatchedCadPadId == cadPadId)
            .Select(regularPad => regularPad.RegularPadId)
            .Distinct()
            .ToList();
    }

    public IReadOnlyList<PadMatchLink> GetMatchedRegularLinks(int cadPadId)
    {
        if (_latestPadMatchResult.CadToRegular.TryGetValue(cadPadId, out var links) && links.Count > 0)
        {
            return links;
        }

        return Array.Empty<PadMatchLink>();
    }

    public (int IcIndex, int DiffIndex)? GetRegularPadIcDiff(int regularPadId)
    {
        var regularPad = RegularPads.FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        if (regularPad is null)
        {
            return null;
        }

        return (regularPad.IcIndex, regularPad.DiffIndex);
    }

    public IReadOnlyList<int> GetMatchedCadPadIds(int regularPadId)
    {
        if (_latestPadMatchResult.RegularToCad.TryGetValue(regularPadId, out var links) && links.Count > 0)
        {
            return links
                .Select(link => link.CadPadId)
                .Distinct()
                .ToList();
        }

        var regularPad = RegularPads.FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        if (regularPad?.MatchedCadPadId is int cadId)
        {
            return new[] { cadId };
        }

        return Array.Empty<int>();
    }

    public IReadOnlyList<PadMatchLink> GetMatchedCadLinks(int regularPadId)
    {
        if (_latestPadMatchResult.RegularToCad.TryGetValue(regularPadId, out var links) && links.Count > 0)
        {
            return links;
        }

        return Array.Empty<PadMatchLink>();
    }

    public void FocusCadMatches(int cadPadId)
    {
        LocateCadMatches(cadPadId, focusSelection: true);
    }

    public void HighlightCadMatches(int cadPadId)
    {
        LocateCadMatches(cadPadId, focusSelection: false);
    }

    public void FocusRegularMatches(int regularPadId)
    {
        LocateRegularMatches(regularPadId, focusSelection: true);
    }

    public void HighlightRegularMatches(int regularPadId)
    {
        LocateRegularMatches(regularPadId, focusSelection: false);
    }

    public void HighlightCadPadGroup(IReadOnlyCollection<int> cadPadIds, string? statusPrefix = null)
    {
        if (cadPadIds is null || cadPadIds.Count == 0)
        {
            SetStatus("Owner CAD highlight skipped: no target ids.");
            return;
        }

        var visibleCadIds = FilterVisibleCadIds(cadPadIds);
        if (visibleCadIds.Count == 0)
        {
            SetStatus("Owner CAD highlight skipped: targets are not visible.");
            return;
        }

        _selectionCoordinator.ApplyProgrammaticSelection(
            visibleCadIds,
            Array.Empty<int>(),
            CanvasHost);
        SetStatus(string.IsNullOrWhiteSpace(statusPrefix)
            ? $"Owner CAD highlighted: {visibleCadIds.Count} pad(s)."
            : $"{statusPrefix}: {visibleCadIds.Count} pad(s).");
    }

    public void HighlightRegularPadGroup(IReadOnlyCollection<int> regularPadIds, string? statusPrefix = null)
    {
        if (regularPadIds is null || regularPadIds.Count == 0)
        {
            SetStatus("Regular target highlight skipped: no target ids.");
            return;
        }

        var visibleRegularIds = FilterVisibleRegularIds(regularPadIds);
        if (visibleRegularIds.Count == 0)
        {
            SetStatus("Regular target highlight skipped: targets are not visible.");
            return;
        }

        _selectionCoordinator.ApplyProgrammaticSelection(
            Array.Empty<int>(),
            visibleRegularIds,
            CanvasHost);
        SetStatus(string.IsNullOrWhiteSpace(statusPrefix)
            ? $"Regular target highlighted: {visibleRegularIds.Count} pad(s)."
            : $"{statusPrefix}: {visibleRegularIds.Count} pad(s).");
    }

    private void LocateCadMatches(int cadPadId, bool focusSelection)
    {
        var visibleCadIds = FilterVisibleCadIds(new[] { cadPadId });
        if (visibleCadIds.Count == 0)
        {
            SetStatus($"CAD {cadPadId} is not visible.");
            return;
        }

        var matchedRegularIds = FilterVisibleRegularIds(GetMatchedRegularPadIds(cadPadId));
        if (matchedRegularIds.Count == 0)
        {
            SetStatus($"CAD {cadPadId} has no overlap match.");
            return;
        }

        TryLocateSelection(
            visibleCadIds,
            matchedRegularIds,
            focusSelection,
            $"CAD {cadPadId} is not visible.",
            (_, regularCount) => focusSelection
                ? $"Match focused: CAD {cadPadId} -> {regularCount} regular pad(s)."
                : $"Match highlighted: CAD {cadPadId} -> {regularCount} regular pad(s).");
    }

    private void LocateRegularMatches(int regularPadId, bool focusSelection)
    {
        var visibleRegularIds = FilterVisibleRegularIds(new[] { regularPadId });
        if (visibleRegularIds.Count == 0)
        {
            SetStatus($"Regular {regularPadId} is not visible.");
            return;
        }

        var matchedCadIds = FilterVisibleCadIds(GetMatchedCadPadIds(regularPadId));
        if (matchedCadIds.Count == 0)
        {
            SetStatus($"Regular {regularPadId} has no overlap match.");
            return;
        }

        TryLocateSelection(
            matchedCadIds,
            visibleRegularIds,
            focusSelection,
            $"Regular {regularPadId} is not visible.",
            (cadCount, _) => focusSelection
                ? $"Match focused: Regular {regularPadId} -> {cadCount} CAD pad(s)."
                : $"Match highlighted: Regular {regularPadId} -> {cadCount} CAD pad(s).");
    }

    private bool TryLocateSelection(
        IReadOnlyCollection<int> cadCandidateIds,
        IReadOnlyCollection<int> regularCandidateIds,
        bool focusSelection,
        string notVisibleStatus,
        Func<int, int, string> successStatusBuilder,
        double? minZoomOverride = null)
    {
        var visibleCadIds = FilterVisibleCadIds(cadCandidateIds);
        var visibleRegularIds = FilterVisibleRegularIds(regularCandidateIds);

        if (visibleCadIds.Count == 0 && visibleRegularIds.Count == 0)
        {
            SetStatus(notVisibleStatus);
            return false;
        }

        _selectionCoordinator.ApplyProgrammaticSelection(visibleCadIds, visibleRegularIds, CanvasHost);
        if (focusSelection)
        {
            CanvasHost?.FocusSelectionWithMinZoom(minZoomOverride ?? LocateFocusMinZoom);
        }

        SetStatus(successStatusBuilder(visibleCadIds.Count, visibleRegularIds.Count));
        return true;
    }

    private List<int> FilterVisibleCadIds(IEnumerable<int> candidateIds)
    {
        var visibleIds = CadPads
            .Select(pad => pad.Id)
            .ToHashSet();

        return candidateIds
            .Where(visibleIds.Contains)
            .Distinct()
            .ToList();
    }

    private List<int> FilterVisibleRegularIds(IEnumerable<int> candidateIds)
    {
        var visibleIds = RegularPads
            .Select(pad => pad.RegularPadId)
            .ToHashSet();

        return candidateIds
            .Where(visibleIds.Contains)
            .Distinct()
            .ToList();
    }

    private static int NormalizeQuickLocateInput(decimal value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, 0, 2_000_000_000);
    }
}
