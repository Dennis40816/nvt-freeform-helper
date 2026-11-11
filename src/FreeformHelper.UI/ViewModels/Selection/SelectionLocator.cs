// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Selection;

internal sealed class SelectionLocator
{
    private const double LocateFocusMinZoom = 2.0;
    private readonly SelectionLocatorPorts _ports;

    public SelectionLocator(SelectionLocatorPorts ports)
    {
        _ports = ports;
    }

    public void ExecuteQuickFocus()
    {
        var parsed = _ports.ParseQuery(_ports.ReadQuery());
        if (!parsed.Success)
        {
            _ports.SetStatus(parsed.Error);
            return;
        }

        var query = parsed.Query;
        var resolved = query.TargetKind switch
        {
            SelectionLocateTargetKind.Cad => TryFocusCadPadById(query.Value, query.IcIndex),
            SelectionLocateTargetKind.Regular => TryFocusRegularPadById(query.Value, query.IcIndex),
            SelectionLocateTargetKind.Diff => TryFocusFwDiff(query.Value, query.IcIndex),
            _ => TryAutoFocusByIdOrDiff(query.Value, query.IcIndex),
        };

        if (!resolved)
        {
            _ports.SetStatus(BuildQuickFocusMissingStatus(query));
        }
    }

    private bool TryAutoFocusByIdOrDiff(int value, int? icIndex)
    {
        return TryFocusCadPadById(value, icIndex, "Auto focus") ||
               TryFocusRegularPadById(value, icIndex, "Auto focus") ||
               TryFocusFwDiff(value, icIndex, "Auto focus");
    }

