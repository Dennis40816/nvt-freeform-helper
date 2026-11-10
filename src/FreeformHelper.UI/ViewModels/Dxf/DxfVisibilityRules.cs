// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfVisibilityRules
{
    internal static List<int> SelectVisibleSelectedCadIds(IEnumerable<int> selectedCadIds, Func<int, bool> isEffectivelyHidden)
    {
        return selectedCadIds
            .Where(id => !isEffectivelyHidden(id))
            .ToList();
    }

    internal static List<int> SelectManualHiddenCadPadIds(IEnumerable<int> hiddenCadPadIds, CombinedCadState combinedCad)
    {
        return hiddenCadPadIds
            .Where(id => !combinedCad.GroupIdBySourceCadId.ContainsKey(id))
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
    }

    internal static List<int> SelectRestorableHiddenCadPadIds(
        IEnumerable<int> cadPadIds, HashSet<int> hiddenCadPadIds, CombinedCadState combinedCad)
    {
        return cadPadIds
            .Where(id => hiddenCadPadIds.Contains(id) && !combinedCad.GroupIdBySourceCadId.ContainsKey(id))
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
    }

    internal static List<int> SelectChangedDuplicateCadPadIds(
        IEnumerable<int> cadPadIds, IReadOnlyDictionary<int, int> duplicateCadPadIdToCanonicalId,
        HashSet<int> autoHiddenDuplicateCadPadIds, bool autoHide)
    {
        var duplicateIds = cadPadIds
            .Where(duplicateCadPadIdToCanonicalId.ContainsKey)
            .Distinct()
            .OrderBy(static id => id)
            .ToList();
        if (duplicateIds.Count == 0)
        {
            return [];
        }

        var changedIds = new List<int>();
        foreach (var duplicateId in duplicateIds)
        {
            var isCurrentlyAutoHidden = autoHiddenDuplicateCadPadIds.Contains(duplicateId);
            if (autoHide == isCurrentlyAutoHidden)
            {
                continue;
            }

            changedIds.Add(duplicateId);
        }

        return changedIds;
    }

    internal static List<CadPad> SelectRegularSourceLayerPadsToAppend(
        IReadOnlyList<CadPad> visiblePads, CadPadSet cad, string regularLayerName, Func<int, bool> isEffectivelyHidden)
    {
        var visibleIds = visiblePads.Select(pad => pad.Id).ToHashSet();
        var appended = new List<CadPad>();
        foreach (var pad in cad.Pads)
        {
            if (isEffectivelyHidden(pad.Id) ||
                !string.Equals(pad.Layer, regularLayerName, StringComparison.OrdinalIgnoreCase) ||
                !visibleIds.Add(pad.Id))
            {
                continue;
            }

            appended.Add(pad);
        }

        return appended;
    }
}
