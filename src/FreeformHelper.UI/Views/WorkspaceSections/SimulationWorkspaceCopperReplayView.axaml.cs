// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Interactivity;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views.WorkspaceSections;

public sealed partial class SimulationWorkspaceCopperReplayView : UserControl
{
    public SimulationWorkspaceCopperReplayView()
    {
        InitializeComponent();
    }

    private void ReplayRow_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SimulationWorkspaceViewModel viewModel &&
            sender is Button { CommandParameter: CopperPillarPathReplayArtifactRow row })
        {
            viewModel.SelectCopperPathReplayRowCommand.Execute(row);
        }
    }

    private void ReplayRows_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SimulationWorkspaceViewModel viewModel &&
            sender is ListBox { SelectedItem: CopperPillarPathReplayArtifactRow row } &&
            !ReferenceEquals(viewModel.SelectedCopperPathReplayRow, row))
        {
            viewModel.SelectCopperPathReplayRowCommand.Execute(row);
        }
    }
}
