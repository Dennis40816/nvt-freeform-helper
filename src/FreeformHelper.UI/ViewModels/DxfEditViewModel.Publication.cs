// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.CadEditing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class DxfEditViewModel
{
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

    internal void SyncHiddenCadIdsToProject(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= CaptureCadEditNotifications();
        var hadEdits = previous.HasAny;
        if (_cad is not null)
        {
            _persistence.SetHiddenIds(_cadEdit.HiddenCadPadIds.ToHashSet());
        }
        NotifyCadEditProjectionChange(nameof(DeletedCadPadCount), previous.Deleted, DeletedCadPadCount);
        NotifyCadEditProjectionChange(nameof(HasDeletedCadPads), previous.Deleted > 0, HasDeletedCadPads);
        previous.Deleted = DeletedCadPadCount;
        OnPropertyChanged(nameof(HiddenDxfEditBadgeTooltip));
        OnPropertyChanged(nameof(HasManualHiddenCadPads));
        SyncCadEditAggregateState(hadEdits, previous.HasAny);
    }

    internal void SyncCombinedCadState(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= CaptureCadEditNotifications();
        var hadEdits = previous.HasAny;
        NotifyCadEditProjectionChange(nameof(CombinedCadPadCount), previous.Combined, CombinedCadPadCount);
        NotifyCadEditProjectionChange(nameof(HasCombinedCadPads), previous.Combined > 0, HasCombinedCadPads);
        previous.Combined = CombinedCadPadCount;
        OnPropertyChanged(nameof(CombinedDxfEditBadgeTooltip));
        OnPropertyChanged(nameof(HasManualHiddenCadPads));
        OnPropertyChanged(nameof(HasSelectedCombinedCadPadsForDxfEdit));
        SyncCadEditAggregateState(hadEdits, previous.HasAny);
    }

    // Without a CAD, the saved visible duplicate ids stay in the project file (persistToProject: false).
    internal void SyncDuplicateCadState(CadEditNotificationSnapshot? previous = null, bool persistToProject = true)
    {
        previous ??= CaptureCadEditNotifications();
        NotifyCadEditProjectionChange(nameof(DuplicateCadPadCount), previous.Duplicate, DuplicateCadPadCount);
        NotifyCadEditProjectionChange(nameof(HasDuplicateCadPads), previous.Duplicate > 0, HasDuplicateCadPads);
        previous.Duplicate = DuplicateCadPadCount;
        if (persistToProject)
        {
            _persistence.SetVisibleDuplicateIds(CadDocument.GetRestoredDuplicateIds());
        }

        OnPropertyChanged(nameof(HasDetectedDuplicateCadPads));
        OnPropertyChanged(nameof(DuplicateDxfEditBadgeTooltip));
    }

    internal void SyncRelayeredCadState(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= CaptureCadEditNotifications();
        var hadEdits = previous.HasAny;
        if (_cadEdit.BaselineCad is null || _cad is null)
        {
            RelayeredCadPadCount = 0;
            NotifyCadEditProjectionChange(nameof(HasRelayeredCadPads), previous.Relayered > 0, HasRelayeredCadPads);
            previous.Relayered = RelayeredCadPadCount;
            SyncCadEditAggregateState(hadEdits, previous.HasAny);
            return;
        }

        RelayeredCadPadCount = DxfEditStateCapture.CountRelayeredPads(_cad, _cadEdit.BaselineCad);
        NotifyCadEditProjectionChange(nameof(HasRelayeredCadPads), previous.Relayered > 0, HasRelayeredCadPads);
        previous.Relayered = RelayeredCadPadCount;
        OnPropertyChanged(nameof(MovedDxfEditBadgeTooltip));
        SyncCadEditAggregateState(hadEdits, previous.HasAny);
    }

    internal void SyncRotatedCadState(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= CaptureCadEditNotifications();
        var hadEdits = previous.HasAny;
        if (_cadEdit.BaselineCad is null || _cad is null)
        {
            RotatedCadPadCount = 0;
            NotifyCadEditProjectionChange(nameof(HasRotatedCadPads), previous.Rotated > 0, HasRotatedCadPads);
            previous.Rotated = RotatedCadPadCount;
            SyncCadEditAggregateState(hadEdits, previous.HasAny);
            return;
        }

        RotatedCadPadCount = DxfEditStateCapture.CountRotatedPads(_cad, _cadEdit.BaselineCad);
        NotifyCadEditProjectionChange(nameof(HasRotatedCadPads), previous.Rotated > 0, HasRotatedCadPads);
        previous.Rotated = RotatedCadPadCount;
        OnPropertyChanged(nameof(RotatedDxfEditBadgeTooltip));
        SyncCadEditAggregateState(hadEdits, previous.HasAny);
    }

    private void SyncCadEditAggregateState(bool previous, bool current)
    {
        NotifyCadEditProjectionChange(nameof(HasAnyCadEdits), previous, current);
    }

    internal CadEditNotificationSnapshot CaptureCadEditNotifications() =>
        new(DeletedCadPadCount, DuplicateCadPadCount, CombinedCadPadCount, RelayeredCadPadCount, RotatedCadPadCount);
}
