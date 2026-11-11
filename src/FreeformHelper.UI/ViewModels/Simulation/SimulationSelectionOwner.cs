// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Owns selection indexes and ordered inspector/safety publication. UI thread only.</summary>
internal sealed class SimulationSelectionOwner
{
    private const double SignificantDeltaEpsilon = 1e-9;
    private readonly Dictionary<int, RegularPad> _regularPadById;
    private readonly Dictionary<int, int> _cadOutputFwDiffByRegularPadId;
    private readonly Dictionary<(int IcIndex, int DiffIndex), int[]> _regularPadIdsByIcDiff;
    private readonly IReadOnlySet<int> _activeRegularPadIds;
    private readonly Func<NotchApplySimulationResult?> _readResult;
    private readonly Func<IReadOnlyDictionary<int, NotchApplySimulationDiffCell>> _readVisibleCells;
    private readonly Func<NotchApplySimulationAreaOption> _readArea;
    private readonly Action<NotchApplySimulationAreaOption> _setArea;
    private readonly Func<SimulationInputSourceMode> _readSourceMode;
    private readonly Func<bool> _hasCsvData;
    private readonly Func<bool> _hasSelectedOverride;
    private readonly Func<IReadOnlyDictionary<int, double>> _readManualOverrides;
    private readonly Func<double> _readGlobalValue;
    private readonly Action _refreshCommands;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _propertyChanging;
    private readonly Action<string> _propertyChanged;
    private readonly Func<IReadOnlyList<NotchApplySimulationDiffCell>, IReadOnlyList<NotchApplySimulationAction>, int?, IEnumerable<int>> _detectViolations;
    private readonly Func<SimulationSafetyHighRiskDiff, double, SimulationSafetyRiskDiffViewModel> _projectRisk;
    private readonly Func<SimulationSafetyAuditResult, string> _buildSafetyStatusText;
    private readonly Func<double, string> _buildEmsCapText;
    private readonly Func<SimulationSafetyAuditResult, string> _buildSafetySummaryText;
    private readonly Func<SimulationSafetyAuditResult, string> _buildPhysicalAuditSummaryText;
    private readonly Func<SimulationSafetyAuditResult, string> _buildHighRiskDiffListText;
    private readonly Func<SimulationSafetyAuditResult, string> _buildViolationListText;
    private readonly Func<double, string> _formatSafetyValue;
    private readonly Func<double, string> _formatSignedSafetyValue;
    private readonly Func<int, string> _formatCountBadge;
    private readonly Func<NotchApplySimulationDiffCell, double, string> _formatCellEmsStatus;
    private readonly ObservableCollection<NotchApplySimulationImpactItemViewModel> _selectedImpactItems = new();
    private readonly ObservableCollection<SimulationSafetyRiskDiffViewModel> _simulationHighRiskDiffs = new();
    private Dictionary<int, NotchApplySimulationDiffCell> _snapshotCellsByRegularPadId = new();
    private Dictionary<int, IReadOnlyList<NotchApplySimulationImpactItemViewModel>> _impactItemsByRegularPadId = new();
    private int[] _diffViolationRegularPadIds = Array.Empty<int>();
    private SimulationSafetyAuditResult _simulationSafetyAudit = SimulationSafetyAuditResult.Empty(
        SimulationSafetyAuditService.DefaultEmsAfterCap);
    private int _selectedRegularPadId = -1;
    private double _selectedManualValue;
    private bool _isSimulationSafetyExpanded;

