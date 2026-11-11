// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.UI.CadEditing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class DxfEditViewModel
{
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
    public int RelayeredCadPadCount
    {
        get => CadDocument.EditDelta.Relayered;
        set
        {
            if (value == RelayeredCadPadCount)
            {
                return;
            }

            OnPropertyChanging(nameof(RelayeredCadPadCount));
            CadDocument.EditDelta = CadDocument.EditDelta with { Relayered = value };
            OnPropertyChanged(nameof(RelayeredCadPadCount));
        }
    }

    /// <summary>Gets whether any CAD pad has been moved to a different layer.</summary>
    public bool HasRelayeredCadPads => RelayeredCadPadCount > 0;

    /// <summary>
    /// Gets or sets the count of CAD pads whose polygon differs from the current DXF edit baseline.
    /// </summary>
    // UI-thread owned; sync measures the O(n) baseline scan so binding reads stay O(1).
    public int RotatedCadPadCount
    {
        get => CadDocument.EditDelta.Rotated;
        set
        {
            if (value == RotatedCadPadCount)
            {
                return;
            }

            OnPropertyChanging(nameof(RotatedCadPadCount));
            CadDocument.EditDelta = CadDocument.EditDelta with { Rotated = value };
            OnPropertyChanged(nameof(RotatedCadPadCount));
        }
    }

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

    private CadEditState _cadEdit => CadDocument.Edit;

    private CombinedCadState _combinedCad => CadDocument.Combined;

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
