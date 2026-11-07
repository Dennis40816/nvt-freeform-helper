// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Input;

namespace FreeformHelper.UI.Views.WorkspaceSections;

public sealed partial class IndexMappingReportFilterBarView : UserControl
{
    public IndexMappingReportFilterBarView()
    {
        InitializeComponent();
    }

    public void FocusSearch()
    {
        SearchBox.Focus(NavigationMethod.Tab);
        SearchBox.SelectAll();
    }
}
