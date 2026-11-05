// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Projects one selection-model entry. Changes and notifications are UI thread only.</summary>
public sealed class LayerImageLayerRow : ObservableObject
{
    private readonly LayerSelectionModel _selection;
    private readonly Action _selectionChanged;
    private IReadOnlyList<LayerTextRange> _highlightRanges = Array.Empty<LayerTextRange>();

    internal LayerImageLayerRow(LayerListEntry entry, LayerSelectionModel selection, Action selectionChanged)
    {
        Name = entry.Name;
        PadCount = entry.PadCount;
        _selection = selection;
        _selectionChanged = selectionChanged;
    }

    public string Name { get; }

    public int PadCount { get; }

    public bool IsChecked
    {
        get => _selection.IsChecked(Name);
        set
        {
            if (_selection.SetChecked(Name, value))
            {
                _selectionChanged();
            }
        }
    }

    public IReadOnlyList<LayerTextRange> HighlightRanges => _highlightRanges;

    internal void RefreshSelection() => OnPropertyChanged(nameof(IsChecked));

    internal void UpdateHighlights(IReadOnlyList<string> terms)
    {
        _highlightRanges = LayerListSearch.FindRanges(Name, terms);
        OnPropertyChanged(nameof(HighlightRanges));
    }
}
