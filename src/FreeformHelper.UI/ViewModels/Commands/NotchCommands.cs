// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the notch command instances for one workspace.</summary>
internal sealed record NotchCommands(
    IAsyncRelayCommand ExportNotchCommand,
    IAsyncRelayCommand ShowNotchDetailCommand,
    IRelayCommand UseSelectedRegularForNotchValidationCommand,
    IRelayCommand AnalyzeNotchValidationCommand,
    IRelayCommand ClearNotchValidationCommand,
    IRelayCommand<FreeformHelperViewModel.NotchValidationDisplayItem> FocusNotchValidationItemCommand,
    IRelayCommand PreviousNotchPreviewStageCommand,
    IRelayCommand NextNotchPreviewStageCommand);
