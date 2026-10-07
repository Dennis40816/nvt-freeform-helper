using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class NotchExportSelectionViewModel : ObservableObject
{
    public ProjectEditingCommands Editing { get; } = new();

    private readonly Dictionary<int, NotchExportIcGroupViewModel> _groupByIcIndex = new();
    private readonly Dictionary<NotchExportColumnFilterField, HashSet<string>> _columnFilters = new();
    private readonly List<NotchExportRowItemViewModel> _allRows = new();
    private readonly NotchToFullCoverageAudit _toFullCoverageAudit;
    private readonly ObservableCollection<NotchExportRowItemViewModel> _rows = new();
    private readonly ObservableCollection<NotchExportIcGroupViewModel> _icGroups = new();
    private readonly ObservableCollection<NotchExportRowItemViewModel> _workspaceLinkedRows = new();
    private NotchExportSelectionFilterViewState _filterViewState = new(
        HasSearchKeyword: false,
        HasActiveColumnFilters: false,
        IsRowColumnFilterActive: false,
        IsIcColumnFilterActive: false,
        IsDiffColumnFilterActive: false,
        IsMatchColumnFilterActive: false,
        IsMappingColumnFilterActive: false,
        IsStatusColumnFilterActive: false,
        HasSearchFilterChip: false,
        SearchFilterChipText: string.Empty,
        HasModeFilterChip: true,
        ModeFilterChipText: "Mode: Transfer-only",
        HasSelectionFilterChip: false,
        SelectionFilterChipText: "Selection: Selected only",
        HasHeaderFilterChip: false,
        HeaderFilterChipText: "Header: None",
        HasNoActiveFilterChips: false,
        HasResettableViewFilters: true,
        ActiveColumnFilterSummaryText: "None",
        CurrentSearchScopeText: "Active filter: text=* | mode=Transfer-only | selected=all | header=None | shown=0.",
        VisibleRowsSummaryText: "Showing 0 rows");
    private NotchExportSelectionSummarySnapshot _summarySnapshot = new(
        AllRowCount: 0,
        VisibleCount: 0,
        SelectedCount: 0,
        V22RowCount: 0,
        TransferRowCount: 0,
        WarningRowCount: 0,
        LegacyRowCount: 0,
        CadLinkedRowCount: 0,
        CadMissingRowCount: 0,
        SummaryText: "Selected 0/0 rows · shown 0.",
        DistributionText: "v2.2 0 · warning 0 · legacy 0 · CAD linked 0 · no CAD 0",
        ExportButtonText: "Export selected",
        HasRows: false,
        HasSelection: false);
    private readonly Action<NotchTableRow>? _previewRowChanged;
    private IReadOnlyList<int> _workspaceCadIds = Array.Empty<int>();
    private IReadOnlyList<int> _workspaceRegularIndices = Array.Empty<int>();
    private bool _suppressPreviewCallback;
    private Action? _hidePanelForInspectRequested;
    private readonly RelayCommand _hidePanelForInspectCommand;
    private SimulationSafetyAuditResult? _simulationSafetyAudit;
    private readonly string _restoreShortcutHintText =
        "Need full AA view? Hide panel, inspect in workspace, then press Ctrl+Shift+E to restore.";
    private readonly string _subtitleText =
        "Review, filter, inspect, and export notch mappings.";
    private const string DefaultSearchWatermark = "Search by row / diff / mapping / CAD...";

    public NotchExportSelectionViewModel(
        NotchTable table,
        Action<NotchTableRow>? previewRowChanged = null,
        IReadOnlyList<FreeformHelperViewModel.NotchExportFileTypeOption>? exportTypeOptions = null,
        FreeformHelperViewModel.NotchExportFileTypeOption? selectedExportTypeOption = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        _previewRowChanged = previewRowChanged;
        ExportTypeOptions = exportTypeOptions is null
            ? Array.Empty<FreeformHelperViewModel.NotchExportFileTypeOption>()
            : exportTypeOptions.ToList();
        VersionOptions = BuildVersionOptions(table, ExportTypeOptions);
        _selectedVersionOption = VersionOptions[0];
        RowDisplayModeOptions = new[]
        {
            new NotchExportRowDisplayModeOption(NotchExportRowDisplayMode.TransferOnly, "Transfer-only"),
            new NotchExportRowDisplayModeOption(NotchExportRowDisplayMode.AllRows, "All"),
            new NotchExportRowDisplayModeOption(NotchExportRowDisplayMode.LinkedOnly, "Linked"),
            new NotchExportRowDisplayModeOption(NotchExportRowDisplayMode.WarningOnly, "Warning"),
            new NotchExportRowDisplayModeOption(NotchExportRowDisplayMode.NoCadOnly, "No CAD"),
            new NotchExportRowDisplayModeOption(NotchExportRowDisplayMode.LegacyOnly, "Legacy"),
        };
        _selectedRowDisplayModeOption = RowDisplayModeOptions[0];
        SortModeOptions = new[]
        {
            new NotchExportSortModeOption(NotchExportSortMode.RowId, "Row ID"),
            new NotchExportSortModeOption(NotchExportSortMode.DiffAscending, "Diff ↑"),
            new NotchExportSortModeOption(NotchExportSortMode.MatchDescending, "Match ↓"),
            new NotchExportSortModeOption(NotchExportSortMode.IcThenDiff, "IC / Diff"),
        };
        _selectedSortModeOption = SortModeOptions[0];

        if (ExportTypeOptions.Count > 0)
        {
            var preferred = selectedExportTypeOption ?? ExportTypeOptions[0];
            var resolved = ExportTypeOptions.FirstOrDefault(option => option.Value == preferred.Value);
            if (string.IsNullOrWhiteSpace(resolved.Display))
            {
                resolved = ExportTypeOptions[0];
            }

            _selectedExportTypeOption = resolved;
        }
        var pinnedVersionOption = ResolvePinnedVersionOption(SelectedExportTypeOption);
        if (!string.IsNullOrWhiteSpace(pinnedVersionOption.Display))
        {
            _selectedVersionOption = pinnedVersionOption;
        }

        _toFullCoverageAudit = table.ToFullCoverageAudit;

        var rows = table.Rows
            .Select((row, index) => new NotchExportRowItemViewModel(index, row))
            .ToList();

        Rows = new ReadOnlyObservableCollection<NotchExportRowItemViewModel>(_rows);
        WorkspaceLinkedRows = new ReadOnlyObservableCollection<NotchExportRowItemViewModel>(_workspaceLinkedRows);
        foreach (var row in rows)
        {
            _allRows.Add(row);
            row.PropertyChanged += OnRowPropertyChanged;
        }

        IcGroups = new ReadOnlyObservableCollection<NotchExportIcGroupViewModel>(_icGroups);

        SelectAllCommand = Editing.Create(SelectAll);
        SelectNoneCommand = Editing.Create(SelectNone);
        UseShownOnlyCommand = Editing.Create(UseShownOnly);
        SetViewFilterModeCommand = Editing.Create<NotchExportRowDisplayMode>(SetViewFilterMode);
        ToggleSelectedOnlyCommand = Editing.Create(() => ShowSelectedOnly = !ShowSelectedOnly);
        ApplyColumnFilterCommand = Editing.Create<string?>(ApplyColumnFilter);
        ClearSearchKeywordCommand = Editing.Create(ClearSearchKeyword);
        SelectPreviewRowCommand = Editing.Create<NotchExportRowItemViewModel?>(SelectPreviewRow);
        ToggleSelectedRowDetailCommand = Editing.Create(() => ShowSelectedRowDetailSection = !ShowSelectedRowDetailSection);
        EnableWorkspaceLinkedRowsCommand = Editing.Create(() => SetWorkspaceLinkedRowsSelected(true));
        DisableWorkspaceLinkedRowsCommand = Editing.Create(() => SetWorkspaceLinkedRowsSelected(false));
        _hidePanelForInspectCommand = Editing.Create(RequestHidePanelForInspect, () => CanHidePanelForInspect);
        HidePanelForInspectCommand = _hidePanelForInspectCommand;

        RebuildVisibleRows();
    }

    public ReadOnlyObservableCollection<NotchExportRowItemViewModel> Rows { get; }
    public ReadOnlyObservableCollection<NotchExportIcGroupViewModel> IcGroups { get; }
    public ReadOnlyObservableCollection<NotchExportRowItemViewModel> WorkspaceLinkedRows { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand SelectNoneCommand { get; }
    public IRelayCommand UseShownOnlyCommand { get; }
    public IRelayCommand<NotchExportRowDisplayMode> SetViewFilterModeCommand { get; }
    public IRelayCommand ToggleSelectedOnlyCommand { get; }
    public IRelayCommand<string?> ApplyColumnFilterCommand { get; }
    public IRelayCommand ClearSearchKeywordCommand { get; }
    public IRelayCommand<NotchExportRowItemViewModel?> SelectPreviewRowCommand { get; }
    public IRelayCommand ToggleSelectedRowDetailCommand { get; }
    public IRelayCommand EnableWorkspaceLinkedRowsCommand { get; }
    public IRelayCommand DisableWorkspaceLinkedRowsCommand { get; }
    public IRelayCommand HidePanelForInspectCommand { get; }
    public IReadOnlyList<FreeformHelperViewModel.NotchExportFileTypeOption> ExportTypeOptions { get; }
    public IReadOnlyList<NotchExportVersionOption> VersionOptions { get; }
    public IReadOnlyList<NotchExportRowDisplayModeOption> RowDisplayModeOptions { get; }
    public IReadOnlyList<NotchExportSortModeOption> SortModeOptions { get; }

    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private NotchExportRowItemViewModel? _selectedRow;
    [ObservableProperty] private bool _showSelectedRowDetailSection;
    [ObservableProperty] private FreeformHelperViewModel.NotchExportFileTypeOption _selectedExportTypeOption;
    [ObservableProperty] private NotchExportVersionOption _selectedVersionOption;
    [ObservableProperty] private NotchExportRowDisplayModeOption _selectedRowDisplayModeOption;
    [ObservableProperty] private NotchExportSortModeOption _selectedSortModeOption;
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private bool _showSelectedOnly;

    public int TotalCount => _summarySnapshot.VisibleCount;
    public int AllRowCount => _summarySnapshot.AllRowCount;
    public int VisibleCount => _summarySnapshot.VisibleCount;
    public bool HasRows => _summarySnapshot.HasRows;
    public bool HasSelection => _summarySnapshot.HasSelection;
    public bool AreAllVisibleRowsSelected
    {
        get => Rows.Count > 0 && Rows.All(static row => row.IsSelected);
        set => SetVisibleRowsSelection(value);
    }
    public bool HasExportTypeOptions => ExportTypeOptions.Count > 0;
    public bool CanSelectExportType => ExportTypeOptions.Count > 1;
    public bool HasVersionOptions => VersionOptions.Count > 0;
    public bool IsVersionSelectionPinnedByExportType => NotchExportFileTypeMetadata.TryGetPinnedVersion(SelectedExportTypeOption, out _);
    public bool CanSelectVersion => VersionOptions.Count > 1 && !IsVersionSelectionPinnedByExportType;
    public bool HasRowDisplayModeOptions => RowDisplayModeOptions.Count > 0;
    public bool CanSelectRowDisplayMode => RowDisplayModeOptions.Count > 1;
    public bool CanSelectSortMode => SortModeOptions.Count > 1;
    public bool IsSelectedOnlyActive => ShowSelectedOnly;
    public bool IsFilterTransferOnly => SelectedRowDisplayModeOption.Value == NotchExportRowDisplayMode.TransferOnly;
    public bool IsFilterAllRows => SelectedRowDisplayModeOption.Value == NotchExportRowDisplayMode.AllRows;
    public bool IsFilterLinkedOnly => SelectedRowDisplayModeOption.Value == NotchExportRowDisplayMode.LinkedOnly;
    public bool IsFilterWarningOnly => SelectedRowDisplayModeOption.Value == NotchExportRowDisplayMode.WarningOnly;
    public bool IsFilterNoCadOnly => SelectedRowDisplayModeOption.Value == NotchExportRowDisplayMode.NoCadOnly;
    public bool IsFilterLegacyOnly => SelectedRowDisplayModeOption.Value == NotchExportRowDisplayMode.LegacyOnly;
    public bool IsRowColumnFilterActive => _filterViewState.IsRowColumnFilterActive;
    public bool IsIcColumnFilterActive => _filterViewState.IsIcColumnFilterActive;
    public bool IsDiffColumnFilterActive => _filterViewState.IsDiffColumnFilterActive;
    public bool IsMatchColumnFilterActive => _filterViewState.IsMatchColumnFilterActive;
    public bool IsMappingColumnFilterActive => _filterViewState.IsMappingColumnFilterActive;
    public bool IsStatusColumnFilterActive => _filterViewState.IsStatusColumnFilterActive;
    public bool HasSearchKeyword => _filterViewState.HasSearchKeyword;
    public bool HasActiveColumnFilters => _filterViewState.HasActiveColumnFilters;
    public bool HasSearchFilterChip => _filterViewState.HasSearchFilterChip;
    public string SearchFilterChipText => _filterViewState.SearchFilterChipText;
    public bool HasModeFilterChip => _filterViewState.HasModeFilterChip;
    public string ModeFilterChipText => _filterViewState.ModeFilterChipText;
    public bool HasSelectionFilterChip => _filterViewState.HasSelectionFilterChip;
    public string SelectionFilterChipText => _filterViewState.SelectionFilterChipText;
    public bool HasHeaderFilterChip => _filterViewState.HasHeaderFilterChip;
    public string HeaderFilterChipText => _filterViewState.HeaderFilterChipText;
    public bool HasNoActiveFilterChips => _filterViewState.HasNoActiveFilterChips;
    public bool HasResettableViewFilters => _filterViewState.HasResettableViewFilters;
    public string ActiveColumnFilterSummaryText => _filterViewState.ActiveColumnFilterSummaryText;
    public string CurrentSearchScopeText => _filterViewState.CurrentSearchScopeText;
    public string SearchWatermarkText { get; } = DefaultSearchWatermark;
    public string VisibleRowsSummaryText => _filterViewState.VisibleRowsSummaryText;
    public string SelectedExportTypeText => string.IsNullOrWhiteSpace(SelectedExportTypeOption.Display)
        ? "-"
        : SelectedExportTypeOption.Display;
    public string SelectedRowDisplayModeText => string.IsNullOrWhiteSpace(SelectedRowDisplayModeOption.Display)
        ? "-"
        : SelectedRowDisplayModeOption.Display;
    public string SelectedVersionText => string.IsNullOrWhiteSpace(SelectedVersionOption.Display)
        ? "All versions"
        : SelectedVersionOption.Display;
    public bool HasSelectedRow => SelectedRow is not null;
    public bool HasNoSelectedRow => !HasSelectedRow;
    public string SummaryText => _summarySnapshot.SummaryText;
    public int V22RowCount => _summarySnapshot.V22RowCount;
    public int TransferRowCount => _summarySnapshot.TransferRowCount;
    public int WarningRowCount => _summarySnapshot.WarningRowCount;
    public int LegacyRowCount => _summarySnapshot.LegacyRowCount;
    public int CadLinkedRowCount => _summarySnapshot.CadLinkedRowCount;
    public int CadMissingRowCount => _summarySnapshot.CadMissingRowCount;
    public string DistributionText => _summarySnapshot.DistributionText;
    public string SubtitleText => _subtitleText;
    public string ExportButtonText => SimulationSafetyTextProjector.BuildExportButtonText(
        IsExportBlockedBySimulationSafety,
        _summarySnapshot.ExportButtonText);
    public bool CanConfirmExport => HasSelection;
    public string SelectedRowHeaderText => SelectedRow?.PreviewHeaderText ?? "No row selected.";
    public string SelectedRowContextText => SelectedRow?.CurrentRowContextText ?? "-";
    public string SelectedRowPositionText
    {
        get
        {
            if (SelectedRow is null || Rows.Count == 0)
            {
                return "-";
            }

            var visibleIndex = Rows.IndexOf(SelectedRow);
            if (visibleIndex < 0)
            {
                return $"-/{Rows.Count}";
            }

            return $"Row {visibleIndex + 1}/{Rows.Count}";
        }
    }
    public string SelectedRowCodePreviewText => SelectedRow?.CodePreviewText ?? "{ - }";
    public string SelectedRowOutcomeText => SelectedRow?.PrimaryOutcomeText ?? "-";
    public string SelectedRowPadRelationText => SelectedRow?.PreviewPadRelationText ?? "-";
    public string SelectedRowMappingStatusText => SelectedRow?.MappingStatusText ?? "-";
    public string SelectedRowPayloadText => SelectedRow?.PreviewPayloadText ?? "-";
    public string SelectedRowPayloadAnchorText => SelectedRow?.PayloadAnchorText ?? "-";
    public string SelectedRowPayloadCombineText => SelectedRow?.PayloadCombineText ?? "-";
    public string SelectedRowPayloadTarget1Text => SelectedRow?.PayloadTarget1Text ?? "-";
    public string SelectedRowPayloadTarget2Text => SelectedRow?.PayloadTarget2Text ?? "-";
    public string SelectedRowColumnBreakdownText => SelectedRow?.PayloadColumnsText ?? "-";
    public string SelectedRowFlagsText => SelectedRow?.PreviewFlagsText ?? "-";
    public string SelectedRowValuesText => SelectedRow?.ValuesText ?? "-";
    public string SelectedRowCommentText => SelectedRow?.CommentText ?? "-";
    public string SelectedRowAnalysisText => SelectedRow?.PreviewAnalysisText ?? "-";
    public string SelectedRowAnalysisRatioSumText => SelectedRow?.AnalysisRatioSumText ?? "-";
    public string SelectedRowAnalysisDeltaText => SelectedRow?.AnalysisDeltaText ?? "-";
    public bool HasWorkspaceLinkedRows => WorkspaceLinkedRows.Count > 0;
    public bool HasNoWorkspaceLinkedRows => !HasWorkspaceLinkedRows;
    public string WorkspaceSelectionSummaryText => BuildWorkspaceSelectionSummaryText();
    public string SelectedRowSummaryText => $"{SelectedVersionText} · {SelectedRowDisplayModeText} · {SelectedExportTypeText}";
    public bool CanHidePanelForInspect => _hidePanelForInspectRequested is not null;
    public string RestoreShortcutHintText => _restoreShortcutHintText;
    public bool HasToFullCoverageExpectations => _toFullCoverageAudit.HasExpectations;
    public bool HasToFullCoverageWarning => _toFullCoverageAudit.HasMissingCoverage;
    public bool HasToFullCoveragePass => _toFullCoverageAudit.HasExpectations && !_toFullCoverageAudit.HasMissingCoverage;
    public bool HasNoToFullCoverageExpectations => !_toFullCoverageAudit.HasExpectations;
    public bool HasToFullCoverageMissingList => _toFullCoverageAudit.HasMissingCoverage;
    public string ToFullCoverageBadgeText => BuildToFullCoverageBadgeText(_toFullCoverageAudit);
    public string ToFullCoverageSummaryText => BuildToFullCoverageSummaryText(_toFullCoverageAudit);
    public string ToFullCoverageDetailText => BuildToFullCoverageDetailText(_toFullCoverageAudit);
    public string ToFullCoverageMissingListText => BuildToFullCoverageMissingListText(_toFullCoverageAudit);
    public bool HasSimulationSafetyAudit => _simulationSafetyAudit?.HasCells == true;
    public bool HasNoSimulationSafetyAudit => !HasSimulationSafetyAudit;
    public bool IsExportBlockedBySimulationSafety => _simulationSafetyAudit?.HasViolations == true;
    public bool HasSimulationPhysicalAuditWarning =>
        _simulationSafetyAudit is { HasCells: true, HasViolations: false, HasPhysicalAuditRisks: true };
    public bool IsSimulationSafetyClean =>
        _simulationSafetyAudit is { HasCells: true, HasViolations: false, HasPhysicalAuditRisks: false };
    public string SimulationSafetyExportBadgeText => SimulationSafetyTextProjector.BuildExportBadgeText(_simulationSafetyAudit);
    public string SimulationSafetyExportSummaryText => SimulationSafetyTextProjector.BuildExportSummaryText(_simulationSafetyAudit);
    public string SimulationSafetyExportHighRiskText => SimulationSafetyTextProjector.BuildExportHighRiskText(_simulationSafetyAudit);

    public NotchTable BuildSelectedTable()
    {
        var selected = _allRows
            .Where(row => row.IsSelected && MatchesSelectedVersion(row))
            .OrderBy(row => row.RowIndex)
            .Select(row => row.Row)
            .ToList();
        return new NotchTable(selected);
    }

    public void AttachSimulationSafetyAudit(SimulationSafetyAuditResult? audit)
    {
        _simulationSafetyAudit = audit;
        NotifySimulationSafetyExportChanged();
    }

    public bool TryGetExportBlockMessage(out string title, out string message)
    {
        if (_simulationSafetyAudit is { HasViolations: true } audit)
        {
            title = SimulationSafetyTextProjector.ExportBlockTitle;
            message = SimulationSafetyTextProjector.BuildExportBlockMessage(audit);
            return true;
        }

        title = string.Empty;
        message = string.Empty;
        return false;
    }

    partial void OnSelectedRowChanged(NotchExportRowItemViewModel? value)
    {
        foreach (var row in _allRows)
        {
            row.IsPreviewSelected = ReferenceEquals(row, value);
        }

        OnPropertyChanged(nameof(HasSelectedRow));
        OnPropertyChanged(nameof(HasNoSelectedRow));
        OnPropertyChanged(nameof(SelectedRowHeaderText));
        OnPropertyChanged(nameof(SelectedRowContextText));
        OnPropertyChanged(nameof(SelectedRowPositionText));
        OnPropertyChanged(nameof(SelectedRowSummaryText));
        OnPropertyChanged(nameof(SelectedRowCodePreviewText));
        OnPropertyChanged(nameof(SelectedRowOutcomeText));
        OnPropertyChanged(nameof(SelectedRowPadRelationText));
        OnPropertyChanged(nameof(SelectedRowMappingStatusText));
        OnPropertyChanged(nameof(SelectedRowPayloadText));
        OnPropertyChanged(nameof(SelectedRowPayloadAnchorText));
        OnPropertyChanged(nameof(SelectedRowPayloadCombineText));
        OnPropertyChanged(nameof(SelectedRowPayloadTarget1Text));
        OnPropertyChanged(nameof(SelectedRowPayloadTarget2Text));
        OnPropertyChanged(nameof(SelectedRowColumnBreakdownText));
        OnPropertyChanged(nameof(SelectedRowFlagsText));
        OnPropertyChanged(nameof(SelectedRowValuesText));
        OnPropertyChanged(nameof(SelectedRowCommentText));
        OnPropertyChanged(nameof(SelectedRowAnalysisText));
        OnPropertyChanged(nameof(SelectedRowAnalysisRatioSumText));
        OnPropertyChanged(nameof(SelectedRowAnalysisDeltaText));

        if (value is not null)
        {
            NotifyPreviewRowChanged(value);
        }
    }

    private void NotifySimulationSafetyExportChanged()
    {
        OnPropertyChanged(nameof(HasSimulationSafetyAudit));
        OnPropertyChanged(nameof(HasNoSimulationSafetyAudit));
        OnPropertyChanged(nameof(IsExportBlockedBySimulationSafety));
        OnPropertyChanged(nameof(HasSimulationPhysicalAuditWarning));
        OnPropertyChanged(nameof(IsSimulationSafetyClean));
        OnPropertyChanged(nameof(SimulationSafetyExportBadgeText));
        OnPropertyChanged(nameof(SimulationSafetyExportSummaryText));
        OnPropertyChanged(nameof(SimulationSafetyExportHighRiskText));
        OnPropertyChanged(nameof(ExportButtonText));
        OnPropertyChanged(nameof(CanConfirmExport));
    }

    private static string BuildToFullCoverageBadgeText(NotchToFullCoverageAudit audit)
    {
        if (!audit.HasExpectations)
        {
            return "ToFull coverage n/a";
        }

        return audit.HasMissingCoverage
            ? $"ToFull coverage missing {audit.MissingTargetDiffCount}/{audit.ExpectedTargetDiffCount}"
            : $"ToFull coverage ok {audit.CoveredTargetDiffCount}/{audit.ExpectedTargetDiffCount}";
    }

    private static string BuildToFullCoverageSummaryText(NotchToFullCoverageAudit audit)
    {
        if (!audit.HasExpectations)
        {
            return "No ToFull-driven target diff was emitted in this table.";
        }

        return audit.HasMissingCoverage
            ? $"Detected {audit.MissingTargetDiffCount} missing ToFull target diff(s) across {audit.BucketCount} source bucket(s)."
            : $"All {audit.ExpectedTargetDiffCount} ToFull target diff(s) are covered across {audit.BucketCount} source bucket(s).";
    }

    private static string BuildToFullCoverageDetailText(NotchToFullCoverageAudit audit)
    {
        if (!audit.HasExpectations)
        {
            return "Audit skipped because no canonical source bucket carried a ToFull-only target leg.";
        }

        return $"Expected target diffs={audit.ExpectedTargetDiffCount} | covered={audit.CoveredTargetDiffCount} | missing={audit.MissingTargetDiffCount}.";
    }

    private static string BuildToFullCoverageMissingListText(NotchToFullCoverageAudit audit)
    {
        if (!audit.HasMissingCoverage)
        {
            return "No missing ToFull target diff.";
        }

        return string.Join(
            Environment.NewLine,
            audit.MissingGaps.Select(gap =>
                $"IC {gap.IcIndex + 1} / FW Diff {gap.SourceDiffIndex} -> FW Diff {gap.TargetDiffIndex} | CAD {string.Join("/", gap.CadPadIds)}"));
    }

    partial void OnSelectedExportTypeOptionChanged(FreeformHelperViewModel.NotchExportFileTypeOption value)
    {
        SyncVersionSelectionToExportType();
        OnPropertyChanged(nameof(SelectedExportTypeText));
        OnPropertyChanged(nameof(IsVersionSelectionPinnedByExportType));
        OnPropertyChanged(nameof(CanSelectVersion));
        OnPropertyChanged(nameof(SelectedRowSummaryText));
    }

    partial void OnSelectedVersionOptionChanged(NotchExportVersionOption value)
    {
        RebuildVisibleRows();
        OnPropertyChanged(nameof(SelectedVersionText));
        OnPropertyChanged(nameof(SelectedRowSummaryText));
    }

    partial void OnSelectedRowDisplayModeOptionChanged(NotchExportRowDisplayModeOption value)
    {
        RebuildVisibleRows();
        OnPropertyChanged(nameof(SelectedRowDisplayModeText));
        OnPropertyChanged(nameof(SelectedRowSummaryText));
        NotifyFilterStateChanged();
    }

    partial void OnSearchKeywordChanged(string value)
    {
        RebuildVisibleRows();
    }

    partial void OnSelectedSortModeOptionChanged(NotchExportSortModeOption value)
    {
        RebuildVisibleRows();
    }

    partial void OnShowSelectedOnlyChanged(bool value)
    {
        RebuildVisibleRows();
        OnPropertyChanged(nameof(IsSelectedOnlyActive));
    }

    public void AttachWindowActions(Action? hidePanelForInspectRequested)
    {
        _hidePanelForInspectRequested = hidePanelForInspectRequested;
        OnPropertyChanged(nameof(CanHidePanelForInspect));
        _hidePanelForInspectCommand.NotifyCanExecuteChanged();
    }

    private void RequestHidePanelForInspect()
    {
        _hidePanelForInspectRequested?.Invoke();
    }

    private bool MatchesSelectedVersion(NotchExportRowItemViewModel row)
    {
        return !SelectedVersionOption.Value.HasValue || row.Row.Version == SelectedVersionOption.Value.Value;
    }

    private static List<NotchExportVersionOption> BuildVersionOptions(
        NotchTable table,
        IReadOnlyList<FreeformHelperViewModel.NotchExportFileTypeOption> exportTypeOptions)
    {
        var countsByVersion = table.Rows
            .GroupBy(row => row.Version)
            .ToDictionary(group => group.Key, group => group.Count());
        var versions = new SortedSet<NotchAlgorithmVersion>(countsByVersion.Keys);
        foreach (var exportTypeOption in exportTypeOptions)
        {
            if (NotchExportFileTypeMetadata.TryGetPinnedVersion(exportTypeOption, out var pinnedVersion))
            {
                versions.Add(pinnedVersion);
            }
        }

        var options = new List<NotchExportVersionOption>
        {
            new NotchExportVersionOption(null, "All versions"),
        };

        foreach (var version in versions)
        {
            countsByVersion.TryGetValue(version, out var count);
            var display = $"{version.ToDisplayLabel()} ({count})";
            options.Add(new NotchExportVersionOption(version, display));
        }

        return options;
    }

    private void SyncVersionSelectionToExportType()
    {
        var pinnedOption = ResolvePinnedVersionOption(SelectedExportTypeOption);
        if (string.IsNullOrWhiteSpace(pinnedOption.Display))
        {
            return;
        }

        if (!Equals(SelectedVersionOption, pinnedOption))
        {
            SelectedVersionOption = pinnedOption;
        }
    }

    private NotchExportVersionOption ResolvePinnedVersionOption(FreeformHelperViewModel.NotchExportFileTypeOption exportTypeOption)
    {
        if (!NotchExportFileTypeMetadata.TryGetPinnedVersion(exportTypeOption, out var pinnedVersion))
        {
            return default;
        }

        return VersionOptions.FirstOrDefault(option => option.Value == pinnedVersion);
    }

    private void SetVisibleRowsSelection(bool selected)
    {
        if (Rows.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var row in Rows)
        {
            if (row.IsSelected == selected)
            {
                continue;
            }

            row.IsSelected = selected;
            changed = true;
        }

        if (!changed)
        {
            OnPropertyChanged(nameof(AreAllVisibleRowsSelected));
        }
    }
}
