using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// ViewModel for displaying and editing information about one or more <see cref="RegularPad" /> objects.
/// Implements <see cref="IPadInfoChangeTracking" /> to manage pad dimension edits.
/// </summary>
public sealed partial class RegularPadInfoViewModel : ObservableObject, IPadInfoChangeTracking
{
    private const double SizeEpsilon = 1e-4; // Tolerance for comparing double values.
    // Actions provided by the main ViewModel to apply/reset pad sizes or close the info popup.
    private readonly Action<IReadOnlyList<int>, IReadOnlyList<int>, decimal?, decimal?>? _applyPadSize;
    private readonly Action<IReadOnlyList<int>, IReadOnlyList<int>>? _resetPadSize;
    private readonly Action<IReadOnlyCollection<int>, FreeformType>? _setFreeform;
    private readonly Action<IReadOnlyCollection<int>>? _highlightCadOwnerPads;
    private readonly Action<int>? _focusRegularMatches;
    private readonly Action<int>? _highlightRegularMatches;
    private readonly Action? _closePadInfo;
    private readonly IReadOnlyList<int> _padIndices; // Indices of the regular pads being displayed.
    private readonly IReadOnlyList<int> _rows; // Distinct rows of selected pads.
    private readonly IReadOnlyList<int> _cols; // Distinct columns of selected pads.
    private readonly int? _regularPadIdForMatchActions;

    // Internal flags for change tracking.
    private bool _isLoading;
    private decimal _padWidthInitial;
    private decimal _padHeightInitial;
    private bool _padWidthDirty;
    private bool _padHeightDirty;
    private readonly DisplayValues _display;

    /// <summary>
    /// Gets a value indicating whether multiple regular pads are being displayed/edited.
    /// </summary>
    public bool IsMulti => _display.Identity.IsMulti;
    /// <summary>
    /// Gets a value indicating whether a single regular pad is being displayed/edited.
    /// </summary>
    public bool IsSingle => !IsMulti;
    /// <summary>
    /// Gets the number of regular pads currently selected.
    /// </summary>
    public int SelectedCount => _display.Identity.SelectedCount;

    // --- Display Properties ---
    public string Title => _display.Identity.Title;
    public string Subtitle => _display.Identity.Subtitle;
    public string HeaderAxisXText => _display.Identity.HeaderAxisXText;
    public string HeaderAxisYText => _display.Identity.HeaderAxisYText;
    public string IdentityText => _display.Identity.IdentityText;
    public string RowText => _display.Identity.RowText;
    public string ColText => _display.Identity.ColText;
    public string IndexText => _display.Identity.IndexText;
    public string IcIndexText => _display.Identity.IcIndexText;
    public string DiffIndexText => _display.Identity.DiffIndexText;
    public string IcText => _display.Identity.IcText;
    public string RegularPadIdText => _display.Identity.RegularPadIdText;
    public string RowColText => _display.Identity.RowColText;
    public string MatchFreeformText => _display.Match.MatchFreeformText;
    public string BoundsText => _display.Geometry.BoundsText;
    public string CentroidText => _display.Geometry.CentroidText;
    public string AreaText => _display.Geometry.AreaText;
    public string MatchText => _display.Match.MatchText;
    public string MatchDetailsText => _display.Match.MatchDetailsText;
    public string MatchConfidenceText => _display.Match.MatchConfidenceText;
    public string FreeformText => _display.Match.FreeformText;
    public string DiffSourceText => _display.Identity.DiffSourceText;
    public string OwnerSummaryText => _display.Notch.OwnerSummaryText;
    public IReadOnlyList<PadInfoRelationCardViewModel> MatchAreaCards => _display.Match.MatchAreaCards;
    public bool HasMatchAreaCards => MatchAreaCards.Count > 0;
    public string NotchRowsSummaryText => _display.Notch.NotchRowsSummaryText;
    public IReadOnlyList<PadInfoRelationCardViewModel> NotchRowFlowCards => _display.Notch.NotchRowFlowCards;
    public bool HasNotchRowFlowCards => NotchRowFlowCards.Count > 0;
    public string RowRangeText => _display.Identity.RowRangeText;
    public string ColRangeText => _display.Identity.ColRangeText;
    public bool HasMatchNavigation { get; }
    public bool HasHighlightMatchAction { get; }
    public bool HasAnyMatchAction => HasMatchNavigation || HasHighlightMatchAction;
    public bool HasLocateMatchAction => HasAnyMatchAction;
    public IReadOnlyList<string> RuleTraceLines => _display.Notch.RuleTraceLines;
    public bool HasRuleTrace => RuleTraceLines.Count > 0;
    /// <summary>
    /// Gets a value indicating whether the pad width is mixed across multiple selected pads.
    /// </summary>
    public bool IsPadWidthMixed { get; }
    /// <summary>
    /// Gets a value indicating whether the pad height is mixed across multiple selected pads.
    /// </summary>
    public bool IsPadHeightMixed { get; }

