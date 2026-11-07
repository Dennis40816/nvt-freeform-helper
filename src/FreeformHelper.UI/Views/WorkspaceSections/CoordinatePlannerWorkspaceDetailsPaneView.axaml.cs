// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;

namespace FreeformHelper.UI.Views.WorkspaceSections;

public partial class CoordinatePlannerWorkspaceDetailsPaneView : UserControl
{
    public static readonly StyledProperty<bool> ShowPixelColumnProperty =
        AvaloniaProperty.Register<CoordinatePlannerWorkspaceDetailsPaneView, bool>(nameof(ShowPixelColumn));

    public static readonly StyledProperty<bool> ShowMachineColumnProperty =
        AvaloniaProperty.Register<CoordinatePlannerWorkspaceDetailsPaneView, bool>(nameof(ShowMachineColumn));

    public CoordinatePlannerWorkspaceDetailsPaneView()
    {
        InitializeComponent();
    }

    public bool ShowPixelColumn
    {
        get => GetValue(ShowPixelColumnProperty);
        set => SetValue(ShowPixelColumnProperty, value);
    }

    public bool ShowMachineColumn
    {
        get => GetValue(ShowMachineColumnProperty);
        set => SetValue(ShowMachineColumnProperty, value);
    }
}
