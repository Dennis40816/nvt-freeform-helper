using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

// Copyright (c) 2026 Dennis Liu. All rights reserved.

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
        _cadEdit.HiddenCadPadIds.Clear();
        _cadEdit.HiddenUndoStack.Clear();
        _combinedCad.Clear();
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
        _cadEdit.BaselineCad = cad is null
            ? null
            : new CadPadSet(cad.Pads.ToList());
        SyncRelayeredCadState(previous);
        SyncRotatedCadState(previous);
    }

    private void SetHiddenCadPads(IEnumerable<int> hiddenIds, CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        _cadEdit.HiddenCadPadIds.Clear();

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
                _cadEdit.HiddenCadPadIds.Add(id);
            }
        }

        SyncHiddenCadIdsToProject(previous);
        SyncCombinedCadState(previous);
        _logger.Debug(CultureInfo.InvariantCulture, "DXF hidden pads set from project. Hidden total={0}.", _cadEdit.HiddenCadPadIds.Count);
    }

    private void ResetHiddenCadPads(CadEditNotificationSnapshot previous)
    {
        ClearDxfEditRuntimeState(resetBaseline: true, previous);
        ClearAutoHiddenDuplicateCadPadState();
        SyncHiddenCadIdsToProject(previous);
        SyncCombinedCadState(previous);
        _logger.Debug(CultureInfo.InvariantCulture, "DXF hidden pads reset.");
    }

    private void SyncHiddenCadIdsToProject(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        var hadEdits = previous.HasAny;
        if (_cad is not null)
        {
            _projectFile.HiddenCadPadIds = _cadEdit.HiddenCadPadIds.ToHashSet();
        }
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

    // Without a CAD, the saved visible duplicate ids stay in the project file (persistToProject: false).
    private void SyncDuplicateCadState(CadEditNotificationSnapshot? previous = null, bool persistToProject = true)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        NotifyCadEditProjectionChange(nameof(DuplicateCadPadCount), previous.Duplicate, DuplicateCadPadCount);
        NotifyCadEditProjectionChange(nameof(HasDuplicateCadPads), previous.Duplicate > 0, HasDuplicateCadPads);
        previous.Duplicate = DuplicateCadPadCount;
        if (persistToProject)
        {
            _projectFile.VisibleDuplicateCadPadIds = GetRestoredDuplicateCadPadIds();
        }

        OnPropertyChanged(nameof(HasDetectedDuplicateCadPads));
        OnPropertyChanged(nameof(DuplicateDxfEditBadgeTooltip));
    }

    private void SyncRelayeredCadState(CadEditNotificationSnapshot? previous = null)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        var hadEdits = previous.HasAny;
        if (_cadEdit.BaselineCad is null || _cad is null)
        {
            RelayeredCadPadCount = 0;
            NotifyCadEditProjectionChange(nameof(HasRelayeredCadPads), previous.Relayered > 0, HasRelayeredCadPads);
            previous.Relayered = RelayeredCadPadCount;
            SyncCadEditAggregateState(hadEdits, previous.HasAny);
            return;
        }

        var baselineLayersByCadId = _cadEdit.BaselineCad.Pads
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
        if (_cadEdit.BaselineCad is null || _cad is null)
        {
            RotatedCadPadCount = 0;
            NotifyCadEditProjectionChange(nameof(HasRotatedCadPads), previous.Rotated > 0, HasRotatedCadPads);
            previous.Rotated = RotatedCadPadCount;
            SyncCadEditAggregateState(hadEdits, previous.HasAny);
            return;
        }

        var baselinePadsByCadId = _cadEdit.BaselineCad.Pads
            .GroupBy(static pad => pad.Id)
            .ToDictionary(static group => group.Key, static group => group.First());
        var rotatedCount = _cad.Pads.Count(pad =>
            baselinePadsByCadId.TryGetValue(pad.Id, out var baselinePad) &&
            !DxfEditRules.ArePolygonVerticesEquivalent(baselinePad.Polygon, pad.Polygon));

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

    /// <summary>
    /// Gets or sets the latest CAD?egular overlap links for canvas ratio labels.
    /// </summary>
    [ObservableProperty] private IReadOnlyList<PadMatchLink> _matchLinksForCanvas = Array.Empty<PadMatchLink>();

    /// <summary>
    /// Gets or sets a value indicating whether the AA canvas should show the shared CAD-load overlay.
    /// This is used by both Open DXF and Load Project while DXF import/reload and grid rebuild are in progress.
    /// </summary>
    [ObservableProperty] private bool _isCadLoadCanvasOverlayVisible;

    /// <summary>
    /// Gets or sets a value indicating whether the global modal loading spinner host should be visible.
    /// Long-running operations use this as a single entry/exit lifecycle rather than re-deriving ad hoc busy UI.
    /// </summary>
    [ObservableProperty] private bool _isModalLoadingSpinnerVisible;

    /// <summary>Gets the number of hidden CAD pads.</summary>
    public int DeletedCadPadCount => _cadEdit.HiddenCadPadIds.Count;

    /// <summary>Gets whether hidden CAD pads exist.</summary>
    public bool HasDeletedCadPads => DeletedCadPadCount > 0;

    /// <summary>Gets the number of detected duplicate CAD pads.</summary>
    public int DuplicateCadPadCount => _cadEdit.DuplicateCadPadIdToCanonicalId.Count;

    /// <summary>Gets whether detected duplicate CAD pads exist.</summary>
    public bool HasDuplicateCadPads => DuplicateCadPadCount > 0;

    /// <summary>Gets the number of synthetic combined CAD pads.</summary>
    public int CombinedCadPadCount => _combinedCad.OutputCadIds.Count;

    /// <summary>Gets whether synthetic combined CAD pads exist.</summary>
    public bool HasCombinedCadPads => CombinedCadPadCount > 0;

    /// <summary>
    /// Gets or sets the count of CAD pads whose layer differs from the current DXF edit baseline.
    /// </summary>
    // UI-thread owned; sync measures the O(n) baseline scan so binding reads stay O(1).
    [ObservableProperty] private int _relayeredCadPadCount;

    /// <summary>Gets whether any CAD pad has been moved to a different layer.</summary>
    public bool HasRelayeredCadPads => RelayeredCadPadCount > 0;

    /// <summary>
    /// Gets or sets the count of CAD pads whose polygon differs from the current DXF edit baseline.
    /// </summary>
    // UI-thread owned; sync measures the O(n) baseline scan so binding reads stay O(1).
    [ObservableProperty] private int _rotatedCadPadCount;

    /// <summary>Gets whether any CAD pad geometry differs from baseline.</summary>
    public bool HasRotatedCadPads => RotatedCadPadCount > 0;

    /// <summary>Gets whether hidden, combined, relayered or rotated CAD edits exist.</summary>
    public bool HasAnyCadEdits => HasDxfEditChanges(HasDeletedCadPads, HasCombinedCadPads, HasRelayeredCadPads, HasRotatedCadPads);

    /// <summary>
    /// Gets or sets available DXF layer names for "move selected pads" action.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _dxfEditLayerOptions = new();

    // The DXF edit panel inputs are stored once. The properties below forward to the record and raise
    // the same notifications and callbacks as the stored properties they replaced.
    private DxfEditPanelInput _dxfEditPanel = DxfEditPanelInput.Initial;

    /// <summary>
    /// Gets or sets the target DXF layer name used to move selected pads.
    /// </summary>
    public string DxfEditTargetLayerName
    {
        get => _dxfEditPanel.DxfEditTargetLayerName;
        set
        {
            if (_dxfEditPanel.DxfEditTargetLayerName == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { DxfEditTargetLayerName = value };
            OnDxfEditTargetLayerNameChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnDxfEditTargetLayerNameChanged(string value);

    /// <summary>
    /// Gets or sets the currently selected DXF rotation scope option.
    /// </summary>
    public DxfEditRotationScopeOption SelectedDxfEditRotationScopeOption
    {
        get => _dxfEditPanel.SelectedDxfEditRotationScopeOption;
        set
        {
            if (_dxfEditPanel.SelectedDxfEditRotationScopeOption == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { SelectedDxfEditRotationScopeOption = value };
            OnSelectedDxfEditRotationScopeOptionChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnSelectedDxfEditRotationScopeOptionChanged(DxfEditRotationScopeOption value);

    /// <summary>
    /// Gets or sets the target DXF layer name used when rotating by layer scope.
    /// </summary>
    public string DxfEditRotationLayerName
    {
        get => _dxfEditPanel.DxfEditRotationLayerName;
        set
        {
            if (_dxfEditPanel.DxfEditRotationLayerName == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { DxfEditRotationLayerName = value };
            OnDxfEditRotationLayerNameChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnDxfEditRotationLayerNameChanged(string value);

    /// <summary>
    /// Gets or sets the input text used to create a new DXF layer.
    /// </summary>
    public string DxfEditNewLayerName
    {
        get => _dxfEditPanel.DxfEditNewLayerName;
        set
        {
            if (_dxfEditPanel.DxfEditNewLayerName == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { DxfEditNewLayerName = value };
            OnDxfEditNewLayerNameChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnDxfEditNewLayerNameChanged(string value);

    /// <summary>
    /// Gets or sets the rotation angle for selected CAD pads in DXF edit workflow.
    /// </summary>
    public decimal DxfEditRotationDegrees
    {
        get => _dxfEditPanel.DxfEditRotationDegrees;
        set
        {
            if (_dxfEditPanel.DxfEditRotationDegrees == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { DxfEditRotationDegrees = value };
            OnDxfEditRotationDegreesChanged(value);
            OnPropertyChanged();
        }
    }

    partial void OnDxfEditRotationDegreesChanged(decimal value);

    /// <summary>
    /// Gets or sets a value indicating whether the advanced DXF edit block is expanded.
    /// </summary>
    public bool IsDxfEditAdvancedPanelExpanded
    {
        get => _dxfEditPanel.IsDxfEditAdvancedPanelExpanded;
        set
        {
            if (_dxfEditPanel.IsDxfEditAdvancedPanelExpanded == value)
            {
                return;
            }

            OnPropertyChanging();
            _dxfEditPanel = _dxfEditPanel with { IsDxfEditAdvancedPanelExpanded = value };
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets a read-only list of <see cref="DxfEditRotationScopeOption"/> objects for DXF rotation scope selection.
    /// </summary>
    public IReadOnlyList<DxfEditRotationScopeOption> DxfEditRotationScopeOptions { get; }

    private readonly CadEditState _cadEdit = new(); // Holds the hidden, duplicate and baseline data of the CAD edits.

    private readonly CombinedCadState _combinedCad = new();

    private static List<DxfEditRotationScopeOption> BuildDxfEditRotationScopeOptions() =>
        new List<DxfEditRotationScopeOption>
        {
            new(DxfEditRotationScope.SelectedPads, "Selected pads", "Rotate the currently selected baseline CAD pads as one rigid group."),
            new(DxfEditRotationScope.TargetLayer, "Target layer", "Rotate all visible baseline CAD pads in the selected rotation layer as one rigid group."),
        };

    /// <summary>Selects the first rotation-scope option in the edit panel.</summary>
    private void InitializeDxfEditOptionSelection()
    {
        _dxfEditPanel = _dxfEditPanel with { SelectedDxfEditRotationScopeOption = DxfEditRotationScopeOptions[0] };
    }
}
