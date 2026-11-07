// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public sealed partial class WarningDialog : Window
{
    public WarningDialog() : this(string.Empty, string.Empty)
    {
    }

    public WarningDialog(string title, string message)
    {
        DataContext = new WarningDialogViewModel(title, message);
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OkButton_Click(object? sender, RoutedEventArgs e) => Close();
}
