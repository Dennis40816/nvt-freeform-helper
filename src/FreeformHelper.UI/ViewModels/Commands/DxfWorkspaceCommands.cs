// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

// Commands owned by the later quality and mapping slices.
internal sealed record DxfWorkspaceCommands(
    IAsyncRelayCommand CheckDxfQualityCommand,
    IRelayCommand SelectDxfOverlapPadsCommand,
    IRelayCommand ClearDxfOverlapHighlightsCommand,
    IAsyncRelayCommand ShowDxfOverlapDetailsCommand,
    IAsyncRelayCommand AnalyzeIndexMappingCommand,
    IRelayCommand OffsetSelectedCadOutputFwDiffIndicesCommand,
    IRelayCommand<Step4DuplicateDiffGroupViewModel?> SelectStep4DuplicateDiffGroupCommand);
