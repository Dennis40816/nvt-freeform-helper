// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Controls;

/// <summary>Hosts the current workspace command and the existing export restore entry.</summary>
public partial class WorkspaceActionBar : UserControl
{
    public event EventHandler<RoutedEventArgs>? RestoreRequested;

    public WorkspaceActionBar()
    {
        InitializeComponent();
    }

    private void RestoreButton_Click(object? sender, RoutedEventArgs e)
    {
        RestoreRequested?.Invoke(this, e);
    }
}
