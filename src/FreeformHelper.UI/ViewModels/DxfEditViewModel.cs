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
    private DxfEditCommands? _dxfEditCommands;

    internal DxfEditViewModel(
        CadDocumentSession document, IDxfEditActions actions, DxfEditInteraction interaction,
        CadEditPersistence persistence, Func<bool> isEditingEnabled)
    {
        _document = document;
        _actions = actions;
        _interaction = interaction;
        _persistence = persistence;
        _isEditingEnabled = isEditingEnabled;
        DxfEditRotationScopeOptions = BuildDxfEditRotationScopeOptions();
        InitializeDxfEditOptionSelection();
    }

    private CadDocumentSession CadDocument => _document;
    private CadPadSet? _cad => _document.Cad;

    internal DxfEditPanelInput PanelInput => _dxfEditPanel;

    public bool IsProjectEditingEnabled => _isEditingEnabled();

    internal void NotifyEditingAvailabilityChanged() => OnPropertyChanged(nameof(IsProjectEditingEnabled));

    // Attached by the view, cleared on detach. The child and its commands survive project replacement.
    public Func<DxfEditChangeListViewModel, Task>? OpenDxfEditChangeListAsync { get; set; }

    private DxfEditCommands Commands =>
        _dxfEditCommands ?? throw new InvalidOperationException("DXF edit commands have not been initialized.");

    // Register once at the existing position in the project editing command registry.
    internal void InitializeCommands(
        Func<Action, IRelayCommand> createCommand, Func<Func<Task>, IAsyncRelayCommand> createAsyncCommand) =>
        _dxfEditCommands = CreateCommands(createCommand, createAsyncCommand);

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
    public IRelayCommand DeleteSelectedCadPadsCommand => Commands.DeleteSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to undo the last CAD hide action.
    /// </summary>
    public IRelayCommand RestoreLastDeletedCadPadsCommand => Commands.RestoreLastDeletedCadPadsCommand;

    /// <summary>
    /// Gets the command to restore all manually hidden CAD pads.
    /// </summary>
    public IRelayCommand RestoreAllHiddenCadPadsCommand => Commands.RestoreAllHiddenCadPadsCommand;

    /// <summary>
    /// Gets the command to combine selected CAD pads into one synthetic pad.
    /// </summary>
    public IRelayCommand CombineSelectedCadPadsCommand => Commands.CombineSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to clear the selected synthetic combined CAD groups.
    /// </summary>
    public IRelayCommand ClearCombinedCadPadsCommand => Commands.ClearCombinedCadPadsCommand;

    /// <summary>
    /// Gets the command to reset all DXF edits, including hidden, combined, and layer-move changes.
    /// </summary>
    public IRelayCommand ResetAllDxfEditsCommand => Commands.ResetAllDxfEditsCommand;

    /// <summary>
    /// Gets the command to open the Hidden DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenHiddenDxfEditChangeListCommand => Commands.OpenHiddenDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Duplicate DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenDuplicateDxfEditChangeListCommand => Commands.OpenDuplicateDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Combined DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenCombinedDxfEditChangeListCommand => Commands.OpenCombinedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Moved DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenMovedDxfEditChangeListCommand => Commands.OpenMovedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Rotated DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenRotatedDxfEditChangeListCommand => Commands.OpenRotatedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to move selected CAD pads to an existing DXF layer.
    /// </summary>
    public IRelayCommand MoveSelectedCadPadsToLayerCommand => Commands.MoveSelectedCadPadsToLayerCommand;

    /// <summary>
    /// Gets the command to create a new DXF layer and move selected CAD pads into it.
    /// </summary>
    public IRelayCommand CreateLayerAndMoveSelectedCadPadsCommand => Commands.CreateLayerAndMoveSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to rotate selected CAD pads around their own centroids.
    /// </summary>
    public IRelayCommand RotateSelectedCadPadsCommand => Commands.RotateSelectedCadPadsCommand;
}
