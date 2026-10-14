using System.Globalization;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private bool IsCadPadEffectivelyHidden(int cadPadId)
    {
        return _cadEdit.HiddenCadPadIds.Contains(cadPadId) || _cadEdit.AutoHiddenDuplicateCadPadIds.Contains(cadPadId);
    }

    private HashSet<int> BuildEffectiveHiddenCadPadIds()
    {
        var effectiveHiddenIds = _cadEdit.HiddenCadPadIds.ToHashSet();
        effectiveHiddenIds.UnionWith(_cadEdit.AutoHiddenDuplicateCadPadIds);
        return effectiveHiddenIds;
    }

    private void ClearAutoHiddenDuplicateCadPadState()
    {
        _cadEdit.AutoHiddenDuplicateCadPadIds.Clear();
        _cadEdit.DuplicateCadPadIdToCanonicalId.Clear();
    }

    private void SetAutoHiddenDuplicateCadPads(CadPadExactDuplicateSanitizationResult duplicateSanitization)
    {
        ArgumentNullException.ThrowIfNull(duplicateSanitization);

        ClearAutoHiddenDuplicateCadPadState();

        if (_cad is null)
        {
            return;
        }

        var validCadIds = _cad.Pads.Select(static pad => pad.Id).ToHashSet();
        foreach (var duplicateId in duplicateSanitization.ExcludedPadIds)
        {
            if (!validCadIds.Contains(duplicateId))
            {
                continue;
            }

            _cadEdit.AutoHiddenDuplicateCadPadIds.Add(duplicateId);
        }

        foreach (var pair in duplicateSanitization.DuplicateToCanonicalPadId)
        {
            if (!_cadEdit.AutoHiddenDuplicateCadPadIds.Contains(pair.Key) || !validCadIds.Contains(pair.Value))
            {
                continue;
            }

            _cadEdit.DuplicateCadPadIdToCanonicalId[pair.Key] = pair.Value;
        }

        if (_cadEdit.AutoHiddenDuplicateCadPadIds.Count > 0)
        {
            Logger.Debug(
                CultureInfo.InvariantCulture,
                "DXF duplicate pads auto-hidden. total={0}, ids={1}.",
                _cadEdit.AutoHiddenDuplicateCadPadIds.Count,
                string.Join(", ", _cadEdit.AutoHiddenDuplicateCadPadIds.OrderBy(static id => id)));
        }
    }

    private HashSet<int> GetRestoredDuplicateCadPadIds()
    {
        var restored = _cadEdit.DuplicateCadPadIdToCanonicalId.Keys.ToHashSet();
        restored.ExceptWith(_cadEdit.AutoHiddenDuplicateCadPadIds);
        return restored;
    }

    private void ApplyPersistedVisibleDuplicateCadPads(IEnumerable<int> duplicateCadPadIds)
    {
        if (_cad is null)
        {
            return;
        }

        var duplicateIds = duplicateCadPadIds
            .Where(_cadEdit.DuplicateCadPadIdToCanonicalId.ContainsKey)
            .Distinct()
            .ToList();
        if (duplicateIds.Count == 0)
        {
            return;
        }

        foreach (var duplicateId in duplicateIds)
        {
            _cadEdit.AutoHiddenDuplicateCadPadIds.Remove(duplicateId);
        }

        SyncDuplicateCadState();
    }
}
