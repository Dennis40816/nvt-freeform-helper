// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns the project command instances for one workspace.</summary>
internal sealed record ProjectCommands(
    IAsyncRelayCommand SaveProjectCommand,
    IAsyncRelayCommand LoadProjectCommand);