    /// <inheritdoc />
    public bool HasPendingChanges => _padWidthDirty || _padHeightDirty;

    /// <summary>
    /// Gets or sets the width of the regular pad(s).
    /// </summary>
    [ObservableProperty]
    private decimal _padWidth;

    /// <summary>
    /// Gets or sets the height of the regular pad(s).
    /// </summary>
    [ObservableProperty]
    private decimal _padHeight;

    /// <summary>
    /// Gets or sets a value indicating whether geometry detail fields are visible.
    /// </summary>
    [ObservableProperty]
    private bool _showGeometryDetails;

    /// <summary>
    /// Gets the command to reset the pad size to its default (auto-distributed) state.
    /// </summary>
    public IRelayCommand ResetPadSizeCommand { get; }
    public IRelayCommand LocateMatchCommand { get; }
    public IRelayCommand ToggleGeometryDetailsCommand { get; }
    public IRelayCommand SetFreeformNoneCommand { get; }
    public IRelayCommand SetFreeformXCommand { get; }
    public IRelayCommand SetFreeformYCommand { get; }
    public IRelayCommand SetFreeformXYCommand { get; }
    public string GeometryDetailsToggleText => ShowGeometryDetails ? "Hide details" : "Show details";
    /// <inheritdoc />
    public ICommand ApplyChangesCommand { get; }
    /// <inheritdoc />
    public ICommand DiscardChangesCommand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RegularPadInfoViewModel" /> class.
    /// </summary>
    /// <param name="pads">A list of <see cref="RegularPad" /> objects to display information for.</param>
    /// <param name="toDisplayRow">A function to convert an actual row index to a display row index.</param>
    /// <param name="getDxfIndex">A function that returns the DXF index of a pad ID, or <c>null</c>.</param>
    /// <param name="getMatchedCadPadIds">A function that returns the IDs of the CAD pads matched to a regular pad ID.</param>
    /// <param name="getMatchedCadLinks">A function that returns the match links from a regular pad ID to CAD pads.</param>
    /// <param name="applyPadSize">An action to apply new width/height to selected pads.</param>
    /// <param name="resetPadSize">An action to reset sizing for selected pads.</param>
    /// <param name="setFreeform">An action that sets the freeform type of the given regular pad IDs.</param>
    /// <param name="getCrossIcOwnerShares">A function that returns the CAD owner shares (CAD ID, percent) of a regular pad ID across ICs.</param>
    /// <param name="getNotchRowsByRegularPad">A function that returns the notch row previews of a regular pad ID.</param>
    /// <param name="highlightCadOwnerPads">An action that highlights the CAD pads that own the given IDs.</param>
    /// <param name="focusRegularMatches">An action that focuses the matches of a regular pad ID on the canvas.</param>
    /// <param name="highlightRegularMatches">An action that highlights the matches of a regular pad ID.</param>
    /// <param name="closePadInfo">An action to close the pad information popup.</param>
    /// <param name="inspectorSnapshot">Optional inspector snapshot of the shown pads.</param>
    public RegularPadInfoViewModel(
        IReadOnlyList<RegularPad> pads,
        Func<int, int> toDisplayRow,
        Func<int, int?>? getDxfIndex,
        Func<int, IReadOnlyList<int>>? getMatchedCadPadIds,
        Func<int, IReadOnlyList<PadMatchLink>>? getMatchedCadLinks,
        Action<IReadOnlyList<int>, IReadOnlyList<int>, decimal?, decimal?>? applyPadSize,
        Action<IReadOnlyList<int>, IReadOnlyList<int>>? resetPadSize,
        Action<IReadOnlyCollection<int>, FreeformType>? setFreeform,
        Func<int, IReadOnlyList<(int CadId, double RatioPercent)>>? getCrossIcOwnerShares,
        Func<int, IReadOnlyList<FreeformHelperViewModel.NotchRowPreview>>? getNotchRowsByRegularPad,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads,
        Action<int>? focusRegularMatches,
        Action<int>? highlightRegularMatches,
        Action? closePadInfo = null,
        PadInspectorSnapshot? inspectorSnapshot = null)
    {
        _applyPadSize = applyPadSize;
        _resetPadSize = resetPadSize;
        _setFreeform = setFreeform;
        _highlightCadOwnerPads = highlightCadOwnerPads;
        _focusRegularMatches = focusRegularMatches;
        _highlightRegularMatches = highlightRegularMatches;
        _closePadInfo = closePadInfo;
        _padIndices = pads.Select(p => p.RegularPadId).ToList();
        _rows = pads.Select(p => p.Row).Distinct().OrderBy(r => r).ToList();
        _cols = pads.Select(p => p.Col).Distinct().OrderBy(c => c).ToList();
        _display = BuildDisplay(
            pads, toDisplayRow, getDxfIndex, getMatchedCadPadIds, getMatchedCadLinks,
            getCrossIcOwnerShares, getNotchRowsByRegularPad, _highlightCadOwnerPads, inspectorSnapshot);
        IsPadWidthMixed = _display.Geometry.IsPadWidthMixed;
        IsPadHeightMixed = _display.Geometry.IsPadHeightMixed;
        InitializePadSize();
        _regularPadIdForMatchActions = _display.Match.RegularPadIdForMatchActions;
        HasMatchNavigation = _focusRegularMatches is not null && _regularPadIdForMatchActions.HasValue;
        HasHighlightMatchAction = _highlightRegularMatches is not null && _regularPadIdForMatchActions.HasValue;

        ResetPadSizeCommand = new RelayCommand(() =>
        {
            _resetPadSize?.Invoke(_rows, _cols);
            _closePadInfo?.Invoke();
        });
        LocateMatchCommand = new RelayCommand(LocateMatch);
        ToggleGeometryDetailsCommand = new RelayCommand(() => ShowGeometryDetails = !ShowGeometryDetails);
        SetFreeformNoneCommand = new RelayCommand(() => ApplyFreeform(FreeformType.None));
        SetFreeformXCommand = new RelayCommand(() => ApplyFreeform(FreeformType.XWay));
        SetFreeformYCommand = new RelayCommand(() => ApplyFreeform(FreeformType.YWay));
        SetFreeformXYCommand = new RelayCommand(() => ApplyFreeform(FreeformType.XYWay));
        ApplyChangesCommand = new RelayCommand(ApplyChanges);
        DiscardChangesCommand = new RelayCommand(DiscardChanges);
    }

