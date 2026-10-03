using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Icons;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class DxfEditChangeListEntry : ObservableObject
{
    public DxfEditChangeListEntry(
        DxfEditChangeKind kind,
        int cadPadId,
        int? focusCadPadId,
        string cadName,
        string statusText,
        string currentLayerText,
        string listDetailText,
        string detailText,
        string actionGlyph,
        string actionLabel,
        string actionHint,
        bool canFocus,
        bool canApply)
    {
        Kind = kind;
        CadPadId = cadPadId;
        FocusCadPadId = focusCadPadId;
        CadName = cadName;
        StatusText = statusText;
        CurrentLayerText = currentLayerText;
        ListDetailText = listDetailText;
        DetailText = detailText;
        ActionGlyph = actionGlyph;
        ActionLabel = actionLabel;
        ActionHint = actionHint;
        CanFocus = canFocus;
        CanApply = canApply;
    }

    public DxfEditChangeKind Kind { get; }
    public int CadPadId { get; }
    public int? FocusCadPadId { get; }
    public string CadName { get; }
    public string StatusText { get; }
    public string CurrentLayerText { get; }
    public string ListDetailText { get; }
    public string DetailText { get; }
    public string ActionGlyph { get; }
    public string ActionLabel { get; }
    public string ActionHint { get; }
    public bool CanFocus { get; }
    public bool CanApply { get; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isInspectorSelected;
    public string RowTitleText => string.IsNullOrWhiteSpace(CadName) ? $"CAD {CadPadId}" : $"CAD {CadPadId} · {CadName}";
    public string RowStatusText => $"{StatusText} · {CurrentLayerText}";
    public string KindDisplayText => DxfEditChangeListViewModel.GetKindDisplayText(Kind);
}

public sealed partial class DxfEditChangeListViewModel : ObservableObject
{
    private readonly ObservableCollection<DxfEditChangeListEntry> _rows = new();
    private readonly Func<List<DxfEditChangeListEntry>> _entryLoader;
    private readonly Action<DxfEditChangeListEntry> _focusEntry;
    private readonly Action<DxfEditChangeListEntry> _applyEntry;
    private readonly string _restoreSelectedDuplicateGlyph = IconGlyphs.Undo;
    private readonly string _rehideSelectedDuplicateGlyph = IconGlyphs.VisibilityOff;
    private IReadOnlyList<DxfEditChangeListEntry> _allEntries = Array.Empty<DxfEditChangeListEntry>();
    private readonly string _titleText = "DXF edit details";
    private readonly string _subtitleText = "Review modified CAD pads and apply scoped actions.";

    public DxfEditChangeListViewModel(
        DxfEditChangeKind initialKind,
        Func<List<DxfEditChangeListEntry>> entryLoader,
        Action<DxfEditChangeListEntry> focusEntry,
        Action<DxfEditChangeListEntry> applyEntry)
    {
        _entryLoader = entryLoader;
        _focusEntry = focusEntry;
        _applyEntry = applyEntry;
        Rows = new ReadOnlyObservableCollection<DxfEditChangeListEntry>(_rows);
        SetHiddenKindCommand = new RelayCommand(() => SelectedKind = DxfEditChangeKind.Hidden);
        SetDuplicateKindCommand = new RelayCommand(() => SelectedKind = DxfEditChangeKind.Duplicate);
        SetCombinedKindCommand = new RelayCommand(() => SelectedKind = DxfEditChangeKind.Combined);
        SetMovedKindCommand = new RelayCommand(() => SelectedKind = DxfEditChangeKind.Moved);
        SetRotatedKindCommand = new RelayCommand(() => SelectedKind = DxfEditChangeKind.Rotated);
        RefreshCommand = new RelayCommand(RefreshEntries);
        ApplySelectedEntriesCommand = new RelayCommand(ApplySelectedEntries, () => CanApplySelectedEntries);
        RestoreSelectedDuplicateEntriesCommand = new RelayCommand(RestoreSelectedDuplicateEntries, () => CanRestoreSelectedDuplicateEntries);
        RehideSelectedDuplicateEntriesCommand = new RelayCommand(RehideSelectedDuplicateEntries, () => CanRehideSelectedDuplicateEntries);
        FocusSelectedEntryCommand = new RelayCommand(FocusSelectedEntry, () => SelectedEntry?.CanFocus == true);
        ApplySelectedEntryCommand = new RelayCommand(ApplySelectedEntry, () => SelectedEntry?.CanApply == true);
        SelectEntryCommand = new RelayCommand<DxfEditChangeListEntry?>(SelectEntry);
        FocusEntryCommand = new RelayCommand<DxfEditChangeListEntry?>(FocusEntry);
        ApplyEntryCommand = new RelayCommand<DxfEditChangeListEntry?>(ApplyEntry);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        SelectedKind = initialKind;
        RefreshEntries();
    }

    public event EventHandler? CloseRequested;

    public ReadOnlyObservableCollection<DxfEditChangeListEntry> Rows { get; }
    public IRelayCommand SetHiddenKindCommand { get; }
    public IRelayCommand SetDuplicateKindCommand { get; }
    public IRelayCommand SetCombinedKindCommand { get; }
    public IRelayCommand SetMovedKindCommand { get; }
    public IRelayCommand SetRotatedKindCommand { get; }
    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand ApplySelectedEntriesCommand { get; }
    public IRelayCommand RestoreSelectedDuplicateEntriesCommand { get; }
    public IRelayCommand RehideSelectedDuplicateEntriesCommand { get; }
    public IRelayCommand FocusSelectedEntryCommand { get; }
    public IRelayCommand ApplySelectedEntryCommand { get; }
    public IRelayCommand<DxfEditChangeListEntry?> SelectEntryCommand { get; }
    public IRelayCommand<DxfEditChangeListEntry?> FocusEntryCommand { get; }
    public IRelayCommand<DxfEditChangeListEntry?> ApplyEntryCommand { get; }
    public IRelayCommand CloseCommand { get; }

    [ObservableProperty] private DxfEditChangeKind _selectedKind;
    [ObservableProperty] private DxfEditChangeListEntry? _selectedEntry;
    [ObservableProperty] private int _hiddenCount;
    [ObservableProperty] private int _duplicateCount;
    [ObservableProperty] private int _combinedCount;
    [ObservableProperty] private int _movedCount;
    [ObservableProperty] private int _rotatedCount;
    [ObservableProperty] private int _selectedVisibleCount;

    public string TitleText => _titleText;
    public string SubtitleText => _subtitleText;
    public string SelectedKindDisplayText => GetKindDisplayText(SelectedKind);
    public bool IsHiddenKindSelected => SelectedKind == DxfEditChangeKind.Hidden;
    public bool IsDuplicateKindSelected => SelectedKind == DxfEditChangeKind.Duplicate;
    public bool IsCombinedKindSelected => SelectedKind == DxfEditChangeKind.Combined;
    public bool IsMovedKindSelected => SelectedKind == DxfEditChangeKind.Moved;
    public bool IsRotatedKindSelected => SelectedKind == DxfEditChangeKind.Rotated;
    public bool HasRows => Rows.Count > 0;
    public bool HasNoRows => !HasRows;
    public bool AreAllVisibleRowsSelected
    {
        get => Rows.Count > 0 && Rows.All(static row => row.IsSelected);
        set => SetVisibleRowsSelection(value);
    }
    public bool HasSelectedVisibleRows => SelectedVisibleCount > 0;
    public bool IsDuplicateBulkMode => SelectedKind == DxfEditChangeKind.Duplicate;
    public bool IsSingleActionBulkMode => !IsDuplicateBulkMode;
    public bool CanApplySelectedEntries => SelectedVisibleCount > 0 && IsSingleActionBulkMode;
    public bool CanRestoreSelectedDuplicateEntries => GetSelectedVisibleRows().Any(static row => row.Kind == DxfEditChangeKind.Duplicate && row.ActionLabel == "Restore");
    public bool CanRehideSelectedDuplicateEntries => GetSelectedVisibleRows().Any(static row => row.Kind == DxfEditChangeKind.Duplicate && row.ActionLabel == "Hide again");
    public string VisibleRowsSummaryText => $"Showing {Rows.Count} rows";
    public string SelectedVisibleRowsSummaryText => $"Selected {SelectedVisibleCount}/{Rows.Count} shown";
    public string SummaryText => $"Hidden {HiddenCount} · Duplicate {DuplicateCount} · Combined {CombinedCount} · Moved {MovedCount} · Rotated {RotatedCount}";
    public string SelectedRowHeaderText => SelectedEntry?.RowTitleText ?? "No DXF edit selected.";
    public string SelectedRowStatusText => SelectedEntry?.RowStatusText ?? "-";
    public string SelectedRowDetailText => SelectedEntry?.DetailText ?? "-";
    public string SelectedRowActionText => SelectedEntry?.ActionHint ?? "-";
    public string SelectedRowActionLabelText => SelectedEntry?.ActionLabel ?? "-";
    public bool CanFocusSelectedEntry => SelectedEntry?.CanFocus == true;
    public bool CanApplySelectedEntry => SelectedEntry?.CanApply == true;
    public string BulkActionGlyph => SelectedKind switch
    {
        DxfEditChangeKind.Hidden => IconGlyphs.Undo,
        DxfEditChangeKind.Combined => IconGlyphs.CallSplit,
        DxfEditChangeKind.Moved => IconGlyphs.DriveFileMove,
        DxfEditChangeKind.Rotated => IconGlyphs.Undo,
        _ => IconGlyphs.Edit,
    };
    public string RestoreSelectedDuplicateGlyph => _restoreSelectedDuplicateGlyph;
    public string RehideSelectedDuplicateGlyph => _rehideSelectedDuplicateGlyph;
    public string BulkActionText => SelectedKind switch
    {
        DxfEditChangeKind.Hidden => "Restore selected",
        DxfEditChangeKind.Combined => "Clear selected groups",
        DxfEditChangeKind.Moved => "Restore selected layers",
        DxfEditChangeKind.Rotated => "Restore selected geometry",
        _ => "Apply selected",
    };
    internal static string GetKindDisplayText(DxfEditChangeKind kind)
    {
        return kind switch
        {
            DxfEditChangeKind.Hidden => "Hidden",
            DxfEditChangeKind.Duplicate => "Duplicate",
            DxfEditChangeKind.Combined => "Combined",
            DxfEditChangeKind.Moved => "Moved",
            DxfEditChangeKind.Rotated => "Rotated",
            _ => "Changed",
        };
    }

    private void RefreshEntries()
    {
        foreach (var entry in _allEntries.OfType<INotifyPropertyChanged>())
        {
            entry.PropertyChanged -= OnEntryPropertyChanged;
        }

        var selectedEntryKeys = _allEntries
            .Where(static entry => entry.IsSelected)
            .Select(static entry => (entry.Kind, entry.CadPadId))
            .ToHashSet();
        _allEntries = _entryLoader()
            .OrderBy(static entry => entry.CadPadId)
            .ThenBy(static entry => entry.Kind)
            .ToList();
        foreach (var entry in _allEntries)
        {
            entry.IsSelected = selectedEntryKeys.Contains((entry.Kind, entry.CadPadId));
            entry.PropertyChanged += OnEntryPropertyChanged;
        }

        HiddenCount = _allEntries.Count(entry => entry.Kind == DxfEditChangeKind.Hidden);
        DuplicateCount = _allEntries.Count(entry => entry.Kind == DxfEditChangeKind.Duplicate);
        CombinedCount = _allEntries.Count(entry => entry.Kind == DxfEditChangeKind.Combined);
        MovedCount = _allEntries.Count(entry => entry.Kind == DxfEditChangeKind.Moved);
        RotatedCount = _allEntries.Count(entry => entry.Kind == DxfEditChangeKind.Rotated);
        RebuildVisibleRows();
    }

    private void RebuildVisibleRows()
    {
        var previousCadId = SelectedEntry?.CadPadId;
        var visibleEntries = _allEntries
            .Where(entry => entry.Kind == SelectedKind)
            .ToList();

        _rows.Clear();
        foreach (var entry in visibleEntries)
        {
            _rows.Add(entry);
        }

        SelectedEntry = visibleEntries.FirstOrDefault(entry => entry.CadPadId == previousCadId)
            ?? visibleEntries.FirstOrDefault();

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(AreAllVisibleRowsSelected));
        OnPropertyChanged(nameof(VisibleRowsSummaryText));
        OnPropertyChanged(nameof(SelectedVisibleRowsSummaryText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsHiddenKindSelected));
        OnPropertyChanged(nameof(IsDuplicateKindSelected));
        OnPropertyChanged(nameof(IsCombinedKindSelected));
        OnPropertyChanged(nameof(IsMovedKindSelected));
        OnPropertyChanged(nameof(IsRotatedKindSelected));
        OnPropertyChanged(nameof(SelectedKindDisplayText));
        OnPropertyChanged(nameof(IsDuplicateBulkMode));
        OnPropertyChanged(nameof(IsSingleActionBulkMode));
        UpdateSelectionSummary();
    }

    private void SetVisibleRowsSelection(bool selected)
    {
        foreach (var entry in Rows)
        {
            entry.IsSelected = selected;
        }
    }

    private void ApplySelectedEntries()
    {
        var selectedRows = GetSelectedVisibleRows()
            .Where(static row => row.CanApply)
            .ToList();
        if (selectedRows.Count == 0)
        {
            return;
        }

        foreach (var entry in selectedRows)
        {
            _applyEntry(entry);
        }

        RefreshEntries();
    }

    private void RestoreSelectedDuplicateEntries()
    {
        ApplySelectedEntries(static row => row.Kind == DxfEditChangeKind.Duplicate && row.ActionLabel == "Restore");
    }

    private void RehideSelectedDuplicateEntries()
    {
        ApplySelectedEntries(static row => row.Kind == DxfEditChangeKind.Duplicate && row.ActionLabel == "Hide again");
    }

    private void ApplySelectedEntries(Func<DxfEditChangeListEntry, bool> predicate)
    {
        var selectedRows = GetSelectedVisibleRows()
            .Where(predicate)
            .Where(static row => row.CanApply)
            .ToList();
        if (selectedRows.Count == 0)
        {
            return;
        }

        foreach (var entry in selectedRows)
        {
            _applyEntry(entry);
        }

        RefreshEntries();
    }

    private List<DxfEditChangeListEntry> GetSelectedVisibleRows()
    {
        return Rows
            .Where(static row => row.IsSelected)
            .ToList();
    }

    private void UpdateSelectionSummary()
    {
        SelectedVisibleCount = Rows.Count(static row => row.IsSelected);
        OnPropertyChanged(nameof(AreAllVisibleRowsSelected));
        OnPropertyChanged(nameof(HasSelectedVisibleRows));
        OnPropertyChanged(nameof(SelectedVisibleRowsSummaryText));
        UpdateBulkActionState();
    }

    private void UpdateBulkActionState()
    {
        OnPropertyChanged(nameof(CanApplySelectedEntries));
        OnPropertyChanged(nameof(CanRestoreSelectedDuplicateEntries));
        OnPropertyChanged(nameof(CanRehideSelectedDuplicateEntries));
        ApplySelectedEntriesCommand.NotifyCanExecuteChanged();
        RestoreSelectedDuplicateEntriesCommand.NotifyCanExecuteChanged();
        RehideSelectedDuplicateEntriesCommand.NotifyCanExecuteChanged();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DxfEditChangeListEntry.IsSelected))
        {
            UpdateSelectionSummary();
        }
    }

    private void FocusSelectedEntry()
    {
        if (SelectedEntry is null || !SelectedEntry.CanFocus)
        {
            return;
        }

        _focusEntry(SelectedEntry);
    }

    private void ApplySelectedEntry()
    {
        if (SelectedEntry is null || !SelectedEntry.CanApply)
        {
            return;
        }

        _applyEntry(SelectedEntry);
        RefreshEntries();
    }

    private void FocusEntry(DxfEditChangeListEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        SelectedEntry = entry;
        FocusSelectedEntry();
    }

    private void SelectEntry(DxfEditChangeListEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        SelectedEntry = entry;
    }

    private void ApplyEntry(DxfEditChangeListEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        SelectedEntry = entry;
        ApplySelectedEntry();
    }

    partial void OnSelectedKindChanged(DxfEditChangeKind value)
    {
        RebuildVisibleRows();
    }

    partial void OnSelectedEntryChanged(DxfEditChangeListEntry? value)
    {
        foreach (var entry in _allEntries)
        {
            entry.IsInspectorSelected = ReferenceEquals(entry, value);
        }

        OnPropertyChanged(nameof(SelectedRowHeaderText));
        OnPropertyChanged(nameof(SelectedRowStatusText));
        OnPropertyChanged(nameof(SelectedRowDetailText));
        OnPropertyChanged(nameof(SelectedRowActionText));
        OnPropertyChanged(nameof(SelectedRowActionLabelText));
        OnPropertyChanged(nameof(CanFocusSelectedEntry));
        OnPropertyChanged(nameof(CanApplySelectedEntry));
        FocusSelectedEntryCommand.NotifyCanExecuteChanged();
        ApplySelectedEntryCommand.NotifyCanExecuteChanged();
    }
}
