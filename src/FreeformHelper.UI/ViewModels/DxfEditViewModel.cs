// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.CadEditing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class DxfEditViewModel : ObservableObject, IProjectEditingAvailability
{
    private readonly CadDocumentSession _document;
    private readonly IDxfEditActions _actions;
    private readonly DxfEditInteraction _interaction;
    private readonly CadEditPersistence _persistence;
    private readonly Func<bool> _isEditingEnabled;
    private readonly DxfEditCommands _dxfEditCommands;

    internal DxfEditViewModel(
        CadDocumentSession document, IDxfEditActions actions, DxfEditInteraction interaction,
        CadEditPersistence persistence, Func<bool> isEditingEnabled,
        Func<Action, IRelayCommand> createCommand, Func<Func<Task>, IAsyncRelayCommand> createAsyncCommand)
    {
        _document = document;
        _actions = actions;
        _interaction = interaction;
        _persistence = persistence;
        _isEditingEnabled = isEditingEnabled;
        DxfEditRotationScopeOptions = BuildDxfEditRotationScopeOptions();
        InitializeDxfEditOptionSelection();
        _dxfEditCommands = CreateCommands(createCommand, createAsyncCommand);
    }

    private CadDocumentSession CadDocument => _document;
    private CadPadSet? _cad => _document.Cad;

    internal DxfEditPanelInput PanelInput => _dxfEditPanel;

    public bool IsProjectEditingEnabled => _isEditingEnabled();

    internal void NotifyEditingAvailabilityChanged() => OnPropertyChanged(nameof(IsProjectEditingEnabled));

    // Attached by the view, cleared on detach. The child and its commands survive project replacement.
    public Func<DxfEditChangeListViewModel, Task>? OpenDxfEditChangeListAsync { get; set; }

    private DxfEditCommands CreateCommands(
        Func<Action, IRelayCommand> createCommand, Func<Func<Task>, IAsyncRelayCommand> createAsyncCommand)
    {
        return new DxfEditCommands(
            DeleteSelectedCadPadsCommand: createCommand(_actions.DeleteSelectedCadPads),
            RestoreLastDeletedCadPadsCommand: createCommand(_actions.RestoreLastDeletedCadPads),
            RestoreAllHiddenCadPadsCommand: createCommand(_actions.RestoreAllHiddenCadPads),
            CombineSelectedCadPadsCommand: createCommand(_actions.CombineSelectedCadPads),
            ClearCombinedCadPadsCommand: createCommand(_actions.ClearCombinedCadPads),
            ResetAllDxfEditsCommand: createCommand(_actions.ResetAllDxfEdits),
            OpenHiddenDxfEditChangeListCommand: createAsyncCommand(OpenHiddenDxfEditChangeListAsync),
            OpenDuplicateDxfEditChangeListCommand: createAsyncCommand(OpenDuplicateDxfEditChangeListAsync),
            OpenCombinedDxfEditChangeListCommand: createAsyncCommand(OpenCombinedDxfEditChangeListAsync),
            OpenMovedDxfEditChangeListCommand: createAsyncCommand(OpenMovedDxfEditChangeListAsync),
            OpenRotatedDxfEditChangeListCommand: createAsyncCommand(OpenRotatedDxfEditChangeListAsync),
            MoveSelectedCadPadsToLayerCommand: createCommand(_actions.MoveSelectedCadPadsToLayer),
            CreateLayerAndMoveSelectedCadPadsCommand: createCommand(_actions.CreateLayerAndMoveSelectedCadPads),
            RotateSelectedCadPadsCommand: createCommand(_actions.RotateSelectedCadPads));
    }

    /// <summary>
    /// Gets the command to hide selected CAD pads.
    /// </summary>
    public IRelayCommand DeleteSelectedCadPadsCommand => _dxfEditCommands.DeleteSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to undo the last CAD hide action.
    /// </summary>
    public IRelayCommand RestoreLastDeletedCadPadsCommand => _dxfEditCommands.RestoreLastDeletedCadPadsCommand;

    /// <summary>
    /// Gets the command to restore all manually hidden CAD pads.
    /// </summary>
    public IRelayCommand RestoreAllHiddenCadPadsCommand => _dxfEditCommands.RestoreAllHiddenCadPadsCommand;

    /// <summary>
    /// Gets the command to combine selected CAD pads into one synthetic pad.
    /// </summary>
    public IRelayCommand CombineSelectedCadPadsCommand => _dxfEditCommands.CombineSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to clear the selected synthetic combined CAD groups.
    /// </summary>
    public IRelayCommand ClearCombinedCadPadsCommand => _dxfEditCommands.ClearCombinedCadPadsCommand;

    /// <summary>
    /// Gets the command to reset all DXF edits, including hidden, combined, and layer-move changes.
    /// </summary>
    public IRelayCommand ResetAllDxfEditsCommand => _dxfEditCommands.ResetAllDxfEditsCommand;

    /// <summary>
    /// Gets the command to open the Hidden DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenHiddenDxfEditChangeListCommand => _dxfEditCommands.OpenHiddenDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Duplicate DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenDuplicateDxfEditChangeListCommand => _dxfEditCommands.OpenDuplicateDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Combined DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenCombinedDxfEditChangeListCommand => _dxfEditCommands.OpenCombinedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Moved DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenMovedDxfEditChangeListCommand => _dxfEditCommands.OpenMovedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Rotated DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenRotatedDxfEditChangeListCommand => _dxfEditCommands.OpenRotatedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to move selected CAD pads to an existing DXF layer.
    /// </summary>
    public IRelayCommand MoveSelectedCadPadsToLayerCommand => _dxfEditCommands.MoveSelectedCadPadsToLayerCommand;

    /// <summary>
    /// Gets the command to create a new DXF layer and move selected CAD pads into it.
    /// </summary>
    public IRelayCommand CreateLayerAndMoveSelectedCadPadsCommand => _dxfEditCommands.CreateLayerAndMoveSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to rotate selected CAD pads around their own centroids.
    /// </summary>
    public IRelayCommand RotateSelectedCadPadsCommand => _dxfEditCommands.RotateSelectedCadPadsCommand;
}