    private static List<PadInfoRelationCardViewModel> BuildMatchAreaCards(
        IReadOnlyList<PadMatchLink> matchedLinks,
        Func<int, int?>? getDxfIndex,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads)
    {
        return matchedLinks
            .Take(12)
            .Select(link =>
            {
                var outputDiff = getDxfIndex?.Invoke(link.CadPadId);
                var metaText = outputDiff.HasValue
                    ? $"CAD Output FW diff {outputDiff.Value}"
                    : "CAD Output FW diff -";
                var ratioText = $"REG {link.RegularCoverage:P0} / CAD {link.CadCoverage:P0}";
                var rawText = $"CAD {link.CadPadId} overlap {link.OverlapArea:0.####} {PadInfoUnits.SquareMillimeter}; regular coverage {link.RegularCoverage:P2}; CAD coverage {link.CadCoverage:P2}.";
                var command = highlightCadOwnerPads is null
                    ? null
                    : new RelayCommand(() => highlightCadOwnerPads(new[] { link.CadPadId }));

                return new PadInfoRelationCardViewModel(
                    $"CAD {link.CadPadId}",
                    "Overlap",
                    metaText,
                    $"{link.OverlapArea:0.####} {PadInfoUnits.SquareMillimeter}",
                    ratioText,
                    rawText,
                    command);
            })
            .ToList();
    }

