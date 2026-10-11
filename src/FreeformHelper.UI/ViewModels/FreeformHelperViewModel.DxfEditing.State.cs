using System.Globalization;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // UI-thread owned, operation-local notification history; never retained on the ViewModel.
    // Advance each source only at its Sync point, including multi-source edits and DXF reloads.
    private sealed class CadEditNotificationSnapshot(FreeformHelperViewModel owner)
    {
        public int Deleted { get; set; } = owner.DeletedCadPadCount;
        public int Duplicate { get; set; } = owner.DuplicateCadPadCount;
        public int Combined { get; set; } = owner.CombinedCadPadCount;
        public int Relayered { get; set; } = owner.RelayeredCadPadCount;
        public int Rotated { get; set; } = owner.RotatedCadPadCount;
        public bool HasAny => HasDxfEditChanges(Deleted > 0, Combined > 0, Relayered > 0, Rotated > 0);
    }

    private static bool HasDxfEditChanges(bool deleted, bool combined, bool relayered, bool rotated) =>
        deleted || combined || relayered || rotated;

    private void NotifyCadEditProjectionChange<T>(string propertyName, T previous, T current)
    {
        if (EqualityComparer<T>.Default.Equals(previous, current))
        {
            return;
        }

        OnPropertyChanging(propertyName);
        OnPropertyChanged(propertyName);
    }

    private void ClearDxfEditRuntimeState(bool resetBaseline, CadEditNotificationSnapshot previous)
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
            ResetDxfEditBaselineCore(_cad, previous);
        }
    }

    private void ResetDxfEditBaseline(CadPadSet? cad)
    {
        ResetDxfEditBaselineCore(cad, new CadEditNotificationSnapshot(this));
    }

    private void ResetDxfEditBaselineCore(CadPadSet? cad, CadEditNotificationSnapshot previous)
    {
        _dxfEditBaselineCad = cad is null
            ? null
            : new CadPadSet(cad.Pads.ToList());
        SyncRelayeredCadState(previous);
        SyncRotatedCadState(previous);
    }

    private void SetHiddenCadPads(IEnumerable<int> hiddenIds, CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        _hiddenCadPadIds.Clear();

        if (_cad is null)
        {
            SyncHiddenCadIdsToProject(previous);
            SyncCombinedCadState(previous);
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

        SyncHiddenCadIdsToProject(previous);
        SyncCombinedCadState(previous);
        Logger.Debug(CultureInfo.InvariantCulture, "DXF hidden pads set from project. Hidden total={0}.", _hiddenCadPadIds.Count);
    }

    private void ResetHiddenCadPads(CadEditNotificationSnapshot previous)
    {
        ClearDxfEditRuntimeState(resetBaseline: true, previous);
        ClearAutoHiddenDuplicateCadPadState();
        SyncHiddenCadIdsToProject(previous);
        SyncCombinedCadState(previous);
        Logger.Debug(CultureInfo.InvariantCulture, "DXF hidden pads reset.");
    }

    private void SyncHiddenCadIdsToProject(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        var hadEdits = previous.HasAny;
        _projectFile.HiddenCadPadIds = _hiddenCadPadIds.ToHashSet();
        NotifyCadEditProjectionChange(nameof(DeletedCadPadCount), previous.Deleted, DeletedCadPadCount);
        NotifyCadEditProjectionChange(nameof(HasDeletedCadPads), previous.Deleted > 0, HasDeletedCadPads);
        previous.Deleted = DeletedCadPadCount;
        OnPropertyChanged(nameof(HiddenDxfEditBadgeTooltip));
        OnPropertyChanged(nameof(HasManualHiddenCadPads));
        SyncCadEditAggregateState(hadEdits, previous.HasAny);
    }

    private void SyncCombinedCadState(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        var hadEdits = previous.HasAny;
        NotifyCadEditProjectionChange(nameof(CombinedCadPadCount), previous.Combined, CombinedCadPadCount);
        NotifyCadEditProjectionChange(nameof(HasCombinedCadPads), previous.Combined > 0, HasCombinedCadPads);
        previous.Combined = CombinedCadPadCount;
        OnPropertyChanged(nameof(CombinedDxfEditBadgeTooltip));
        OnPropertyChanged(nameof(HasManualHiddenCadPads));
        OnPropertyChanged(nameof(HasSelectedCombinedCadPadsForDxfEdit));
        SyncCadEditAggregateState(hadEdits, previous.HasAny);
    }

    private void SyncDuplicateCadState(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        NotifyCadEditProjectionChange(nameof(DuplicateCadPadCount), previous.Duplicate, DuplicateCadPadCount);
        NotifyCadEditProjectionChange(nameof(HasDuplicateCadPads), previous.Duplicate > 0, HasDuplicateCadPads);
        previous.Duplicate = DuplicateCadPadCount;
        _projectFile.VisibleDuplicateCadPadIds = GetRestoredDuplicateCadPadIds();
        OnPropertyChanged(nameof(HasDetectedDuplicateCadPads));
        OnPropertyChanged(nameof(DuplicateDxfEditBadgeTooltip));
    }

    private void SyncRelayeredCadState(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        var hadEdits = previous.HasAny;
        if (_dxfEditBaselineCad is null || _cad is null)
        {
            RelayeredCadPadCount = 0;
            NotifyCadEditProjectionChange(nameof(HasRelayeredCadPads), previous.Relayered > 0, HasRelayeredCadPads);
            previous.Relayered = RelayeredCadPadCount;
            SyncCadEditAggregateState(hadEdits, previous.HasAny);
            return;
        }

        var baselineLayersByCadId = _dxfEditBaselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First().Layer);
        var relayeredCount = _cad.Pads.Count(pad =>
            baselineLayersByCadId.TryGetValue(pad.Id, out var baselineLayer) &&
            !string.Equals(baselineLayer, pad.Layer, StringComparison.OrdinalIgnoreCase));

        RelayeredCadPadCount = relayeredCount;
        NotifyCadEditProjectionChange(nameof(HasRelayeredCadPads), previous.Relayered > 0, HasRelayeredCadPads);
        previous.Relayered = RelayeredCadPadCount;
        OnPropertyChanged(nameof(MovedDxfEditBadgeTooltip));
        SyncCadEditAggregateState(hadEdits, previous.HasAny);
    }

    private void SyncRotatedCadState(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        var hadEdits = previous.HasAny;
        if (_dxfEditBaselineCad is null || _cad is null)
        {
            RotatedCadPadCount = 0;
            NotifyCadEditProjectionChange(nameof(HasRotatedCadPads), previous.Rotated > 0, HasRotatedCadPads);
            previous.Rotated = RotatedCadPadCount;
            SyncCadEditAggregateState(hadEdits, previous.HasAny);
            return;
        }

        var baselinePadsByCadId = _dxfEditBaselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First());
        var rotatedCount = _cad.Pads.Count(pad =>
            baselinePadsByCadId.TryGetValue(pad.Id, out var baselinePad) &&
            !ArePolygonVerticesEquivalent(baselinePad.Polygon, pad.Polygon));

        RotatedCadPadCount = rotatedCount;
        NotifyCadEditProjectionChange(nameof(HasRotatedCadPads), previous.Rotated > 0, HasRotatedCadPads);
        previous.Rotated = RotatedCadPadCount;
        OnPropertyChanged(nameof(RotatedDxfEditBadgeTooltip));
        SyncCadEditAggregateState(hadEdits, previous.HasAny);
    }

    private void SyncCadEditAggregateState(bool previous, bool current)
    {
        NotifyCadEditProjectionChange(nameof(HasAnyCadEdits), previous, current);
    }
}
