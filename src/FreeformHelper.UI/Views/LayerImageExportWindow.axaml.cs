// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Converters;

namespace FreeformHelper.UI.Views;

public sealed partial class LayerImageExportWindow : Window
{
    public LayerImageExportWindow()
    {
        FocusSearchCommand = new RelayCommand(FocusSearch);
        InitializeComponent();
    }

    public IRelayCommand FocusSearchCommand { get; }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LayerList.Focus();
    }

    private void FocusSearch()
    {
        SearchBox.Focus(NavigationMethod.Tab);
        SearchBox.SelectAll();
    }

    protected override void OnClosed(EventArgs e)
    {
        PreviewImage.Source = null;
        ((LayerPreviewBitmapConverter)Resources["PreviewBitmapConverter"]!).Dispose();
        base.OnClosed(e);
    }
}
