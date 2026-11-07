// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class SimulationWorkspaceView
{
    private void InitializeSimulationConsole()
    {
        // UI thread only: the existing toggle owns this workspace's expansion, independently of the shell.
        var console = SimulationConsole;
        var toggle = console.FindControl<ToggleButton>("ConsoleToggleButton")
            ?? throw new InvalidOperationException("Console toggle not found.");
        var border = console.FindControl<Border>("ConsoleBorder")
            ?? throw new InvalidOperationException("Console border not found.");
        var content = (Grid)border.Child!;
        var body = content.Children.OfType<Grid>().Single(static grid => Grid.GetRow(grid) == 1);
        toggle.ClearValue(ToggleButton.IsCheckedProperty);
        toggle.IsChecked = false;
        body.Bind(IsVisibleProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = toggle });
        toggle.PropertyChanged += (_, args) =>
        {
            if (args.Property == ToggleButton.IsCheckedProperty)
            {
                console.Classes.Set("expanded", toggle.IsChecked == true);
                RestoreLowerRegionAutoHeight();
            }
        };
        var replay = CopperReplayView.FindControl<HidePanelBlock>("CopperReplayPanel")
            ?? throw new InvalidOperationException("Copper replay panel not found.");
        replay.PropertyChanged += (_, args) =>
        {
            if (args.Property == HidePanelBlock.IsExpandedProperty)
            {
                RestoreLowerRegionAutoHeight();
            }
        };
    }

    private void RestoreLowerRegionAutoHeight()
    {
        SimulationCenterGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        SimulationCenterGrid.RowDefinitions[3].Height = GridLength.Auto;
    }

    private void OnWorkspacePresentationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SimulationWorkspaceViewModel.SelectedInputSourceOption))
        {
            RestoreLowerRegionAutoHeight();
        }

        if (e.PropertyName == nameof(SimulationWorkspaceViewModel.ShowSelectedOverrideEditor))
        {
            if (_attachedViewModel?.ShowSelectedOverrideEditor != true)
            {
                _isInlineEditorOpen = false;
            }

            UpdateInlineEditorVisibility();
            QueueInlineEditorLayoutUpdate();
        }
    }

    private void InlineEdit_Click(object? sender, RoutedEventArgs e)
    {
        OpenInlineEditor();
        _uiEvents?.Run("Simulation.EditRegularPad", _ => FocusInlineEditorAsync(), CancellationToken.None);
    }

    private void InlineClose_Click(object? sender, RoutedEventArgs e)
    {
        CloseInlineEditor();
        QueueInlineEditorLayoutUpdate();
        FindCanvas().Focus();
    }

    internal void FocusRiskPad(int regularPadId)
    {
        Dispatcher.Post(() =>
        {
            var canvas = FindCanvas();
            canvas.SetSelection(Array.Empty<int>(), [regularPadId]);
            SimulationRiskMarker.IsVisible = true;
            var rect = PositionRiskLocation();
            if (rect.HasValue && !new Rect(SimulationRiskOverlay.Bounds.Size).Contains(rect.Value))
            {
                canvas.FocusSelection();
            }
        });
    }

    private Rect? PositionRiskLocation()
    {
        if (!SimulationRiskMarker.IsVisible || DataContext is not SimulationWorkspaceViewModel viewModel)
        {
            return null;
        }

        var pad = viewModel.RegularPads.FirstOrDefault(pad => pad.RegularPadId == viewModel.SelectedRegularPadId);
        if (pad is null)
        {
            HideRiskLocation();
            return null;
        }

        var rect = BuildPadScreenRect(FindViewportAdapter(), SimulationRiskOverlay, pad.Bounds);
        Canvas.SetLeft(SimulationRiskMarker, rect.Left);
        Canvas.SetTop(SimulationRiskMarker, rect.Top);
        SimulationRiskMarker.Width = rect.Width;
        SimulationRiskMarker.Height = rect.Height;
        return rect;
    }

    private void HideRiskLocation() => SimulationRiskMarker.IsVisible = false;
}
