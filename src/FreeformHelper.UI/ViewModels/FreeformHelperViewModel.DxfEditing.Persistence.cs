using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.UseCases.ProjectLoad;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    internal sealed record PersistedDxfEditProjectState(
        HashSet<int> HiddenCadPadIds,
        HashSet<int> VisibleDuplicateCadPadIds,
        Dictionary<int, string> CadLayerOverrides,
        Dictionary<int, ProjectCadGeometrySnapshot> CadGeometryOverrides,
        List<ProjectDxfCombinedCadGroup> CombinedCadGroups);

    private void CaptureDxfEditProjectState(ProjectFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (_cad is null)
        {
            file.HiddenCadPadIds = new HashSet<int>();
            file.VisibleDuplicateCadPadIds = new HashSet<int>();
            file.DxfCadLayerOverrides = new Dictionary<int, string>();
            file.DxfCadGeometryOverrides = new Dictionary<int, ProjectCadGeometrySnapshot>();
            file.DxfCombinedCadGroups = CapturePersistedCombinedCadGroups();
            return;
        }

        var validCadIds = _cad.Pads.Select(static pad => pad.Id).ToHashSet();
        file.HiddenCadPadIds = _cadEdit.HiddenCadPadIds
            .Where(validCadIds.Contains)
            .ToHashSet();
        file.VisibleDuplicateCadPadIds = GetRestoredDuplicateCadPadIds()
            .Where(validCadIds.Contains)
            .ToHashSet();
        file.DxfCadLayerOverrides = CapturePersistedCadLayerOverrides();
        file.DxfCadGeometryOverrides = CapturePersistedCadGeometryOverrides();
        file.DxfCombinedCadGroups = CapturePersistedCombinedCadGroups();
    }

    internal static PersistedDxfEditProjectState CapturePersistedDxfEditProjectState(ProjectFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new PersistedDxfEditProjectState(
            file.HiddenCadPadIds.ToHashSet(),
            file.VisibleDuplicateCadPadIds.ToHashSet(),
            file.DxfCadLayerOverrides.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            file.DxfCadGeometryOverrides.ToDictionary(
                static pair => pair.Key,
                static pair => new ProjectCadGeometrySnapshot
                {
                    Vertices = pair.Value.Vertices.ToList(),
                }),
            file.DxfCombinedCadGroups
                .Select(static group => new ProjectDxfCombinedCadGroup
                {
                    GroupId = group.GroupId,
                    SourceCadIds = group.SourceCadIds.ToList(),
                    OutputPads = group.OutputPads
                        .Select(static outputPad => new ProjectCadPadSnapshot
                        {
                            Id = outputPad.Id,
                            Name = outputPad.Name,
                            Layer = outputPad.Layer,
                            Vertices = outputPad.Vertices.ToList(),
                        })
                        .ToList(),
                })
                .ToList());
    }

    private Dictionary<int, string> CapturePersistedCadLayerOverrides()
    {
        if (_cad is null || _cadEdit.BaselineCad is null)
        {
            return new Dictionary<int, string>();
        }

        var baselineLayersByCadId = _cadEdit.BaselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First().Layer);

        return _cad.Pads
            .Where(pad =>
                baselineLayersByCadId.TryGetValue(pad.Id, out var baselineLayer) &&
                !string.Equals(baselineLayer, pad.Layer, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(static pad => pad.Id, static pad => pad.Layer);
    }

    private List<ProjectDxfCombinedCadGroup> CapturePersistedCombinedCadGroups()
    {
        var groups = new List<ProjectDxfCombinedCadGroup>();
        if (_cad is not null && _combinedCad.Groups.Count > 0)
        {
            AddCurrentCombinedCadGroups(groups, _cad);
        }

        AppendUnrestoredCombinedCadGroups(groups);
        return groups;
    }

    private void AppendUnrestoredCombinedCadGroups(List<ProjectDxfCombinedCadGroup> groups)
    {
        var usedGroupIds = groups.Select(static group => group.GroupId).ToHashSet();
        var nextGroupId = usedGroupIds.Count == 0 ? 1 : usedGroupIds.Max() + 1;
        foreach (var kept in _cadEdit.UnrestoredCombinedGroups)
        {
            var groupId = kept.GroupId;
            if (groupId > 0 && !usedGroupIds.Add(groupId))
            {
                // A group made after a DXF reload may reuse this id. Give the kept group a free one.
                groupId = nextGroupId;
                usedGroupIds.Add(groupId);
            }

            nextGroupId = Math.Max(nextGroupId, groupId + 1);
            groups.Add(new ProjectDxfCombinedCadGroup
            {
                GroupId = groupId,
                SourceCadIds = kept.SourceCadIds,
                OutputPads = kept.OutputPads,
            });
        }
    }

    private void AddCurrentCombinedCadGroups(List<ProjectDxfCombinedCadGroup> groups, CadPadSet cad)
    {
        var currentPadsById = cad.Pads.ToDictionary(static pad => pad.Id);
        foreach (var pair in _combinedCad.Groups.OrderBy(static pair => pair.Key))
        {
            var outputPads = pair.Value.OutputCadIds
                .Select(outputCadId => currentPadsById.TryGetValue(outputCadId, out var pad)
                    ? ProjectCadPadSnapshot.FromCadPad(pad)
                    : null)
                .Where(static pad => pad is not null)
                .Cast<ProjectCadPadSnapshot>()
                .ToList();
            if (pair.Value.SourceCadIds.Count == 0 || outputPads.Count == 0)
            {
                continue;
            }

            groups.Add(new ProjectDxfCombinedCadGroup
            {
                GroupId = pair.Key,
                SourceCadIds = pair.Value.SourceCadIds.Distinct().OrderBy(static id => id).ToList(),
                OutputPads = outputPads,
            });
        }
    }

    // Keeps the saved groups of a project whose DXF is not available, so the next save does not drop them.
    private DxfCombinedGroupReplayResult KeepCombinedGroupsWithoutCad(List<ProjectDxfCombinedCadGroup> combinedGroups)
    {
        var replay = new DxfCombinedGroupReplayResult();
        replay.SkippedGroups.AddRange(combinedGroups);
        _cadEdit.UnrestoredCombinedGroups = [.. combinedGroups];
        return replay;
    }

    internal DxfCombinedGroupReplayResult? ApplyCapturedDxfEditProjectState(PersistedDxfEditProjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var previous = new CadEditNotificationSnapshot(this);

        if (_cad is null)
        {
            ClearDxfEditRuntimeState(resetBaseline: false, previous);
            ClearAutoHiddenDuplicateCadPadState();
            SyncRelayeredCadState(previous);
            SyncRotatedCadState(previous);
            SyncHiddenCadIdsToProject(previous);
            SyncCombinedCadState(previous);
            SyncDuplicateCadState(previous);
            return KeepCombinedGroupsWithoutCad(state.CombinedCadGroups);
        }

        var layerSelectionSnapshot = CaptureLayerSelectionSnapshot();
        PreserveDxfDependentLayerSelections();
        ClearDxfEditRuntimeState(resetBaseline: false, previous);
        ApplyPersistedCadLayerOverrides(state.CadLayerOverrides);
        ApplyPersistedCadGeometryOverrides(state.CadGeometryOverrides);
        var replay = ReplayPersistedCombinedCadGroups(state.CombinedCadGroups);
        _cadEdit.UnrestoredCombinedGroups = [.. replay.SkippedGroups];
        SetHiddenCadPads(state.HiddenCadPadIds.Concat(replay.CombinedSourceCadIds), previous);
        ApplyPersistedVisibleDuplicateCadPads(state.VisibleDuplicateCadPadIds);
        _cadEdit.HiddenUndoStack.Clear();
        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState(previous);
        SyncRotatedCadState(previous);
        SyncCombinedCadState(previous);
        UpdateLayerToggles(_cad);
        RestoreLayerSelectionAfterCadLayerEdit(layerSelectionSnapshot, ensureVisibleLayerName: string.Empty);
        return replay;
    }

    private void ApplyPersistedCadLayerOverrides(Dictionary<int, string> layerOverrides)
    {
        if (_cad is null || layerOverrides.Count == 0)
        {
            return;
        }

        var updatedPads = new List<CadPad>(_cad.Pads.Count);
        foreach (var pad in _cad.Pads)
        {
            if (!layerOverrides.TryGetValue(pad.Id, out var targetLayer) ||
                string.IsNullOrWhiteSpace(targetLayer) ||
                string.Equals(targetLayer, pad.Layer, StringComparison.OrdinalIgnoreCase))
            {
                updatedPads.Add(pad);
                continue;
            }

            updatedPads.Add(new CadPad(pad.Id, pad.Name, targetLayer, pad.Polygon));
        }

        _cad = new CadPadSet(updatedPads);
    }

    private static bool TryBuildCombinedOutputPads(
        ProjectDxfCombinedCadGroup persistedGroup,
        HashSet<int> currentPadIds,
        ref int nextCombinedCadPadId,
        out List<CadPad> outputPads)
    {
        outputPads = new List<CadPad>(persistedGroup.OutputPads.Count);
        var conflictingOutputPad = false;
        foreach (var outputPadSnapshot in persistedGroup.OutputPads)
        {
            CadPad outputPad;
            try
            {
                outputPad = outputPadSnapshot.ToCadPad();
            }
            catch (ArgumentException ex)
            {
                _logger.Warn(
                    ex,
                    "DXF edit replay skipped combined group {0}: output pad snapshot invalid.",
                    persistedGroup.GroupId);
                conflictingOutputPad = true;
                break;
            }

            if (!currentPadIds.Add(outputPad.Id))
            {
                _logger.Warn(
                    CultureInfo.InvariantCulture,
                    "DXF edit replay skipped combined group {0}: output pad id {1} conflicts.",
                    persistedGroup.GroupId,
                    outputPad.Id);
                conflictingOutputPad = true;
                break;
            }

            outputPads.Add(outputPad);
            nextCombinedCadPadId = Math.Max(nextCombinedCadPadId, outputPad.Id + 1);
        }

        if (!conflictingOutputPad && outputPads.Count > 0)
        {
            return true;
        }

        foreach (var outputPad in outputPads)
        {
            currentPadIds.Remove(outputPad.Id);
        }

        return false;
    }

    private DxfCombinedGroupReplayResult ReplayPersistedCombinedCadGroups(List<ProjectDxfCombinedCadGroup> combinedGroups)
    {
        var replay = new DxfCombinedGroupReplayResult();
        if (_cad is null || combinedGroups.Count == 0)
        {
            return replay;
        }

        var currentPads = _cad.Pads.ToList();
        var currentPadIds = currentPads.Select(static pad => pad.Id).ToHashSet();
        var maxGroupId = 0;
        var nextCombinedCadPadId = _combinedCad.NextPadId;

        foreach (var persistedGroup in combinedGroups.OrderBy(static group => group.GroupId))
        {
            maxGroupId = Math.Max(maxGroupId, persistedGroup.GroupId);
            if (persistedGroup.SourceCadIds.Count == 0 || persistedGroup.OutputPads.Count == 0)
            {
                replay.SkippedGroups.Add(persistedGroup);
                continue;
            }

            var distinctSourceCadIds = persistedGroup.SourceCadIds
                .Distinct()
                .OrderBy(static id => id)
                .ToList();
            if (distinctSourceCadIds.Any(sourceCadId => !currentPadIds.Contains(sourceCadId)))
            {
                _logger.Warn(
                    CultureInfo.InvariantCulture,
                    "DXF edit replay skipped combined group {0}: source pad missing in baseline.",
                    persistedGroup.GroupId);
                replay.SkippedGroups.Add(persistedGroup);
                continue;
            }

            if (!TryBuildCombinedOutputPads(persistedGroup, currentPadIds, ref nextCombinedCadPadId, out var outputPads))
            {
                replay.SkippedGroups.Add(persistedGroup);
                continue;
            }

            var groupId = persistedGroup.GroupId > 0 ? persistedGroup.GroupId : _combinedCad.NextGroupId++;
            currentPads.AddRange(outputPads);
            _combinedCad.Groups[groupId] = new CombinedCadGroupState(
                distinctSourceCadIds,
                outputPads.Select(static pad => pad.Id).ToList());
            foreach (var outputPad in outputPads)
            {
                _combinedCad.GroupIdByOutputCadId[outputPad.Id] = groupId;
            }

            foreach (var sourceCadId in distinctSourceCadIds)
            {
                replay.CombinedSourceCadIds.Add(sourceCadId);
                _combinedCad.GroupIdBySourceCadId[sourceCadId] = groupId;
            }

            maxGroupId = Math.Max(maxGroupId, groupId);
        }

        _combinedCad.NextGroupId = Math.Max(_combinedCad.NextGroupId, maxGroupId + 1);
        _combinedCad.NextPadId = nextCombinedCadPadId;
        _cad = new CadPadSet(currentPads);
        return replay;
    }
}
