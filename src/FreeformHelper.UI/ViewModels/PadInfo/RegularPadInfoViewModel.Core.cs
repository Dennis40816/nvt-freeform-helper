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
    private readonly IReadOnlyList<int> _allOwnerCadIds = Array.Empty<int>();

    // Internal flags for change tracking.
    private bool _isLoading;
    private decimal _padWidthInitial;
    private decimal _padHeightInitial;
    private bool _padWidthDirty;
    private bool _padHeightDirty;

    /// <summary>
    /// Gets a value indicating whether multiple regular pads are being displayed/edited.
    /// </summary>
    public bool IsMulti { get; }
    /// <summary>
    /// Gets a value indicating whether a single regular pad is being displayed/edited.
    /// </summary>
    public bool IsSingle => !IsMulti;
    /// <summary>
    /// Gets the number of regular pads currently selected.
    /// </summary>
    public int SelectedCount { get; }

    // --- Display Properties ---
    public string Title { get; }
    public string Subtitle { get; }
    public string HeaderAxisXText { get; }
    public string HeaderAxisYText { get; }
    public string IdentityText { get; }
    public string RowText { get; }
    public string ColText { get; }
    public string IndexText { get; }
    public string IcIndexText { get; }
    public string DiffIndexText { get; }
    public string IcText { get; }
    public string RegularPadIdText { get; }
    public string RowColText { get; }
    public string MatchFreeformText { get; }
    public string BoundsText { get; }
    public string CentroidText { get; }
    public string AreaText { get; }
    public string MatchText { get; }
    public string MatchDetailsText { get; }
    public string MatchConfidenceText { get; }
    public string FreeformText { get; }
    public string DiffSourceText { get; }
    public string OwnerSummaryText { get; }
    public IReadOnlyList<PadInfoRelationCardViewModel> MatchAreaCards { get; }
    public bool HasMatchAreaCards => MatchAreaCards.Count > 0;
    public IReadOnlyList<NotchOwnerLinkViewModel> OwnerLinks { get; }
    public bool HasOwnerLinks => OwnerLinks.Count > 0;
    public bool HasHighlightAllOwners => HasOwnerLinks && _allOwnerCadIds.Count > 1;
    public string HighlightAllOwnersText => $"Highlight all owners ({_allOwnerCadIds.Count})";
    public string NotchRowsSummaryText { get; }
    public IReadOnlyList<PadInfoRelationCardViewModel> NotchRowFlowCards { get; }
    public bool HasNotchRowFlowCards => NotchRowFlowCards.Count > 0;
    public IReadOnlyList<NotchOwnerLinkViewModel> NotchRowLinks { get; }
    public bool HasNotchRows => !string.IsNullOrWhiteSpace(NotchRowsSummaryText);
    public bool HasNotchRowLinks => NotchRowLinks.Count > 0;
    public string RowRangeText { get; }
    public string ColRangeText { get; }
    public bool HasMatchNavigation { get; }
    public bool HasNoMatchNavigation => !HasMatchNavigation;
    public bool HasHighlightMatchAction { get; }
    public bool HasAnyMatchAction => HasMatchNavigation || HasHighlightMatchAction;
    public bool HasLocateMatchAction => HasAnyMatchAction;
    public IReadOnlyList<string> RuleTraceLines { get; }
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
    public IRelayCommand NavigateToMatchCommand { get; }
    public IRelayCommand LocateMatchCommand { get; }
    public IRelayCommand HighlightMatchesCommand { get; }
    public IRelayCommand ToggleGeometryDetailsCommand { get; }
    public IRelayCommand SetFreeformNoneCommand { get; }
    public IRelayCommand SetFreeformXCommand { get; }
    public IRelayCommand SetFreeformYCommand { get; }
    public IRelayCommand SetFreeformXYCommand { get; }
    public IRelayCommand HighlightAllOwnersCommand { get; }
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
    /// <param name="applyPadSize">An action to apply new width/height to selected pads.</param>
    /// <param name="resetPadSize">An action to reset sizing for selected pads.</param>
    /// <param name="closePadInfo">An action to close the pad information popup.</param>
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
        var regularPadIdForMatchActions = (int?)null;

        SelectedCount = pads.Count;
        IsMulti = pads.Count > 1;
        Title = IsMulti ? "Regular pads" : "Regular pad";
        Subtitle = IsMulti ? $"Selected: {SelectedCount} pads" : "Single pad";
        var snapshotRegular = !IsMulti &&
                              inspectorSnapshot?.Regular is RegularPadInspectorSnapshot candidate &&
                              candidate.RegularPadId == pads[0].RegularPadId
            ? candidate
            : null;
        var ruleTraceLines = new List<string>();
        var ownerLinks = new List<NotchOwnerLinkViewModel>();
        var matchAreaCards = new List<PadInfoRelationCardViewModel>();
        var allOwnerCadIds = new List<int>();
        var ownerSummaryText = "Owners: -";
        var notchRowSummaryText = "Notch rows: generate Step 5 first.";
        var notchRowFlowCards = new List<PadInfoRelationCardViewModel>();
        var notchRowLinks = new List<NotchOwnerLinkViewModel>();

        // Populate display texts based on whether it's a multi-selection or single.
        if (IsMulti)
        {
            HeaderAxisXText = "-";
            HeaderAxisYText = "-";
            IdentityText = $"Regular pads · {Subtitle}";
            RowText = "-";
            ColText = "-";
            IndexText = "-";
            IcIndexText = PadInfoTextFormatter.RangeText(pads.Select(p => p.IcIndex + 1));
            DiffIndexText = PadInfoTextFormatter.RangeText(pads.Select(p => p.DiffIndex));
            IcText = IcIndexText;
            RegularPadIdText = PadInfoTextFormatter.RangeText(pads.Select(p => p.RegularPadId));
            RowColText = "-";
            MatchFreeformText = "-";
            var rows = pads.Select(p => toDisplayRow(p.Row));
            var cols = pads.Select(p => p.Col);
            RowRangeText = FormatIndexRanges(rows);
            ColRangeText = FormatIndexRanges(cols);
            MatchText = "Mixed";
            MatchDetailsText = "Mixed";
            MatchConfidenceText = "-";
            DiffSourceText = "-";
            FreeformText = PadInfoTextFormatter.DistinctText(pads.Select(p => p.Freeform.ToString()));
        }
        else
        {
            var pad = pads[0];
            var displayRow = snapshotRegular?.DisplayRow ?? toDisplayRow(pad.Row);
            var xOneBased = pad.Col + 1;
            var yOneBased = displayRow + 1;
            HeaderAxisXText = $"X {xOneBased}";
            HeaderAxisYText = $"Y {yOneBased}";
            RowText = displayRow.ToString(CultureInfo.InvariantCulture);
            ColText = pad.Col.ToString(CultureInfo.InvariantCulture);
            IndexText = pad.RegularPadId.ToString(CultureInfo.InvariantCulture);
            IcIndexText = (pad.IcIndex + 1).ToString(CultureInfo.InvariantCulture);
            var diffIndexText = (snapshotRegular?.DiffIndex ?? pad.DiffIndex).ToString(CultureInfo.InvariantCulture);
            DiffIndexText = diffIndexText;
            IcText = IcIndexText;
            RegularPadIdText = IndexText;
            RowColText = $"Row {RowText}, Col {ColText}";
            RowRangeText = RowText;
            ColRangeText = ColText;
            IdentityText = $"REG {pad.RegularPadId} · IC {pad.IcIndex + 1} · Diff idx {diffIndexText}";
            if (snapshotRegular is not null)
            {
                MatchText = snapshotRegular.MatchText;
                MatchDetailsText = snapshotRegular.MatchDetailsText;
                MatchConfidenceText = snapshotRegular.MatchedCadPadIds.Count == 0
                    ? "-"
                    : snapshotRegular.MatchConfidence.ToString("P0", CultureInfo.InvariantCulture);
                DiffSourceText = snapshotRegular.DiffSource;
                FreeformText = snapshotRegular.Freeform == FreeformType.None
                    ? snapshotRegular.Freeform.ToString()
                    : $"{snapshotRegular.Freeform} ({snapshotRegular.FreeformSource})";
                MatchFreeformText = $"Match {MatchText}, Freeform {FreeformText}";
                if (snapshotRegular.MatchedCadPadIds.Count > 0)
                {
                    regularPadIdForMatchActions = pad.RegularPadId;
                }

                var matchedLinks = getMatchedCadLinks?.Invoke(pad.RegularPadId)
                    .OrderByDescending(link => link.RegularCoverage)
                    .ThenByDescending(link => link.CadCoverage)
                    .ThenBy(link => link.CadPadId)
                    .ToList()
                    ?? new List<PadMatchLink>();
                matchAreaCards.AddRange(BuildMatchAreaCards(
                    matchedLinks,
                    getDxfIndex,
                    _highlightCadOwnerPads));

                ruleTraceLines.AddRange(snapshotRegular.RuleTrace.Select(PadInspectorRuleTraceFormatter.Format));
            }
            else
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
                MatchText = BuildMatchText(getDxfIndex, matchedCadIds);
                MatchDetailsText = BuildRegularMatchDetailsText(getDxfIndex, matchedCadIds, matchedLinks);
                MatchConfidenceText = "-";
                DiffSourceText = "-";
                FreeformText = pad.Freeform.ToString();
                MatchFreeformText = $"Match {MatchText}, Freeform {FreeformText}";
                if (matchedCadIds.Count > 0)
                {
                    regularPadIdForMatchActions = pad.RegularPadId;
                }

                matchAreaCards.AddRange(BuildMatchAreaCards(
                    matchedLinks,
                    getDxfIndex,
                    _highlightCadOwnerPads));
            }

            var ownerInfos = getCrossIcOwnerShares?.Invoke(pad.RegularPadId)
                ?.OrderByDescending(info => info.RatioPercent)
                .ThenBy(info => info.CadId)
                .ToList()
                ?? new List<(int CadId, double RatioPercent)>();
            if (ownerInfos.Count > 0)
            {
                foreach (var owner in ownerInfos)
                {
                    allOwnerCadIds.Add(owner.CadId);
                    if (_highlightCadOwnerPads is null)
                    {
                        continue;
                    }

                    ownerLinks.Add(new NotchOwnerLinkViewModel(
                        $"CAD {owner.CadId} ({owner.RatioPercent:0.##}%)",
                        new RelayCommand(() => _highlightCadOwnerPads(new[] { owner.CadId }))));
                }

                var preview = ownerInfos
                    .Take(4)
                    .Select(owner => $"CAD {owner.CadId} {owner.RatioPercent:0.##}%");
                ownerSummaryText = string.Join(", ", preview);
                if (ownerInfos.Count > 4)
                {
                    ownerSummaryText += $" (+{ownerInfos.Count - 4})";
                }
            }

            var relatedRows = getNotchRowsByRegularPad?.Invoke(pad.RegularPadId)
                ?.OrderBy(row => row.RowNumber)
                .ToList()
                ?? new List<FreeformHelperViewModel.NotchRowPreview>();
            if (relatedRows.Count > 0)
            {
                notchRowSummaryText = $"Notch rows: {relatedRows.Count} (REG {pad.RegularPadId})";
                foreach (var row in relatedRows.Take(8))
                {
                    var cadText = row.CadPadId.HasValue ? $"CAD {row.CadPadId.Value}" : "CAD -";
                    var rowText = $"#{row.RowNumber} {row.Version.ToDisplayLabel()} IC{row.IcIndex + 1} diff{row.DiffIndex} {cadText}";
                    var highlightCommand = _highlightCadOwnerPads is not null && row.CadPadId.HasValue
                        ? new RelayCommand(() => _highlightCadOwnerPads(new[] { row.CadPadId.Value }))
                        : null;
                    notchRowFlowCards.Add(new PadInfoRelationCardViewModel(
                        rowText,
                        "Row",
                        $"FW diff {row.DiffIndex}",
                        "Step 5",
                        row.Version.ToDisplayLabel(),
                        rowText,
                        highlightCommand));

                    if (_highlightCadOwnerPads is null || !row.CadPadId.HasValue)
                    {
                        continue;
                    }

                    notchRowLinks.Add(new NotchOwnerLinkViewModel(
                        rowText,
                        new RelayCommand(() => _highlightCadOwnerPads(new[] { row.CadPadId.Value }))));
                }
            }
            else
            {
                notchRowSummaryText = $"Notch rows: 0 (REG {pad.RegularPadId})";
            }
        }

        var bounds = UnionBounds(pads.Select(p => p.Bounds));
        BoundsText = FormatBounds(bounds);
        CentroidText = IsMulti ? "Mixed" : FormatPoint(pads[0].Centroid);
        AreaText = IsMulti
            ? $"{pads.Min(p => p.Area):0.####} - {pads.Max(p => p.Area):0.####} {PadInfoUnits.SquareMillimeter}"
            : $"{pads[0].Area:0.####} {PadInfoUnits.SquareMillimeter}";

        // Handle pad dimensions: check for mixed state and set initial values.
        var widthValues = pads.Select(p => p.Bounds.Width).ToList();
        var heightValues = pads.Select(p => p.Bounds.Height).ToList();
        var widthMin = widthValues.Min();
        var widthMax = widthValues.Max();
        var heightMin = heightValues.Min();
        var heightMax = heightValues.Max();
        IsPadWidthMixed = (widthMax - widthMin) > SizeEpsilon;
        IsPadHeightMixed = (heightMax - heightMin) > SizeEpsilon;

        _isLoading = true; // Suppress dirty tracking during initial load.
        PadWidth = IsPadWidthMixed ? 0m : (decimal)((widthMin + widthMax) * 0.5);
        PadHeight = IsPadHeightMixed ? 0m : (decimal)((heightMin + heightMax) * 0.5);
        _isLoading = false;

        _padWidthInitial = PadWidth;
        _padHeightInitial = PadHeight;
        _padWidthDirty = false;
        _padHeightDirty = false;
        ShowGeometryDetails = false;
        _regularPadIdForMatchActions = regularPadIdForMatchActions;
        HasMatchNavigation = _focusRegularMatches is not null && _regularPadIdForMatchActions.HasValue;
        HasHighlightMatchAction = _highlightRegularMatches is not null && _regularPadIdForMatchActions.HasValue;
        OwnerSummaryText = ownerSummaryText;
        MatchAreaCards = matchAreaCards;
        OwnerLinks = ownerLinks;
        _allOwnerCadIds = allOwnerCadIds;
        NotchRowsSummaryText = notchRowSummaryText;
        NotchRowFlowCards = notchRowFlowCards;
        NotchRowLinks = notchRowLinks;
        RuleTraceLines = ruleTraceLines;

        ResetPadSizeCommand = new RelayCommand(() =>
        {
            _resetPadSize?.Invoke(_rows, _cols);
            _closePadInfo?.Invoke();
        });
        NavigateToMatchCommand = new RelayCommand(NavigateToMatch);
        LocateMatchCommand = new RelayCommand(LocateMatch);
        HighlightMatchesCommand = new RelayCommand(HighlightMatches);
        ToggleGeometryDetailsCommand = new RelayCommand(() => ShowGeometryDetails = !ShowGeometryDetails);
        SetFreeformNoneCommand = new RelayCommand(() => ApplyFreeform(FreeformType.None));
        SetFreeformXCommand = new RelayCommand(() => ApplyFreeform(FreeformType.XWay));
        SetFreeformYCommand = new RelayCommand(() => ApplyFreeform(FreeformType.YWay));
        SetFreeformXYCommand = new RelayCommand(() => ApplyFreeform(FreeformType.XYWay));
        HighlightAllOwnersCommand = new RelayCommand(HighlightAllOwners);
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
}