    private static DisplayValues BuildDisplay(
        IReadOnlyList<RegularPad> pads,
        Func<int, int> toDisplayRow,
        Func<int, int?>? getDxfIndex,
        Func<int, IReadOnlyList<int>>? getMatchedCadPadIds,
        Func<int, IReadOnlyList<PadMatchLink>>? getMatchedCadLinks,
        Func<int, IReadOnlyList<(int CadId, double RatioPercent)>>? getCrossIcOwnerShares,
        Func<int, IReadOnlyList<FreeformHelperViewModel.NotchRowPreview>>? getNotchRowsByRegularPad,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads,
        PadInspectorSnapshot? inspectorSnapshot)
    {
        var selectedCount = pads.Count;
        var isMulti = pads.Count > 1;
        var display = new DisplayValues(
            new IdentityDisplayValues(
                IsMulti: isMulti,
                SelectedCount: selectedCount,
                Title: isMulti ? "Regular pads" : "Regular pad",
                Subtitle: isMulti ? $"Selected: {selectedCount} pads" : "Single pad"),
            new GeometryDisplayValues(),
            new MatchDisplayValues(MatchAreaCards: new List<PadInfoRelationCardViewModel>()),
            new NotchDisplayValues(
                NotchRowFlowCards: new List<PadInfoRelationCardViewModel>(),
                RuleTraceLines: new List<string>()));
        var snapshotRegular = !display.Identity.IsMulti &&
                              inspectorSnapshot?.Regular is RegularPadInspectorSnapshot candidate &&
                              candidate.RegularPadId == pads[0].RegularPadId
            ? candidate
            : null;
        if (display.Identity.IsMulti)
        {
            display = BuildMultiSelectionDisplay(pads, toDisplayRow, display);
        }
        else
        {
            var pad = pads[0];
            var displayRow = snapshotRegular?.DisplayRow ?? toDisplayRow(pad.Row);
            display = BuildSingleIdentityDisplay(pad, displayRow, snapshotRegular?.DiffIndex ?? pad.DiffIndex, display);
            display = snapshotRegular is not null
                ? BuildSnapshotDisplay(pad, snapshotRegular, display, getDxfIndex, getMatchedCadLinks, highlightCadOwnerPads)
                : BuildCallbackDisplay(pad, display, getDxfIndex, getMatchedCadPadIds, getMatchedCadLinks, highlightCadOwnerPads);
            display = BuildOwnerDisplay(pad.RegularPadId, display, getCrossIcOwnerShares);
            display = BuildNotchRowDisplay(pad.RegularPadId, display, getNotchRowsByRegularPad, highlightCadOwnerPads);
        }

        display = BuildGeometryDisplay(pads, display);
        return BuildPadSizeDisplay(pads, display);
    }

    private static DisplayValues BuildMultiSelectionDisplay(
        IReadOnlyList<RegularPad> pads,
        Func<int, int> toDisplayRow,
        DisplayValues display)
    {
        var icIndexText = PadInfoTextFormatter.RangeText(pads.Select(p => p.IcIndex + 1));
        var diffIndexText = PadInfoTextFormatter.RangeText(pads.Select(p => p.DiffIndex));
        var regularPadIdText = PadInfoTextFormatter.RangeText(pads.Select(p => p.RegularPadId));
        var rows = pads.Select(p => toDisplayRow(p.Row));
        var cols = pads.Select(p => p.Col);
        return display with
        {
            Identity = display.Identity with
            {
                IdentityText = $"Regular pads · {display.Identity.Subtitle}",
                IcIndexText = icIndexText,
                DiffIndexText = diffIndexText,
                IcText = icIndexText,
                RegularPadIdText = regularPadIdText,
                RowRangeText = FormatIndexRanges(rows),
                ColRangeText = FormatIndexRanges(cols),
            },
            Match = display.Match with
            {
                MatchText = "Mixed",
                MatchDetailsText = "Mixed",
                FreeformText = PadInfoTextFormatter.DistinctText(pads.Select(p => p.Freeform.ToString())),
            },
        };
    }

