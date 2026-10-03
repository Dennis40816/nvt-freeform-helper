using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private sealed record PersistedDxfEditProjectState(
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
            file.DxfCombinedCadGroups = new List<ProjectDxfCombinedCadGroup>();
            return;
        }

        var validCadIds = _cad.Pads.Select(static pad => pad.Id).ToHashSet();
        file.HiddenCadPadIds = _hiddenCadPadIds
            .Where(validCadIds.Contains)
            .ToHashSet();
        file.VisibleDuplicateCadPadIds = GetRestoredDuplicateCadPadIds()
            .Where(validCadIds.Contains)
            .ToHashSet();
        file.DxfCadLayerOverrides = CapturePersistedCadLayerOverrides();
        file.DxfCadGeometryOverrides = CapturePersistedCadGeometryOverrides();
        file.DxfCombinedCadGroups = CapturePersistedCombinedCadGroups();
    }

    private static PersistedDxfEditProjectState CapturePersistedDxfEditProjectState(ProjectFile file)
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
        if (_cad is null || _dxfEditBaselineCad is null)
        {
            return new Dictionary<int, string>();
        }

        var baselineLayersByCadId = _dxfEditBaselineCad.Pads
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
        if (_cad is null || _combinedCadGroups.Count == 0)
        {
            return new List<ProjectDxfCombinedCadGroup>();
        }

        var currentPadsById = _cad.Pads.ToDictionary(static pad => pad.Id);
        var groups = new List<ProjectDxfCombinedCadGroup>();
        foreach (var pair in _combinedCadGroups.OrderBy(static pair => pair.Key))
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

        return groups;
    }

    private void ApplyPersistedDxfEditProjectState(ProjectFile file)
    {
        ApplyCapturedDxfEditProjectState(CapturePersistedDxfEditProjectState(file));
    }

    private void ApplyCapturedDxfEditProjectState(PersistedDxfEditProjectState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_cad is null)
        {
            ClearDxfEditRuntimeState(resetBaseline: false);
            ClearAutoHiddenDuplicateCadPadState();
            SyncRelayeredCadState();
            SyncRotatedCadState();
            SyncHiddenCadIdsToProject();
            SyncCombinedCadState();
            SyncDuplicateCadState();
            return;
        }

        var layerSelectionSnapshot = CaptureLayerSelectionSnapshot();
        PreserveDxfDependentLayerSelections();
        ClearDxfEditRuntimeState(resetBaseline: false);
        ApplyPersistedCadLayerOverrides(state.CadLayerOverrides);
        ApplyPersistedCadGeometryOverrides(state.CadGeometryOverrides);
        var combinedSourceCadIds = ReplayPersistedCombinedCadGroups(state.CombinedCadGroups);
        SetHiddenCadPads(state.HiddenCadPadIds.Concat(combinedSourceCadIds));
        ApplyPersistedVisibleDuplicateCadPads(state.VisibleDuplicateCadPadIds);
        _hiddenCadUndoStack.Clear();
        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState();
        SyncRotatedCadState();
        SyncCombinedCadState();
        UpdateLayerToggles(_cad);
        RestoreLayerSelectionAfterCadLayerEdit(layerSelectionSnapshot, ensureVisibleLayerName: string.Empty);
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

    private HashSet<int> ReplayPersistedCombinedCadGroups(List<ProjectDxfCombinedCadGroup> combinedGroups)
    {
        var combinedSourceCadIds = new HashSet<int>();
        if (_cad is null || combinedGroups.Count == 0)
        {
            return combinedSourceCadIds;
        }

        var currentPads = _cad.Pads.ToList();
        var currentPadIds = currentPads.Select(static pad => pad.Id).ToHashSet();
        var maxGroupId = 0;
        var nextCombinedCadPadId = _nextCombinedCadPadId;

        foreach (var persistedGroup in combinedGroups.OrderBy(static group => group.GroupId))
        {
            if (persistedGroup.SourceCadIds.Count == 0 || persistedGroup.OutputPads.Count == 0)
            {
                continue;
            }

            var distinctSourceCadIds = persistedGroup.SourceCadIds
                .Distinct()
                .OrderBy(static id => id)
                .ToList();
            if (distinctSourceCadIds.Any(sourceCadId => !currentPadIds.Contains(sourceCadId)))
            {
                Logger.Warn(
                    CultureInfo.InvariantCulture,
                    "DXF edit replay skipped combined group {0}: source pad missing in baseline.",
                    persistedGroup.GroupId);
                continue;
            }

            var outputPads = new List<CadPad>(persistedGroup.OutputPads.Count);
            var conflictingOutputPad = false;
            foreach (var outputPadSnapshot in persistedGroup.OutputPads)
            {
                CadPad outputPad;
                try
                {
                    outputPad = outputPadSnapshot.ToCadPad();
                }
                catch (Exception ex)
                {
                    Logger.Warn(
                        ex,
                        "DXF edit replay skipped combined group {0}: output pad snapshot invalid.",
                        persistedGroup.GroupId);
                    conflictingOutputPad = true;
                    break;
                }

                if (!currentPadIds.Add(outputPad.Id))
                {
                    Logger.Warn(
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

            if (conflictingOutputPad || outputPads.Count == 0)
            {
                foreach (var outputPad in outputPads)
                {
                    currentPadIds.Remove(outputPad.Id);
                }

                continue;
            }

            var groupId = persistedGroup.GroupId > 0 ? persistedGroup.GroupId : _nextCombinedCadGroupId++;
            currentPads.AddRange(outputPads);
            _combinedCadGroups[groupId] = new CombinedCadGroupState(
                distinctSourceCadIds,
                outputPads.Select(static pad => pad.Id).ToList());
            foreach (var outputPad in outputPads)
            {
                _combinedCadPadIds.Add(outputPad.Id);
                _combinedCadGroupIdByOutputCadId[outputPad.Id] = groupId;
            }

            foreach (var sourceCadId in distinctSourceCadIds)
            {
                _combinedSourceCadPadIds.Add(sourceCadId);
                combinedSourceCadIds.Add(sourceCadId);
                _combinedCadGroupIdBySourceCadId[sourceCadId] = groupId;
            }

            maxGroupId = Math.Max(maxGroupId, groupId);
        }

        _nextCombinedCadGroupId = Math.Max(_nextCombinedCadGroupId, maxGroupId + 1);
        _nextCombinedCadPadId = nextCombinedCadPadId;
        _cad = new CadPadSet(currentPads);
        return combinedSourceCadIds;
    }
}