    private bool TryFocusCadPadById(int cadPadId, int? icIndex, string statusPrefix = "Quick focus")
    {
        var visibleCadIds = _ports.ReadCadPads()
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
            notVisibleStatus: BuildQuickFocusMissingStatus(new SelectionLocateQuery(SelectionLocateTargetKind.Cad, cadPadId, icIndex)),
            successStatusBuilder: (cadCount, _) =>
                $"{statusPrefix}: {FormatQuickFocusIc(icIndex)}CAD {cadPadId} focused (CAD output={cadCount}).");
    }

    private bool TryFocusRegularPadById(int regularPadId, int? icIndex, string statusPrefix = "Quick focus")
    {
        var visibleRegularIds = _ports.ReadRegularPads()
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
            notVisibleStatus: BuildQuickFocusMissingStatus(new SelectionLocateQuery(SelectionLocateTargetKind.Regular, regularPadId, icIndex)),
            successStatusBuilder: (_, regularCount) =>
                $"{statusPrefix}: {FormatQuickFocusIc(icIndex)}REG {regularPadId} focused (visible REG={regularCount}).");
    }

    private bool TryFocusFwDiff(int diffIndex, int? icIndex, string statusPrefix = "Quick focus")
    {
        var regularIds = _ports.ReadRegularPads()
            .Where(pad => pad.DiffIndex == diffIndex && IsRegularInIc(pad, icIndex))
            .Select(pad => pad.RegularPadId)
            .Distinct()
            .ToList();
        var cadIds = _ports.ReadCadPads()
            .Where(pad => IsCadInIc(pad.Id, icIndex) &&
                          _ports.ReadCadOutputDiffIndexes().TryGetValue(pad.Id, out var cadOutputFwDiffIndex) &&
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
            notVisibleStatus: BuildQuickFocusMissingStatus(new SelectionLocateQuery(SelectionLocateTargetKind.Diff, diffIndex, icIndex)),
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

        return _ports.ReadCadIcIndexes().TryGetValue(cadPadId, out var cadIcIndex) && cadIcIndex == icIndex.Value;
    }

    private static string FormatQuickFocusIc(int? icIndex)
    {
        return icIndex.HasValue ? $"IC {icIndex.Value + 1} " : string.Empty;
    }

    private static string BuildQuickFocusMissingStatus(SelectionLocateQuery query)
    {
        var icText = FormatQuickFocusIc(query.IcIndex);
        return query.TargetKind switch
        {
            SelectionLocateTargetKind.Cad => $"Quick focus: {icText}CAD {query.Value} is not visible.",
            SelectionLocateTargetKind.Regular => $"Quick focus: {icText}REG {query.Value} is not visible.",
            SelectionLocateTargetKind.Diff => $"Quick focus: {icText}FW diff {query.Value} is not visible.",
            _ => $"Quick focus: {icText}{query.Value} did not match visible CAD, REG, or FW diff.",
        };
    }

    public IReadOnlyList<int> GetMatchedRegularPadIds(int cadPadId)
    {
        if (_ports.ReadMatch().CadToRegular.TryGetValue(cadPadId, out var links) && links.Count > 0)
        {
            return links
                .Select(link => link.RegularPadId)
                .Distinct()
                .ToList();
        }

        return _ports.ReadRegularPads()
            .Where(regularPad => regularPad.MatchedCadPadId == cadPadId)
            .Select(regularPad => regularPad.RegularPadId)
            .Distinct()
            .ToList();
    }

    public IReadOnlyList<PadMatchLink> GetMatchedRegularLinks(int cadPadId)
    {
        if (_ports.ReadMatch().CadToRegular.TryGetValue(cadPadId, out var links) && links.Count > 0)
        {
            return links;
        }

        return Array.Empty<PadMatchLink>();
    }

    public (int IcIndex, int DiffIndex)? GetRegularPadIcDiff(int regularPadId)
    {
        var regularPad = _ports.ReadRegularPads().FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        if (regularPad is null)
        {
            return null;
        }

        return (regularPad.IcIndex, regularPad.DiffIndex);
    }

    public IReadOnlyList<int> GetMatchedCadPadIds(int regularPadId)
    {
        if (_ports.ReadMatch().RegularToCad.TryGetValue(regularPadId, out var links) && links.Count > 0)
        {
            return links
                .Select(link => link.CadPadId)
                .Distinct()
                .ToList();
        }

        var regularPad = _ports.ReadRegularPads().FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        if (regularPad?.MatchedCadPadId is int cadId)
        {
            return [cadId];
        }

        return Array.Empty<int>();
    }

    public IReadOnlyList<PadMatchLink> GetMatchedCadLinks(int regularPadId)
    {
        if (_ports.ReadMatch().RegularToCad.TryGetValue(regularPadId, out var links) && links.Count > 0)
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
            _ports.SetStatus("Owner CAD highlight skipped: no target ids.");
            return;
        }

        var visibleCadIds = FilterVisibleCadIds(cadPadIds);
        if (visibleCadIds.Count == 0)
        {
            _ports.SetStatus("Owner CAD highlight skipped: targets are not visible.");
            return;
        }

        _ports.ApplySelection(
            visibleCadIds,
            Array.Empty<int>());
        _ports.SetStatus(string.IsNullOrWhiteSpace(statusPrefix)
            ? $"Owner CAD highlighted: {visibleCadIds.Count} pad(s)."
            : $"{statusPrefix}: {visibleCadIds.Count} pad(s).");
    }

    public void HighlightRegularPadGroup(IReadOnlyCollection<int> regularPadIds, string? statusPrefix = null)
    {
        if (regularPadIds is null || regularPadIds.Count == 0)
        {
            _ports.SetStatus("Regular target highlight skipped: no target ids.");
            return;
        }

        var visibleRegularIds = FilterVisibleRegularIds(regularPadIds);
        if (visibleRegularIds.Count == 0)
        {
            _ports.SetStatus("Regular target highlight skipped: targets are not visible.");
            return;
        }

        _ports.ApplySelection(
            Array.Empty<int>(),
            visibleRegularIds);
        _ports.SetStatus(string.IsNullOrWhiteSpace(statusPrefix)
            ? $"Regular target highlighted: {visibleRegularIds.Count} pad(s)."
            : $"{statusPrefix}: {visibleRegularIds.Count} pad(s).");
    }

    private void LocateCadMatches(int cadPadId, bool focusSelection)
    {
        var visibleCadIds = FilterVisibleCadIds([cadPadId]);
        if (visibleCadIds.Count == 0)
        {
            _ports.SetStatus($"CAD {cadPadId} is not visible.");
            return;
        }

        var matchedRegularIds = FilterVisibleRegularIds(GetMatchedRegularPadIds(cadPadId));
        if (matchedRegularIds.Count == 0)
        {
            _ports.SetStatus($"CAD {cadPadId} has no overlap match.");
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
        var visibleRegularIds = FilterVisibleRegularIds([regularPadId]);
        if (visibleRegularIds.Count == 0)
        {
            _ports.SetStatus($"Regular {regularPadId} is not visible.");
            return;
        }

        var matchedCadIds = FilterVisibleCadIds(GetMatchedCadPadIds(regularPadId));
        if (matchedCadIds.Count == 0)
        {
            _ports.SetStatus($"Regular {regularPadId} has no overlap match.");
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

    public bool TryLocateSelection(
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
            _ports.SetStatus(notVisibleStatus);
            return false;
        }

        _ports.ApplySelection(visibleCadIds, visibleRegularIds);
        if (focusSelection)
        {
            _ports.Focus(minZoomOverride ?? LocateFocusMinZoom);
        }

        _ports.SetStatus(successStatusBuilder(visibleCadIds.Count, visibleRegularIds.Count));
        return true;
    }

    private List<int> FilterVisibleCadIds(IEnumerable<int> candidateIds)
    {
        var visibleIds = _ports.ReadCadPads()
            .Select(pad => pad.Id)
            .ToHashSet();

        return candidateIds
            .Where(visibleIds.Contains)
            .Distinct()
            .ToList();
    }

    private List<int> FilterVisibleRegularIds(IEnumerable<int> candidateIds)
    {
        var visibleIds = _ports.ReadRegularPads()
            .Select(pad => pad.RegularPadId)
            .ToHashSet();

        return candidateIds
            .Where(visibleIds.Contains)
            .Distinct()
            .ToList();
    }
}
