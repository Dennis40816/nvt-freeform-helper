using System.Globalization;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void ClearDxfEditRuntimeState(bool resetBaseline)
    {
        _hiddenCadPadIds.Clear();
        _hiddenCadUndoStack.Clear();
        _combinedCadPadIds.Clear();
        _combinedSourceCadPadIds.Clear();
        _combinedCadGroupIdByOutputCadId.Clear();
        _combinedCadGroupIdBySourceCadId.Clear();
        _combinedCadGroups.Clear();
        _nextCombinedCadGroupId = 1;
        _nextCombinedCadPadId = 1_500_000_000;
        if (resetBaseline)
        {
            ResetDxfEditBaseline(_cad);
        }
    }

    private void ResetDxfEditBaseline(CadPadSet? cad)
    {
        _dxfEditBaselineCad = cad is null
            ? null
            : new CadPadSet(cad.Pads.ToList());
        SyncRelayeredCadState();
        SyncRotatedCadState();
    }

    private void SetHiddenCadPads(IEnumerable<int> hiddenIds)
    {
        _hiddenCadPadIds.Clear();

        if (_cad is null)
        {
            SyncHiddenCadIdsToProject();
            SyncCombinedCadState();
            return;
        }

        var valid = _cad.Pads.Select(p => p.Id).ToHashSet();
        foreach (var id in hiddenIds)
        {
            if (valid.Contains(id))
            {
                _hiddenCadPadIds.Add(id);
            }
        }

        SyncHiddenCadIdsToProject();
        SyncCombinedCadState();
        Logger.Debug(CultureInfo.InvariantCulture, "DXF hidden pads set from project. Hidden total={0}.", _hiddenCadPadIds.Count);
    }

    private void ResetHiddenCadPads()
    {
        ClearDxfEditRuntimeState(resetBaseline: true);
        ClearAutoHiddenDuplicateCadPadState();
        SyncHiddenCadIdsToProject();
        SyncCombinedCadState();
        Logger.Debug(CultureInfo.InvariantCulture, "DXF hidden pads reset.");
    }

    private void SyncHiddenCadIdsToProject()
    {
        _projectFile.HiddenCadPadIds = _hiddenCadPadIds.ToHashSet();
        DeletedCadPadCount = _hiddenCadPadIds.Count;
        HasDeletedCadPads = DeletedCadPadCount > 0;
        OnPropertyChanged(nameof(HiddenDxfEditBadgeTooltip));
        OnPropertyChanged(nameof(HasManualHiddenCadPads));
        SyncCadEditAggregateState();
    }

    private void SyncCombinedCadState()
    {
        CombinedCadPadCount = _combinedCadPadIds.Count;
        HasCombinedCadPads = CombinedCadPadCount > 0;
        OnPropertyChanged(nameof(CombinedDxfEditBadgeTooltip));
        OnPropertyChanged(nameof(HasManualHiddenCadPads));
        OnPropertyChanged(nameof(HasSelectedCombinedCadPadsForDxfEdit));
        SyncCadEditAggregateState();
    }

    private void SyncDuplicateCadState()
    {
        DuplicateCadPadCount = _duplicateCadPadIdToCanonicalId.Count;
        HasDuplicateCadPads = DuplicateCadPadCount > 0;
        _projectFile.VisibleDuplicateCadPadIds = GetRestoredDuplicateCadPadIds();
        OnPropertyChanged(nameof(HasDetectedDuplicateCadPads));
        OnPropertyChanged(nameof(DuplicateDxfEditBadgeTooltip));
    }

    private void SyncRelayeredCadState()
    {
        if (_dxfEditBaselineCad is null || _cad is null)
        {
            RelayeredCadPadCount = 0;
            HasRelayeredCadPads = false;
            SyncCadEditAggregateState();
            return;
        }

        var baselineLayersByCadId = _dxfEditBaselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First().Layer);
        var relayeredCount = _cad.Pads.Count(pad =>
            baselineLayersByCadId.TryGetValue(pad.Id, out var baselineLayer) &&
            !string.Equals(baselineLayer, pad.Layer, StringComparison.OrdinalIgnoreCase));

        RelayeredCadPadCount = relayeredCount;
        HasRelayeredCadPads = relayeredCount > 0;
        OnPropertyChanged(nameof(MovedDxfEditBadgeTooltip));
        SyncCadEditAggregateState();
    }

    private void SyncRotatedCadState()
    {
        if (_dxfEditBaselineCad is null || _cad is null)
        {
            RotatedCadPadCount = 0;
            HasRotatedCadPads = false;
            SyncCadEditAggregateState();
            return;
        }

        var baselinePadsByCadId = _dxfEditBaselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First());
        var rotatedCount = _cad.Pads.Count(pad =>
            baselinePadsByCadId.TryGetValue(pad.Id, out var baselinePad) &&
            !ArePolygonVerticesEquivalent(baselinePad.Polygon, pad.Polygon));

        RotatedCadPadCount = rotatedCount;
        HasRotatedCadPads = rotatedCount > 0;
        OnPropertyChanged(nameof(RotatedDxfEditBadgeTooltip));
        SyncCadEditAggregateState();
    }

    private void SyncCadEditAggregateState()
    {
        HasAnyCadEdits = HasDeletedCadPads || HasCombinedCadPads || HasRelayeredCadPads || HasRotatedCadPads;
    }
}
