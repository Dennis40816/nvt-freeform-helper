using System.Globalization;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private bool IsCadPadEffectivelyHidden(int cadPadId)
    {
        return _hiddenCadPadIds.Contains(cadPadId) || _autoHiddenDuplicateCadPadIds.Contains(cadPadId);
    }

    private HashSet<int> BuildEffectiveHiddenCadPadIds()
    {
        var effectiveHiddenIds = _hiddenCadPadIds.ToHashSet();
        effectiveHiddenIds.UnionWith(_autoHiddenDuplicateCadPadIds);
        return effectiveHiddenIds;
    }

    private void ClearAutoHiddenDuplicateCadPadState()
    {
        _autoHiddenDuplicateCadPadIds.Clear();
        _duplicateCadPadIdToCanonicalId.Clear();
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

            _autoHiddenDuplicateCadPadIds.Add(duplicateId);
        }

        foreach (var pair in duplicateSanitization.DuplicateToCanonicalPadId)
        {
            if (!_autoHiddenDuplicateCadPadIds.Contains(pair.Key) || !validCadIds.Contains(pair.Value))
            {
                continue;
            }

            _duplicateCadPadIdToCanonicalId[pair.Key] = pair.Value;
        }

        if (_autoHiddenDuplicateCadPadIds.Count > 0)
        {
            Logger.Debug(
                CultureInfo.InvariantCulture,
                "DXF duplicate pads auto-hidden. total={0}, ids={1}.",
                _autoHiddenDuplicateCadPadIds.Count,
                string.Join(", ", _autoHiddenDuplicateCadPadIds.OrderBy(static id => id)));
        }
    }

    private HashSet<int> GetRestoredDuplicateCadPadIds()
    {
        var restored = _duplicateCadPadIdToCanonicalId.Keys.ToHashSet();
        restored.ExceptWith(_autoHiddenDuplicateCadPadIds);
        return restored;
    }

    private void ApplyPersistedVisibleDuplicateCadPads(IEnumerable<int> duplicateCadPadIds)
    {
        if (_cad is null)
        {
            return;
        }

        var duplicateIds = duplicateCadPadIds
            .Where(_duplicateCadPadIdToCanonicalId.ContainsKey)
            .Distinct()
            .ToList();
        if (duplicateIds.Count == 0)
        {
            return;
        }

        foreach (var duplicateId in duplicateIds)
        {
            _autoHiddenDuplicateCadPadIds.Remove(duplicateId);
        }

        SyncDuplicateCadState();
    }
}
