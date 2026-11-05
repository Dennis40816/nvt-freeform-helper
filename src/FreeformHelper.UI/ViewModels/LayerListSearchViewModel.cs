using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using LayerToggle = FreeformHelper.UI.ViewModels.FreeformHelperViewModel.LayerToggle;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the layer list search text and projects the matching layer toggles for the left panel.
/// </summary>
/// <remarks>
/// The projection holds the same <see cref="LayerToggle"/> instances as the source, so toggling a
/// filtered row changes the real layer selection. The search only hides rows; it never changes
/// <see cref="LayerToggle.IsSelected"/>.
/// </remarks>
public sealed partial class LayerListSearchViewModel : ObservableObject
{
    private readonly ObservableCollection<LayerToggle> _visibleLayers = [];
    private ObservableCollection<LayerToggle>? _source;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayerListSearchViewModel"/> class.
    /// </summary>
    public LayerListSearchViewModel()
    {
        VisibleLayers = new ReadOnlyObservableCollection<LayerToggle>(_visibleLayers);
    }

    /// <summary>
    /// Gets the layer toggles whose names match <see cref="SearchText"/>, in source order.
    /// </summary>
    public ReadOnlyObservableCollection<LayerToggle> VisibleLayers { get; }

    /// <summary>
    /// Gets a value indicating whether the search text contains a term.
    /// </summary>
    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>
    /// Gets a short "visible of total" summary while a search is active; otherwise empty.
    /// </summary>
    public string MatchSummary => HasSearchText
        ? string.Format(CultureInfo.InvariantCulture, "{0} of {1} layers", _visibleLayers.Count, _source?.Count ?? 0)
        : string.Empty;

    /// <summary>
    /// Uses <paramref name="source"/> as the layer list to filter. Pass <see langword="null"/> to detach.
    /// </summary>
    /// <param name="source">The live layer toggle collection.</param>
    public void Attach(ObservableCollection<LayerToggle>? source)
    {
        if (!ReferenceEquals(_source, source))
        {
            if (_source is not null)
            {
                _source.CollectionChanged -= OnSourceCollectionChanged;
            }

            _source = source;
            if (_source is not null)
            {
                _source.CollectionChanged += OnSourceCollectionChanged;
            }
        }

        Refresh();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    partial void OnSearchTextChanged(string value) => Refresh();

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        _visibleLayers.Clear();
        if (_source is not null)
        {
            foreach (var toggle in _source)
            {
                if (LayerNameSearch.Matches(toggle.Name, SearchText))
                {
                    _visibleLayers.Add(toggle);
                }
            }
        }

        OnPropertyChanged(nameof(MatchSummary));
    }
}
