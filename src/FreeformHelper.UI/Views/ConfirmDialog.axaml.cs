// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

/// <summary>Shows caller-supplied confirmation content without changing the operation's result contract.</summary>
public sealed partial class ConfirmDialog : Window
{
    public ConfirmDialog() : this(string.Empty, string.Empty, string.Empty, string.Empty)
    {
    }

    public ConfirmDialog(
        string title, string message, string confirmText, string cancelText, bool emphasizeCancel = false,
        string? confirmTip = null, string? cancelTip = null, string? consequenceText = null,
        string? nextStartMessage = null, string? dismissText = null, string? dismissTip = null)
    {
        DataContext = new ConfirmDialogViewModel(
            title, message, confirmText, cancelText, emphasizeCancel, confirmTip, cancelTip,
            consequenceText, nextStartMessage, dismissText, dismissTip);
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void DismissButton_Click(object? sender, RoutedEventArgs e) => Close();
}