    private static DisplayValues BuildSingleIdentityDisplay(
        RegularPad pad,
        int displayRow,
        int diffIndex,
        DisplayValues display)
    {
        var xOneBased = pad.Col + 1;
        var yOneBased = displayRow + 1;
        var rowText = displayRow.ToString(CultureInfo.InvariantCulture);
        var colText = pad.Col.ToString(CultureInfo.InvariantCulture);
        var indexText = pad.RegularPadId.ToString(CultureInfo.InvariantCulture);
        var icIndexText = (pad.IcIndex + 1).ToString(CultureInfo.InvariantCulture);
        var diffIndexText = diffIndex.ToString(CultureInfo.InvariantCulture);
        return display with
        {
            Identity = display.Identity with
            {
                HeaderAxisXText = $"X {xOneBased}",
                HeaderAxisYText = $"Y {yOneBased}",
                RowText = rowText,
                ColText = colText,
                IndexText = indexText,
                IcIndexText = icIndexText,
                DiffIndexText = diffIndexText,
                IcText = icIndexText,
                RegularPadIdText = indexText,
                RowColText = $"Row {rowText}, Col {colText}",
                RowRangeText = rowText,
                ColRangeText = colText,
                IdentityText = $"REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · Diff idx {diffIndexText}",
            },
        };
    }

