// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Views.WorkspaceSections;

public partial class CoordinatePlannerCustomSectionView : UserControl
{
    public CoordinatePlannerCustomSectionView()
    {
        InitializeComponent();
    }

    private void PointForm_Click(object? sender, RoutedEventArgs e) => SelectForm(false);

    private void PathForm_Click(object? sender, RoutedEventArgs e) => SelectForm(true);

    private void SelectForm(bool path)
    {
        this.FindControl<ToggleButton>("PointFormButton")?.SetCurrentValue(ToggleButton.IsCheckedProperty, !path);
        this.FindControl<ToggleButton>("PathFormButton")?.SetCurrentValue(ToggleButton.IsCheckedProperty, path);
    }
}
