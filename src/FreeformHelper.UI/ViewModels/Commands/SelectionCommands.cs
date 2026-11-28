// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the selection command instances for one workspace.</summary>
internal sealed record SelectionCommands(
    IRelayCommand ClearSelectionCommand,
    IRelayCommand QuickFocusCommand);