    private static DisplayValues BuildSnapshotDisplay(
        RegularPad pad,
        RegularPadInspectorSnapshot snapshotRegular,
        DisplayValues display,
        Func<int, int?>? getDxfIndex,
        Func<int, IReadOnlyList<PadMatchLink>>? getMatchedCadLinks,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads)
    {
        var freeformText = snapshotRegular.Freeform == FreeformType.None
            ? snapshotRegular.Freeform.ToString()
            : $"{snapshotRegular.Freeform} ({snapshotRegular.FreeformSource})";
        display = display with
        {
            Match = display.Match with
            {
                MatchText = snapshotRegular.MatchText,
                MatchDetailsText = snapshotRegular.MatchDetailsText,
                MatchConfidenceText = snapshotRegular.MatchedCadPadIds.Count == 0
                    ? "-"
                    : snapshotRegular.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture),
                FreeformText = freeformText,
                MatchFreeformText = $"Match {snapshotRegular.MatchText}, Freeform {freeformText}",
                RegularPadIdForMatchActions = snapshotRegular.MatchedCadPadIds.Count > 0 ? pad.RegularPadId : null,
            },
            Identity = display.Identity with
            {
                DiffSourceText = snapshotRegular.DiffSource,
            },
        };
        var matchedLinks = getMatchedCadLinks?.Invoke(pad.RegularPadId)
            .OrderByDescending(link => link.RegularCoverage)
            .ThenByDescending(link => link.CadCoverage)
            .ThenBy(link => link.CadPadId)
            .ToList()
            ?? new List<PadMatchLink>();
        var matchAreaCards = BuildMatchAreaCards(matchedLinks, getDxfIndex, highlightCadOwnerPads);
        return display with
        {
            Match = display.Match with
            {
                MatchAreaCards = matchAreaCards,
            },
            Notch = display.Notch with
            {
                RuleTraceLines = snapshotRegular.RuleTrace.Select(PadInspectorRuleTraceFormatter.Format).ToList(),
            },
        };
    }

    private static DisplayValues BuildCallbackDisplay(
        RegularPad pad,
        DisplayValues display,
        Func<int, int?>? getDxfIndex,
        Func<int, IReadOnlyList<int>>? getMatchedCadPadIds,
        Func<int, IReadOnlyList<PadMatchLink>>? getMatchedCadLinks,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads)
    {
        var matchedLinks = getMatchedCadLinks?.Invoke(pad.RegularPadId)
            .OrderByDescending(link => link.CadCoverage)
            .ThenBy(link => link.CadPadId)
            .ToList()
            ?? new List<PadMatchLink>();
        var matchedCadIds = (getMatchedCadPadIds?.Invoke(pad.RegularPadId) ?? Array.Empty<int>())
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var matchText = BuildMatchText(getDxfIndex, matchedCadIds);
        var matchDetailsText = BuildRegularMatchDetailsText(getDxfIndex, matchedCadIds, matchedLinks);
        var freeformText = pad.Freeform.ToString();
        display = display with
        {
            Match = display.Match with
            {
                MatchText = matchText,
                MatchDetailsText = matchDetailsText,
                FreeformText = freeformText,
                MatchFreeformText = $"Match {matchText}, Freeform {freeformText}",
                RegularPadIdForMatchActions = matchedCadIds.Count > 0 ? pad.RegularPadId : null,
            },
        };
        return display with
        {
            Match = display.Match with
            {
                MatchAreaCards = BuildMatchAreaCards(matchedLinks, getDxfIndex, highlightCadOwnerPads),
            },
        };
    }

    private static DisplayValues BuildOwnerDisplay(
        int regularPadId,
        DisplayValues display,
        Func<int, IReadOnlyList<(int CadId, double RatioPercent)>>? getCrossIcOwnerShares)
    {
        var ownerInfos = getCrossIcOwnerShares?.Invoke(regularPadId)
            ?.OrderByDescending(info => info.RatioPercent)
            .ThenBy(info => info.CadId)
            .ToList()
            ?? new List<(int CadId, double RatioPercent)>();
        if (ownerInfos.Count == 0)
        {
            return display;
        }

        var preview = ownerInfos
            .Take(4)
            .Select(owner => $"CAD {owner.CadId} {owner.RatioPercent:0.##}%");
        var ownerSummaryText = string.Join(", ", preview);
        if (ownerInfos.Count > 4)
        {
            ownerSummaryText += $" (+{ownerInfos.Count - 4})";
        }

        return display with { Notch = display.Notch with { OwnerSummaryText = ownerSummaryText } };
    }

    private static DisplayValues BuildNotchRowDisplay(
        int regularPadId,
        DisplayValues display,
        Func<int, IReadOnlyList<FreeformHelperViewModel.NotchRowPreview>>? getNotchRowsByRegularPad,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads)
    {
        var relatedRows = getNotchRowsByRegularPad?.Invoke(regularPadId)
            ?.OrderBy(row => row.RowNumber)
            .ToList()
            ?? new List<FreeformHelperViewModel.NotchRowPreview>();
        var notchRowSummaryText = $"Notch rows: {relatedRows.Count} (REG {regularPadId})";
        var notchRowFlowCards = new List<PadInfoRelationCardViewModel>();
        foreach (var row in relatedRows.Take(8))
        {
            var cadText = row.CadPadId.HasValue ? $"CAD {row.CadPadId.Value}" : "CAD -";
            var rowText = $"#{row.RowNumber} {row.Version.ToDisplayLabel()} IC{row.IcIndex + 1} diff{row.DiffIndex} {cadText}";
            var highlightCommand = highlightCadOwnerPads is not null && row.CadPadId.HasValue
                ? new RelayCommand(() => highlightCadOwnerPads(new[] { row.CadPadId.Value }))
                : null;
            notchRowFlowCards.Add(new PadInfoRelationCardViewModel(
                rowText,
                "Row",
                $"FW diff {row.DiffIndex}",
                "Step 5",
                row.Version.ToDisplayLabel(),
                rowText,
                highlightCommand,
                compactTitleText: $"#{row.RowNumber} IC{row.IcIndex + 1} diff{row.DiffIndex} {cadText}"));
        }

        return display with
        {
            Notch = display.Notch with
            {
                NotchRowsSummaryText = notchRowSummaryText,
                NotchRowFlowCards = notchRowFlowCards,
            },
        };
    }

    private static DisplayValues BuildGeometryDisplay(IReadOnlyList<RegularPad> pads, DisplayValues display)
    {
        var bounds = UnionBounds(pads.Select(p => p.Bounds));
        return display with
        {
            Geometry = display.Geometry with
            {
                BoundsText = FormatBounds(bounds),
                CentroidText = display.Identity.IsMulti ? "Mixed" : FormatPoint(pads[0].Centroid),
                AreaText = display.Identity.IsMulti
                    ? $"{pads.Min(p => p.Area):0.####} - {pads.Max(p => p.Area):0.####} {PadInfoUnits.SquareMillimeter}"
                    : $"{pads[0].Area:0.####} {PadInfoUnits.SquareMillimeter}",
            },
        };
    }

    private static DisplayValues BuildPadSizeDisplay(IReadOnlyList<RegularPad> pads, DisplayValues display)
    {
        // Handle pad dimensions: check for mixed state and set initial values.
        var widthValues = pads.Select(p => p.Bounds.Width).ToList();
        var heightValues = pads.Select(p => p.Bounds.Height).ToList();
        var widthMin = widthValues.Min();
        var widthMax = widthValues.Max();
        var heightMin = heightValues.Min();
        var heightMax = heightValues.Max();
        var isPadWidthMixed = (widthMax - widthMin) > SizeEpsilon;
        var isPadHeightMixed = (heightMax - heightMin) > SizeEpsilon;
        return display with
        {
            Geometry = display.Geometry with
            {
                IsPadWidthMixed = isPadWidthMixed,
                IsPadHeightMixed = isPadHeightMixed,
                PadWidth = isPadWidthMixed ? 0m : (decimal)((widthMin + widthMax) * 0.5),
                PadHeight = isPadHeightMixed ? 0m : (decimal)((heightMin + heightMax) * 0.5),
            },
        };
    }

    private void InitializePadSize()
    {
        _isLoading = true; // Suppress dirty tracking during initial load.
        PadWidth = _display.Geometry.PadWidth;
        PadHeight = _display.Geometry.PadHeight;
        _isLoading = false;
        _padWidthInitial = PadWidth;
        _padHeightInitial = PadHeight;
        _padWidthDirty = false;
        _padHeightDirty = false;
        ShowGeometryDetails = false;
    }

    private sealed record DisplayValues(
        IdentityDisplayValues Identity,
        GeometryDisplayValues Geometry,
        MatchDisplayValues Match,
        NotchDisplayValues Notch);

    private sealed record IdentityDisplayValues(
        bool IsMulti,
        int SelectedCount,
        string Title,
        string Subtitle,
        string HeaderAxisXText = "-",
        string HeaderAxisYText = "-",
        string IdentityText = "",
        string RowText = "-",
        string ColText = "-",
        string IndexText = "-",
        string IcIndexText = "-",
        string DiffIndexText = "-",
        string IcText = "-",
        string RegularPadIdText = "-",
        string RowColText = "-",
        string DiffSourceText = "-",
        string RowRangeText = "-",
        string ColRangeText = "-");

    private sealed record GeometryDisplayValues(
        string BoundsText = "",
        string CentroidText = "",
        string AreaText = "",
        bool IsPadWidthMixed = false,
        bool IsPadHeightMixed = false,
        decimal PadWidth = 0m,
        decimal PadHeight = 0m);

    private sealed record MatchDisplayValues(
        IReadOnlyList<PadInfoRelationCardViewModel> MatchAreaCards,
        string MatchFreeformText = "-",
        string MatchText = "Unmatched",
        string MatchDetailsText = "Unmatched",
        string MatchConfidenceText = "-",
        string FreeformText = "-",
        int? RegularPadIdForMatchActions = null);

    private sealed record NotchDisplayValues(
        IReadOnlyList<PadInfoRelationCardViewModel> NotchRowFlowCards,
        IReadOnlyList<string> RuleTraceLines,
        string OwnerSummaryText = "Owners: -",
        string NotchRowsSummaryText = "Notch rows: generate Step 5 first.");

}
