// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the general command instances for one workspace.</summary>
internal sealed record GeneralCommands(
    IAsyncRelayCommand OpenDxfCommand,
    IAsyncRelayCommand MatchCommand,
    IAsyncRelayCommand ImportRegularVisibilityMaskCommand,
    IRelayCommand ClearRegularVisibilityMaskCommand,
    IAsyncRelayCommand AutoDetectFreeformsCommand,
    IRelayCommand ClearStep1ResultCommand,
    IRelayCommand ClearStep2ResultCommand,
    IRelayCommand ClearStep3ResultCommand,
    IRelayCommand ClearStep4ResultCommand,
    IRelayCommand ClearStep5ResultCommand,
    IRelayCommand FitCommand,
    IRelayCommand UndoCommand,
    IRelayCommand SetFreeformNoneCommand,
    IRelayCommand SetFreeformXCommand,
    IRelayCommand SetFreeformYCommand,
    IRelayCommand SetFreeformXYCommand);
