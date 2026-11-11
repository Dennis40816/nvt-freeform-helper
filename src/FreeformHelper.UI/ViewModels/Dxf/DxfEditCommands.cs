// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the DXF edit command instances for one workspace.</summary>
internal sealed record DxfEditCommands(
    IAsyncRelayCommand CheckDxfQualityCommand,
    IRelayCommand SelectDxfOverlapPadsCommand,
    IRelayCommand ClearDxfOverlapHighlightsCommand,
    IAsyncRelayCommand ShowDxfOverlapDetailsCommand,
    IAsyncRelayCommand AnalyzeIndexMappingCommand,
    IRelayCommand OffsetSelectedCadOutputFwDiffIndicesCommand,
    IRelayCommand<Step4DuplicateDiffGroupViewModel?> SelectStep4DuplicateDiffGroupCommand,
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
