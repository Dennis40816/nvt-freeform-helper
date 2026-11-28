// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.CadEditing;

internal sealed record DxfEditCommands(
    IRelayCommand DeleteSelectedCadPadsCommand,
    IRelayCommand RestoreLastDeletedCadPadsCommand,
    IRelayCommand RestoreAllHiddenCadPadsCommand,
    IRelayCommand CombineSelectedCadPadsCommand,
    IRelayCommand ClearCombinedCadPadsCommand,
    IRelayCommand ResetAllDxfEditsCommand,
    IAsyncRelayCommand OpenHiddenDxfEditChangeListCommand,
    IAsyncRelayCommand OpenDuplicateDxfEditChangeListCommand,
    IAsyncRelayCommand OpenCombinedDxfEditChangeListCommand,
    IAsyncRelayCommand OpenMovedDxfEditChangeListCommand,
    IAsyncRelayCommand OpenRotatedDxfEditChangeListCommand,
    IRelayCommand MoveSelectedCadPadsToLayerCommand,
    IRelayCommand CreateLayerAndMoveSelectedCadPadsCommand,
    IRelayCommand RotateSelectedCadPadsCommand);
