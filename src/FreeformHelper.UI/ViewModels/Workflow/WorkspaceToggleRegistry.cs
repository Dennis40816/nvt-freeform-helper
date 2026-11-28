// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns header item identity and lookup on the UI thread, independently of the replaceable collection.</summary>
internal sealed class WorkspaceToggleRegistry(Action<string> propertyChanging, Action<string> propertyChanged)
{
    private readonly Dictionary<string, WorkspaceHeaderItem> _itemsById = new();
    private ObservableCollection<WorkspaceHeaderItem> _items = new();

    public ObservableCollection<WorkspaceHeaderItem> WorkspaceHeaderItems
    {
        get => _items;
        set
        {
            if (EqualityComparer<ObservableCollection<WorkspaceHeaderItem>>.Default.Equals(_items, value)) return;
            propertyChanging(nameof(WorkspaceHeaderItems));
            _items = value;
            propertyChanged(nameof(WorkspaceHeaderItems));
        }
    }

    internal void InitializeWorkspaceHeaderItems(CanvasAppearanceState appearance)
    {
        _itemsById.Clear();
        WorkspaceHeaderItems = new ObservableCollection<WorkspaceHeaderItem>
        {
            CreateWorkspaceToggle("highlightUnmatched", "Highlight unmatched regular",
                appearance.HighlightUnmatched, value => appearance.HighlightUnmatched = value),
            CreateWorkspaceToggle("highlightFreeform", "Highlight freeform regular",
                appearance.HighlightFreeform, value => appearance.HighlightFreeform = value),
        };
    }

    private WorkspaceHeaderItem CreateWorkspaceToggle(string id, string label, bool value, Action<bool> setter)
    {
        var item = new WorkspaceHeaderItem(id, label, value, setter);
        _itemsById[id] = item;
        return item;
    }

    internal void SyncWorkspaceToggle(string id, bool value)
    {
        if (_itemsById.TryGetValue(id, out var item))
        {
            item.SetCheckedFromSource(value);
        }
    }
}
