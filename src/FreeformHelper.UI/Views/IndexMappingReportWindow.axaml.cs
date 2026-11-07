// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace FreeformHelper.UI.Views;

public sealed partial class IndexMappingReportWindow : Window
{
    public IndexMappingReportWindow()
    {
        FocusSearchCommand = new RelayCommand(FocusSearch);
        InitializeComponent();
    }

    public IRelayCommand FocusSearchCommand { get; }

    private void FocusSearch() => FilterBar.FocusSearch();
}

