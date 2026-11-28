// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Views;

public enum LeftPanelSection { Expand, Dxf, Layers, Edits }

public sealed partial class LeftPanelRail : UserControl
{
    public static readonly Avalonia.StyledProperty<ObservableCollection<LayerToggle>?> LayersProperty =
        Avalonia.AvaloniaProperty.Register<LeftPanelRail, ObservableCollection<LayerToggle>?>(nameof(Layers));

    private readonly List<LayerToggle> _observedLayers = [];
    private ObservableCollection<LayerToggle>? _observedCollection;

    public LeftPanelRail()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => ObserveLayers();
        DetachedFromVisualTree += (_, _) => StopObservingLayers();
    }

    public event EventHandler<LeftPanelSection>? SectionRequested;

    public ObservableCollection<LayerToggle>? Layers
    {
        get => GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayersProperty)
        {
            ObserveLayers();
        }
    }

    private void ObserveLayers()
    {
        StopObservingLayers();
        _observedCollection = Layers;
        if (_observedCollection is not null)
        {
            _observedCollection.CollectionChanged += OnLayersChanged;
            _observedLayers.AddRange(_observedCollection);
            foreach (var layer in _observedLayers)
            {
                layer.PropertyChanged += OnLayerChanged;
            }
        }

        RefreshCount();
    }

    private void StopObservingLayers()
    {
        if (_observedCollection is not null)
        {
            _observedCollection.CollectionChanged -= OnLayersChanged;
        }

        foreach (var layer in _observedLayers)
        {
            layer.PropertyChanged -= OnLayerChanged;
        }

        _observedLayers.Clear();
        _observedCollection = null;
    }

    private void OnLayersChanged(object? sender, NotifyCollectionChangedEventArgs e) => ObserveLayers();

    private void OnLayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayerToggle.IsSelected))
        {
            RefreshCount();
        }
    }

    private void RefreshCount() => LayerCount.Text = $"{Layers?.Count(layer => layer.IsSelected) ?? 0} / {Layers?.Count ?? 0}";

    private void OnSectionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section } && Enum.TryParse<LeftPanelSection>(section, out var requested))
        {
            SectionRequested?.Invoke(this, requested);
        }
    }
}