    internal SimulationSelectionOwner(
        Dictionary<int, RegularPad> regularPads, Dictionary<int, int> cadOutputDiffs,
        Dictionary<(int IcIndex, int DiffIndex), int[]> padsByIcDiff, IReadOnlySet<int> activeRegularPadIds,
        IReadOnlyList<NotchApplySimulationAreaOption> areaOptions,
        Func<NotchApplySimulationResult?> readResult,
        Func<IReadOnlyDictionary<int, NotchApplySimulationDiffCell>> readVisibleCells,
        Func<NotchApplySimulationAreaOption> readArea, Action<NotchApplySimulationAreaOption> setArea,
        Func<SimulationInputSourceMode> readSourceMode, Func<bool> hasCsvData, Func<bool> hasSelectedOverride,
        Func<IReadOnlyDictionary<int, double>> readManualOverrides, Func<double> readGlobalValue,
        double selectedManualValue, Action refreshCommands, Action<string> setStatus,
        Func<IReadOnlyList<NotchApplySimulationDiffCell>, IReadOnlyList<NotchApplySimulationAction>, int?, IEnumerable<int>> detectViolations,
        Func<SimulationSafetyHighRiskDiff, double, SimulationSafetyRiskDiffViewModel> projectRisk,
        Func<SimulationSafetyAuditResult, string> buildSafetyStatusText, Func<double, string> buildEmsCapText,
        Func<SimulationSafetyAuditResult, string> buildSafetySummaryText,
        Func<SimulationSafetyAuditResult, string> buildPhysicalAuditSummaryText,
        Func<SimulationSafetyAuditResult, string> buildHighRiskDiffListText,
        Func<SimulationSafetyAuditResult, string> buildViolationListText,
        Func<double, string> formatSafetyValue, Func<double, string> formatSignedSafetyValue,
        Func<int, string> formatCountBadge, Func<NotchApplySimulationDiffCell, double, string> formatCellEmsStatus,
        Action<string> propertyChanging, Action<string> propertyChanged)
    {
        _regularPadById = regularPads;
        _cadOutputFwDiffByRegularPadId = cadOutputDiffs;
        _regularPadIdsByIcDiff = padsByIcDiff;
        _activeRegularPadIds = activeRegularPadIds;
        AreaOptions = areaOptions;
        _readResult = readResult;
        _readVisibleCells = readVisibleCells;
        _readArea = readArea;
        _setArea = setArea;
        _readSourceMode = readSourceMode;
        _hasCsvData = hasCsvData;
        _hasSelectedOverride = hasSelectedOverride;
        _readManualOverrides = readManualOverrides;
        _readGlobalValue = readGlobalValue;
        _selectedManualValue = selectedManualValue;
        _refreshCommands = refreshCommands;
        _setStatus = setStatus;
        _detectViolations = detectViolations;
        _projectRisk = projectRisk;
        _buildSafetyStatusText = buildSafetyStatusText;
        _buildEmsCapText = buildEmsCapText;
        _buildSafetySummaryText = buildSafetySummaryText;
        _buildPhysicalAuditSummaryText = buildPhysicalAuditSummaryText;
        _buildHighRiskDiffListText = buildHighRiskDiffListText;
        _buildViolationListText = buildViolationListText;
        _formatSafetyValue = formatSafetyValue;
        _formatSignedSafetyValue = formatSignedSafetyValue;
        _formatCountBadge = formatCountBadge;
        _formatCellEmsStatus = formatCellEmsStatus;
        _propertyChanging = propertyChanging;
        _propertyChanged = propertyChanged;
        SelectedImpactItems = new ReadOnlyObservableCollection<NotchApplySimulationImpactItemViewModel>(_selectedImpactItems);
        SimulationHighRiskDiffs = new ReadOnlyObservableCollection<SimulationSafetyRiskDiffViewModel>(_simulationHighRiskDiffs);
        SelectSimulationRiskDiffCommand = new RelayCommand<SimulationSafetyRiskDiffViewModel?>(SelectSimulationRiskDiff);
    }

    public ReadOnlyObservableCollection<NotchApplySimulationImpactItemViewModel> SelectedImpactItems { get; }
    public ReadOnlyObservableCollection<SimulationSafetyRiskDiffViewModel> SimulationHighRiskDiffs { get; }
    public IRelayCommand<SimulationSafetyRiskDiffViewModel?> SelectSimulationRiskDiffCommand { get; }
    private IReadOnlyList<NotchApplySimulationAreaOption> AreaOptions { get; }
    private NotchApplySimulationResult? Result => _readResult();
    private IReadOnlyDictionary<int, NotchApplySimulationDiffCell> VisibleCells => _readVisibleCells();
    private NotchApplySimulationAreaOption SelectedAreaOption
    {
        get => _readArea();
        set => _setArea(value);
    }
    private bool IsManualSource => _readSourceMode() == SimulationInputSourceMode.Manual;
    private bool HasCsvData => _hasCsvData();
    private bool HasSelectedOverride => _hasSelectedOverride();
    private double GlobalValue => _readGlobalValue();

