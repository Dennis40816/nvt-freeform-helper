// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>Owns ordered layers, selection and search for one export dialog.</summary>
/// <remarks>UI thread only: the caller serializes changes to selection and query.</remarks>
public sealed class LayerSelectionModel
{
    private readonly HashSet<string> _names;
    private readonly HashSet<string> _checkedNames = new(StringComparer.Ordinal);

    public LayerSelectionModel(IEnumerable<LayerListEntry> entries, IEnumerable<string>? initiallyChecked)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = Array.AsReadOnly(entries.ToArray());
        _names = Entries.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
        if (initiallyChecked is not null)
        {
            foreach (string name in initiallyChecked)
            {
                SetChecked(name, true);
            }
        }
    }

    public IReadOnlyList<LayerListEntry> Entries { get; }

    public string Query { get; private set; } = string.Empty;

    public IReadOnlyList<LayerListEntry> VisibleEntries
    {
        get
        {
            var terms = LayerListSearch.ParseTerms(Query);
            return Array.AsReadOnly(Entries.Where(entry => LayerListSearch.Matches(entry.Name, terms)).ToArray());
        }
    }

    public int TotalCount => Entries.Count;

    public int CheckedCount => _checkedNames.Count;

    public IReadOnlyList<string> CheckedNames =>
        Array.AsReadOnly(Entries.Where(entry => IsChecked(entry.Name)).Select(entry => entry.Name).ToArray());

    public AllLayersState AllLayersState
    {
        get
        {
            var visible = VisibleEntries;
            int count = visible.Count(entry => IsChecked(entry.Name));
            return count == 0 ? AllLayersState.Unchecked
                : count == visible.Count ? AllLayersState.Checked : AllLayersState.Indeterminate;
        }
    }

    public bool IsChecked(string name) => _checkedNames.Contains(name);

    public bool SetChecked(string name, bool isChecked) =>
        _names.Contains(name) && (isChecked ? _checkedNames.Add(name) : _checkedNames.Remove(name));

    public void SetQuery(string? query) => Query = query ?? string.Empty;

    public void CheckAllVisible()
    {
        foreach (var entry in VisibleEntries)
        {
            SetChecked(entry.Name, true);
        }
    }

    public void UncheckAllVisible()
    {
        foreach (var entry in VisibleEntries)
        {
            SetChecked(entry.Name, false);
        }
    }

    public void ToggleAllVisible()
    {
        if (AllLayersState == AllLayersState.Checked)
        {
            UncheckAllVisible();
        }
        else
        {
            CheckAllVisible();
        }
    }
}
