// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Data.Converters;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Converters;

/// <summary>Projects a layer name and a search query with the shared rules of <see cref="LayerListSearch"/>.</summary>
public static class LayerSearchConverters
{
    /// <summary>Values: layer name, query. True when the layer stays in the filtered list.</summary>
    public static IMultiValueConverter IsMatch { get; } = new FuncMultiValueConverter<object?, bool>(
        values =>
        {
            var (name, terms) = Read(values);
            return LayerListSearch.Matches(name, terms);
        });

    /// <summary>Values: layer name, query. The parts of the name to highlight.</summary>
    public static IMultiValueConverter Ranges { get; } = new FuncMultiValueConverter<object?, IReadOnlyList<LayerTextRange>>(
        values =>
        {
            var (name, terms) = Read(values);
            return LayerListSearch.FindRanges(name, terms);
        });

    /// <summary>Values: query, layer toggles, layer count. True when there are layers and none matches.</summary>
    public static IMultiValueConverter HasNoMatch { get; } = new FuncMultiValueConverter<object?, bool>(
        values =>
        {
            var items = values.ToArray();
            if (items.Length < 2 || items[1] is not IEnumerable<LayerToggle> toggles)
            {
                return false;
            }

            var terms = LayerListSearch.ParseTerms(items[0] as string);
            var layers = toggles.ToArray();
            return layers.Length > 0 && !layers.Any(layer => LayerListSearch.Matches(layer.Name, terms));
        });

    private static (string Name, IReadOnlyList<string> Terms) Read(IEnumerable<object?> values)
    {
        var items = values.ToArray();
        var name = items.Length > 0 ? items[0] as string : null;
        var query = items.Length > 1 ? items[1] as string : null;
        return (name ?? string.Empty, LayerListSearch.ParseTerms(query));
    }
}