    public int SelectedRegularPadId
    {
        get => _selectedRegularPadId;
        set => SetValue(ref _selectedRegularPadId, value, nameof(SelectedRegularPadId), OnSelectedRegularPadIdChanged);
    }

    public double SelectedManualValue
    {
        get => _selectedManualValue;
        set => SetValue(ref _selectedManualValue, value, nameof(SelectedManualValue));
    }

    public bool IsSimulationSafetyExpanded
    {
        get => _isSimulationSafetyExpanded;
        set => SetValue(ref _isSimulationSafetyExpanded, value, nameof(IsSimulationSafetyExpanded));
    }

    public void IndexResult(NotchApplySimulationResult result)
    {
        _snapshotCellsByRegularPadId = result.Cells.ToDictionary(
            static cell => cell.RegularPadId,
            static cell => cell);
        RebuildImpactItemIndex(result.Actions);
    }

    public bool HasSelectedRegular => SelectedRegularPadId >= 0 && _regularPadById.ContainsKey(SelectedRegularPadId);
    public bool HasSelectedImpactItems => SelectedImpactItems.Count > 0;
    public string SelectedImpactSummaryText => !HasSelectedRegular
        ? "Select a regular pad to inspect notch impact."
        : HasSelectedImpactItems
            ? $"{SelectedImpactItems.Count} notch impact(s) affect this regular."
            : "No notch rows affect this regular under the current simulation version.";
    public string SelectedRegularHeaderText => TryGetSelectedRegularPad(out var pad)
        ? $"REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · FW Diff Idx {GetFwDiffIndex(pad.RegularPadId)} · CAD Output FW Diff Idx {GetCadOutputFwDiffIndex(pad.RegularPadId)}"
        : "No regular selected.";
    public string SelectedRegularLocationText => TryGetSelectedRegularPad(out var locationPad)
        ? $"R{locationPad.Row}, C{locationPad.Col}"
        : "-";
    public string SelectedBeforeText => TryGetSelectedCell(out var beforeCell)
        ? SimulationWorkspaceViewModel.FormatWholeNumberDisplay(beforeCell.BeforeValue)
        : "-";
    public string SelectedAfterText => TryGetSelectedCell(out var afterCell)
        ? SimulationWorkspaceViewModel.FormatWholeNumberDisplay(afterCell.AfterValue)
        : "-";
    public string SelectedDeltaText => TryGetSelectedCell(out var deltaCell)
        ? deltaCell.DeltaValue.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture)
        : "-";

    public string SelectedPercentChangeText
    {
        get
        {
            if (!TryGetSelectedCell(out var cell) || Math.Abs(cell.BeforeValue) <= SignificantDeltaEpsilon)
            {
                return "-";
            }

            var percent = ((cell.AfterValue - cell.BeforeValue) / cell.BeforeValue) * 100d;
            return percent.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture) + "%";
        }
    }

    public string SelectedSourceText => _readSourceMode() switch
    {
        SimulationInputSourceMode.Manual => HasSelectedOverride ? "Manual override" : "Global baseline",
        SimulationInputSourceMode.Csv => HasCsvData ? "CSV frame projection" : "CSV not loaded",
        SimulationInputSourceMode.Copper => "CAD overlap -> CAD Output FW Diff",
        _ => "-",
    };
    public bool ShowSelectedOverrideEditor => HasSelectedRegular && IsManualSource;

    private int GetFwDiffIndex(int regularPadId)
    {
        return _regularPadById.TryGetValue(regularPadId, out var channelPad)
            ? channelPad.DiffIndex
            : -1;
    }

    private int GetCadOutputFwDiffIndex(int regularPadId)
    {
        return _cadOutputFwDiffByRegularPadId.TryGetValue(regularPadId, out var cadOutputFwDiffIndex)
            ? cadOutputFwDiffIndex
            : GetFwDiffIndex(regularPadId);
    }

    public string SelectedImpactCountText => $"{SelectedImpactItems.Count.ToString(CultureInfo.InvariantCulture)} impact(s)";
    public string? BuildHoverTipText(int regularPadId)
    {
        if (!VisibleCells.TryGetValue(regularPadId, out var cell) || !_regularPadById.TryGetValue(regularPadId, out var pad))
        {
            return null;
        }

        return $"REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · FW Diff Idx {GetFwDiffIndex(pad.RegularPadId)} · CAD Output FW Diff Idx {GetCadOutputFwDiffIndex(pad.RegularPadId)}\n" +
               $"Before {SimulationWorkspaceViewModel.FormatWholeNumberDisplay(cell.BeforeValue)}\n" +
               $"After {SimulationWorkspaceViewModel.FormatWholeNumberDisplay(cell.AfterValue)}\n" +
               $"Delta {cell.DeltaValue.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture)}\n" +
               FormatCellEmsStatus(cell);
    }

    internal bool TryGetRegularQuerySnapshot(int regularPadId, out SimulationRegularQuerySnapshot snapshot)
    {
        if (!_regularPadById.TryGetValue(regularPadId, out var pad) || Result is null)
        {
            snapshot = default;
            return false;
        }

        if (!_snapshotCellsByRegularPadId.TryGetValue(regularPadId, out var cell))
        {
            snapshot = default;
            return false;
        }

        var impacts = GetImpactItemsForPad(pad);
        snapshot = new SimulationRegularQuerySnapshot(
            pad.RegularPadId,
            pad.Row,
            pad.Col,
            pad.IcIndex,
            GetFwDiffIndex(pad.RegularPadId),
            GetCadOutputFwDiffIndex(pad.RegularPadId),
            cell.BeforeValue,
            cell.AfterValue,
            cell.DeltaValue,
            SimulationSafetyAuditService.IsEmsAfterCapViolation(cell.AfterValue, SimulationEmsAfterCap),
            _activeRegularPadIds.Contains(regularPadId),
            VisibleCells.ContainsKey(regularPadId),
            SelectedSourceText,
            impacts);
        return true;
    }

    public void SelectRegularPad(int regularPadId)
    {
        if (regularPadId < 0)
        {
            SelectedRegularPadId = -1;
            return;
        }

        if (!_regularPadById.ContainsKey(regularPadId))
        {
            return;
        }

        SelectedRegularPadId = regularPadId;
    }

    public void RefreshSelectionPresentation()
    {
        _selectedImpactItems.Clear();
        if (!TryGetSelectedRegularPad(out _))
        {
            OnSelectionPresentationChanged();
            return;
        }

        if (IsManualSource)
        {
            SelectedManualValue = _readManualOverrides().TryGetValue(SelectedRegularPadId, out var overrideValue)
                ? overrideValue
                : GlobalValue;
        }

        OnSelectionPresentationChanged();
        RebuildSelectedImpactItems();
        _propertyChanged(nameof(SelectedImpactSummaryText));
        _propertyChanged(nameof(HasSelectedImpactItems));
    }

    private void OnSelectionPresentationChanged()
    {
        _propertyChanged(nameof(SelectedRegularHeaderText));
        _propertyChanged(nameof(SelectedRegularLocationText));
        _propertyChanged(nameof(SelectedBeforeText));
        _propertyChanged(nameof(SelectedAfterText));
        _propertyChanged(nameof(SelectedDeltaText));
        _propertyChanged(nameof(SelectedPercentChangeText));
        _propertyChanged(nameof(SelectedEmsStatusText));
        _propertyChanged(nameof(SelectedNetFlowText));
        _propertyChanged(nameof(SelectedFlowLegSummaryText));
        _propertyChanged(nameof(SelectedFlowLegs));
        _propertyChanged(nameof(HasSelectedFlowLegs));
        _propertyChanged(nameof(SelectedSourceText));
        _propertyChanged(nameof(ShowSelectedOverrideEditor));
        _propertyChanged(nameof(SelectedImpactSummaryText));
        _propertyChanged(nameof(HasSelectedImpactItems));
    }

    private bool TryGetSelectedRegularPad(out RegularPad pad)
    {
        if (_regularPadById.TryGetValue(SelectedRegularPadId, out pad!))
        {
            return true;
        }

        pad = null!;
        return false;
    }

    private bool TryGetSelectedCell(out NotchApplySimulationDiffCell cell)
    {
        if (VisibleCells.TryGetValue(SelectedRegularPadId, out cell!))
        {
            return true;
        }

        cell = null!;
        return false;
    }

    private void OnSelectedRegularPadIdChanged()
    {
        RefreshSelectionPresentation();
        _refreshCommands();
    }

    private void RebuildSelectedImpactItems()
    {
        _selectedImpactItems.Clear();
        if (Result is null || !TryGetSelectedRegularPad(out var pad))
        {
            return;
        }

        foreach (var impact in GetImpactItemsForPad(pad))
        {
            _selectedImpactItems.Add(impact);
        }
    }

    private IReadOnlyList<NotchApplySimulationImpactItemViewModel> GetImpactItemsForPad(RegularPad pad)
    {
        return _impactItemsByRegularPadId.TryGetValue(pad.RegularPadId, out var impacts)
            ? impacts
            : Array.Empty<NotchApplySimulationImpactItemViewModel>();
    }

    private void RebuildImpactItemIndex(IReadOnlyList<NotchApplySimulationAction> actions)
    {
        var indexed = new Dictionary<int, List<(double SortKey, NotchApplySimulationImpactItemViewModel Item)>>();
        foreach (var action in actions)
        {
            var sourceRowsText = BuildSourceRowsText(action.SourceRowNumbers);
            var sourceCadText = action.CadPadId.HasValue
                ? action.CadPadId.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
            var sourceDelta = action.SourceAfterValue - action.SourceBeforeValue;
            if (Math.Abs(sourceDelta) > SignificantDeltaEpsilon)
            {
                var sourceImpact = new NotchApplySimulationImpactItemViewModel(
                    "Anchor retain",
                    $"FW Diff Idx {action.AnchorDiffIndex} keeps {action.SourceRetainedPercent}%",
                    $"Combine {action.CombinePercent}% · CAD {sourceCadText}",
                    sourceDelta.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture),
                    sourceRowsText);
                AppendImpactForDiff(
                    action.IcIndex,
                    action.AnchorDiffIndex,
                    Math.Abs(sourceDelta),
                    sourceImpact,
                    indexed);
            }

            foreach (var leg in action.Legs)
            {
                var targetImpact = new NotchApplySimulationImpactItemViewModel(
                    "Target receive",
                    $"From FW Diff Idx {action.AnchorDiffIndex} at {leg.RatioPercent}%",
                    $"CAD {sourceCadText} · anchor REG {action.RegularPadId}",
                    leg.DeltaValue.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture),
                    sourceRowsText);
                AppendImpactForDiff(
                    action.IcIndex,
                    leg.TargetDiffIndex,
                    Math.Abs(leg.DeltaValue),
                    targetImpact,
                    indexed);
            }
        }

        var finalized = new Dictionary<int, IReadOnlyList<NotchApplySimulationImpactItemViewModel>>(indexed.Count);
        foreach (var (regularPadId, impacts) in indexed)
        {
            finalized[regularPadId] = impacts
                .OrderByDescending(static item => item.SortKey)
                .ThenBy(static item => item.Item.RoleText, StringComparer.Ordinal)
                .Select(static item => item.Item)
                .ToArray();
        }

        _impactItemsByRegularPadId = finalized;
    }

    private void AppendImpactForDiff(
        int icIndex,
        int diffIndex,
        double sortKey,
        NotchApplySimulationImpactItemViewModel impact,
        Dictionary<int, List<(double SortKey, NotchApplySimulationImpactItemViewModel Item)>> indexed)
    {
        if (!_regularPadIdsByIcDiff.TryGetValue((icIndex, diffIndex), out var regularPadIds))
        {
            return;
        }

        foreach (var regularPadId in regularPadIds)
        {
            if (!indexed.TryGetValue(regularPadId, out var impacts))
            {
                impacts = new List<(double SortKey, NotchApplySimulationImpactItemViewModel Item)>();
                indexed[regularPadId] = impacts;
            }

            impacts.Add((sortKey, impact));
        }
    }

    private static string BuildSourceRowsText(IReadOnlyList<int> sourceRowNumbers)
    {
        if (sourceRowNumbers.Count == 0)
        {
            return "Rows: -";
        }

        return "Rows: #" + string.Join(", #", sourceRowNumbers);
    }

    private bool IsPadInSelectedArea(RegularPad pad)
    {
        return !SelectedAreaOption.IcIndex.HasValue || pad.IcIndex == SelectedAreaOption.IcIndex.Value;
    }

    public bool IsPadInSelectedAreaAndActiveSurface(RegularPad pad)
    {
        return IsPadInSelectedArea(pad) && _activeRegularPadIds.Contains(pad.RegularPadId);
    }

    public void EnsureSelectedRegularWithinArea()
    {
        if (SelectedRegularPadId < 0)
        {
            return;
        }

        if (VisibleCells.ContainsKey(SelectedRegularPadId))
        {
            return;
        }

        SelectedRegularPadId = -1;
    }

    public int DiffViolationPadCount => _diffViolationRegularPadIds.Length;
    public bool HasDiffViolationPads => DiffViolationPadCount > 0;
    public string DiffViolationSummaryText => HasDiffViolationPads
        ? $"Potential mismatch pads: {DiffViolationPadCount.ToString(CultureInfo.InvariantCulture)}."
        : "No mismatch pads detected between notch expected delta and simulation delta.";

    public string DiffViolationPadListText
    {
        get
        {
            if (!HasDiffViolationPads)
            {
                return "REG -";
            }

            const int maxDisplayPadCount = 24;
            var ids = _diffViolationRegularPadIds;
            var preview = string.Join(
                ", ",
                ids.Take(maxDisplayPadCount)
                    .Select(static regularPadId => $"REG {regularPadId}"));
            if (ids.Length > maxDisplayPadCount)
            {
                return $"{preview}, ... (+{(ids.Length - maxDisplayPadCount).ToString(CultureInfo.InvariantCulture)})";
            }

            return preview;
        }
    }

    public double SimulationEmsAfterCap => _simulationSafetyAudit.AfterCap;
    public int SimulationSafetyViolationCount => _simulationSafetyAudit.TotalViolationCount;
    public bool HasSimulationSafetyViolations => _simulationSafetyAudit.HasViolations;
    public bool HasSimulationHighRiskDiffs => _simulationSafetyAudit.HasHighRiskDiffs;
    public int SimulationNetFlowResidualCount => _simulationSafetyAudit.TotalNetFlowResidualCount;
    public int SimulationTargetCoverageRiskCount => _simulationSafetyAudit.TotalTargetCoverageRiskCount;
    public string SimulationNetFlowResidualCountText =>
        $"Net-flow {SimulationNetFlowResidualCount.ToString(CultureInfo.InvariantCulture)}";
    public string SimulationTargetCoverageRiskCountText =>
        $"Coverage {SimulationTargetCoverageRiskCount.ToString(CultureInfo.InvariantCulture)}";
    public string SimulationSafetyStatusText =>
        _buildSafetyStatusText(_simulationSafetyAudit);
    public string SimulationEmsCapText => _buildEmsCapText(SimulationEmsAfterCap);
    public string SimulationMaxAfterText => _simulationSafetyAudit.HasCells
        ? $"Max After {_formatSafetyValue(_simulationSafetyAudit.MaxAfterValue)}"
        : "Max After -";
    public string SimulationSafetySummaryText =>
        _buildSafetySummaryText(_simulationSafetyAudit);
    public string SimulationSafetyViolationCountText =>
        $"Violations {SimulationSafetyViolationCount.ToString(CultureInfo.InvariantCulture)}";
    public string SimulationSafetyViolationBadgeText => _formatCountBadge(SimulationSafetyViolationCount);
    public string SimulationPhysicalAuditSummaryText =>
        _buildPhysicalAuditSummaryText(_simulationSafetyAudit);
    public string SimulationHighRiskDiffListText =>
        _buildHighRiskDiffListText(_simulationSafetyAudit);
    public string SimulationSafetyViolationListText =>
        _buildViolationListText(_simulationSafetyAudit);

    public string SelectedEmsStatusText
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return "-";
            }

            return FormatCellEmsStatus(cell);
        }
    }

    public string SelectedNetFlowText
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return "-";
            }

            var flow = BuildSelectedDiffFlow(cell);
            return $"NetFlow {_formatSignedSafetyValue(flow.NetFlow)} · source {_formatSignedSafetyValue(flow.SourceDelta)} · target {_formatSignedSafetyValue(flow.TargetDelta)}";
        }
    }

    public IReadOnlyList<SimulationSelectedFlowLegViewModel> SelectedFlowLegs
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return Array.Empty<SimulationSelectedFlowLegViewModel>();
            }

            return BuildSelectedDiffFlow(cell).Legs;
        }
    }

    public string SelectedFlowLegSummaryText
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return "Select a regular pad to inspect source/target legs.";
            }

            var flow = BuildSelectedDiffFlow(cell);
            if (flow.Lines.Count == 0)
            {
                return "No notch source/target leg touches this selected FW diff.";
            }

            return $"{flow.Lines.Count.ToString(CultureInfo.InvariantCulture)} source/target leg(s) touch this selected FW diff.";
        }
    }

    public bool HasSelectedFlowLegs
    {
        get
        {
            if (!TryGetSelectedCell(out var cell))
            {
                return false;
            }

            return BuildSelectedDiffFlow(cell).Lines.Count > 0;
        }
    }

    internal SimulationSafetyAuditResult GetSimulationSafetyAuditSnapshot() => _simulationSafetyAudit;

    public void RefreshDiffViolationState()
    {
        if (Result is null)
        {
            _diffViolationRegularPadIds = Array.Empty<int>();
            NotifyDiffViolationChanged();
            return;
        }

        var violations = _detectViolations(
            Result.Cells,
            Result.Actions,
            SelectedAreaOption.IcIndex);
        _diffViolationRegularPadIds = violations
            .Distinct()
            .OrderBy(static regularPadId => regularPadId)
            .ToArray();
        NotifyDiffViolationChanged();
    }

    public void RefreshSimulationSafetyState(SimulationSafetyAuditResult? audit)
    {
        _simulationSafetyAudit = audit ?? SimulationSafetyAuditResult.Empty(SimulationSafetyAuditService.DefaultEmsAfterCap);
        RebuildSimulationHighRiskDiffs();
        NotifySimulationSafetyChanged();
    }

    private void RebuildSimulationHighRiskDiffs()
    {
        _simulationHighRiskDiffs.Clear();
        foreach (var item in _simulationSafetyAudit.HighRiskDiffs)
        {
            _simulationHighRiskDiffs.Add(_projectRisk(item, _simulationSafetyAudit.AfterCap));
        }
    }

    private void NotifyDiffViolationChanged()
    {
        _propertyChanged(nameof(DiffViolationPadCount));
        _propertyChanged(nameof(HasDiffViolationPads));
        _propertyChanged(nameof(DiffViolationSummaryText));
        _propertyChanged(nameof(DiffViolationPadListText));
    }

    private void NotifySimulationSafetyChanged()
    {
        _propertyChanged(nameof(SimulationEmsAfterCap));
        _propertyChanged(nameof(SimulationSafetyViolationCount));
        _propertyChanged(nameof(HasSimulationSafetyViolations));
        _propertyChanged(nameof(HasSimulationHighRiskDiffs));
        _propertyChanged(nameof(SimulationNetFlowResidualCount));
        _propertyChanged(nameof(SimulationTargetCoverageRiskCount));
        _propertyChanged(nameof(SimulationNetFlowResidualCountText));
        _propertyChanged(nameof(SimulationTargetCoverageRiskCountText));
        _propertyChanged(nameof(SimulationSafetyStatusText));
        _propertyChanged(nameof(SimulationEmsCapText));
        _propertyChanged(nameof(SimulationMaxAfterText));
        _propertyChanged(nameof(SimulationSafetySummaryText));
        _propertyChanged(nameof(SimulationSafetyViolationCountText));
        _propertyChanged(nameof(SimulationSafetyViolationBadgeText));
        _propertyChanged(nameof(SimulationPhysicalAuditSummaryText));
        _propertyChanged(nameof(SimulationHighRiskDiffListText));
        _propertyChanged(nameof(SimulationSafetyViolationListText));
        _propertyChanged(nameof(SimulationHighRiskDiffs));
        _propertyChanged(nameof(SelectedEmsStatusText));
        _propertyChanged(nameof(SelectedNetFlowText));
        _propertyChanged(nameof(SelectedFlowLegSummaryText));
        _propertyChanged(nameof(SelectedFlowLegs));
        _propertyChanged(nameof(HasSelectedFlowLegs));
    }

    private string FormatCellEmsStatus(NotchApplySimulationDiffCell cell) =>
        _formatCellEmsStatus(cell, SimulationEmsAfterCap);

    private SelectedDiffFlow BuildSelectedDiffFlow(NotchApplySimulationDiffCell cell)
    {
        if (Result is null)
        {
            return SelectedDiffFlow.Empty;
        }

        var sourceDelta = 0d;
        var targetDelta = 0d;
        var lines = new List<string>();
        var legs = new List<SimulationSelectedFlowLegViewModel>();
        foreach (var action in Result.Actions)
        {
            if (action.IcIndex != cell.IcIndex)
            {
                continue;
            }

            var sourceRowsText = BuildSourceRowsText(action.SourceRowNumbers);
            if (action.AnchorDiffIndex == cell.DiffIndex)
            {
                var delta = action.SourceAfterValue - action.SourceBeforeValue;
                sourceDelta += delta;
                lines.Add(
                    $"Source leg: anchor FW Diff {action.AnchorDiffIndex} retain {action.SourceRetainedPercent}% => {_formatSignedSafetyValue(delta)} · {sourceRowsText}");
                legs.Add(new SimulationSelectedFlowLegViewModel(
                    "Source retain",
                    $"FW Diff {action.AnchorDiffIndex} keeps {action.SourceRetainedPercent}%",
                    $"Combine {action.CombinePercent}% · CAD {FormatCadPadId(action.CadPadId)}",
                    _formatSignedSafetyValue(delta),
                    sourceRowsText));
            }

            foreach (var leg in action.Legs)
            {
                if (leg.TargetDiffIndex != cell.DiffIndex)
                {
                    continue;
                }

                targetDelta += leg.DeltaValue;
                lines.Add(
                    $"Target leg: from FW Diff {action.AnchorDiffIndex} ratio {leg.RatioPercent}% => {_formatSignedSafetyValue(leg.DeltaValue)} · {sourceRowsText}");
                legs.Add(new SimulationSelectedFlowLegViewModel(
                    "Target receive",
                    $"From FW Diff {action.AnchorDiffIndex} at {leg.RatioPercent}%",
                    $"Anchor REG {action.RegularPadId} · CAD {FormatCadPadId(action.CadPadId)}",
                    _formatSignedSafetyValue(leg.DeltaValue),
                    sourceRowsText));
            }
        }

        return new SelectedDiffFlow(sourceDelta, targetDelta, sourceDelta + targetDelta, lines, legs);
    }

    private void SelectSimulationRiskDiff(SimulationSafetyRiskDiffViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var targetArea = AreaOptions.FirstOrDefault(option => option.IcIndex == item.IcIndex);
        if (!targetArea.Equals(default(NotchApplySimulationAreaOption))
            && !targetArea.Equals(SelectedAreaOption))
        {
            SelectedAreaOption = targetArea;
        }

        SelectRegularPad(item.RegularPadId);
        _setStatus($"Focused {item.LocationText} from Simulation safety risk list.");
    }

    private static string FormatCadPadId(int? cadPadId)
    {
        return cadPadId.HasValue
            ? cadPadId.Value.ToString(CultureInfo.InvariantCulture)
            : "-";
    }

    private sealed record SelectedDiffFlow(
        double SourceDelta,
        double TargetDelta,
        double NetFlow,
        IReadOnlyList<string> Lines,
        IReadOnlyList<SimulationSelectedFlowLegViewModel> Legs)
    {
        public static SelectedDiffFlow Empty { get; } = new(
            0d,
            0d,
            0d,
            Array.Empty<string>(),
            Array.Empty<SimulationSelectedFlowLegViewModel>());
    }

    private void SetValue<T>(ref T field, T value, string name, Action? changed = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        _propertyChanging(name);
        field = value;
        changed?.Invoke();
        _propertyChanged(name);
    }
}
