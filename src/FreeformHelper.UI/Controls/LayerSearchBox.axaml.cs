// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FreeformHelper.UI.Controls;

/// <summary>The layer name search box shared by the main window and the layer image export window.</summary>
public sealed partial class LayerSearchBox : UserControl
{
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<LayerSearchBox, string>(
            nameof(Query), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public LayerSearchBox()
    {
        InitializeComponent();
    }

    /// <summary>The text typed by the user. It only filters a list; it never changes a layer.</summary>
    public string Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    /// <summary>Moves keyboard focus into the box and selects the current text.</summary>
    public void FocusSearch()
    {
        SearchBox.Focus(NavigationMethod.Tab);
        SearchBox.SelectAll();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(Query))
        {
            Clear();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void ClearSearchButton_Click(object? sender, RoutedEventArgs e) => Clear();

    // SetCurrentValue keeps the owner's binding on Query.
    private void Clear() => SetCurrentValue(QueryProperty, string.Empty);
}
