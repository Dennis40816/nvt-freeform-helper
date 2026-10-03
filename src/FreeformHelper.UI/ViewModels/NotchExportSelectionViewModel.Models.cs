using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.UI.ViewModels;

public enum NotchExportRowDisplayMode
{
    TransferOnly,
    AllRows,
    LinkedOnly,
    WarningOnly,
    NoCadOnly,
    LegacyOnly,
}

public readonly record struct NotchExportRowDisplayModeOption(
    NotchExportRowDisplayMode Value,
    string Display);

public readonly record struct NotchExportVersionOption(
    NotchAlgorithmVersion? Value,
    string Display);

public enum NotchExportSortMode
{
    RowId,
    DiffAscending,
    MatchDescending,
    IcThenDiff,
}

public readonly record struct NotchExportSortModeOption(
    NotchExportSortMode Value,
    string Display);

public enum NotchExportColumnFilterField
{
    RowNumber,
    Ic,
    Diff,
    Match,
    Mapping,
    Status,
}

public sealed partial class NotchExportIcGroupViewModel : ObservableObject
{
    private readonly ObservableCollection<NotchExportRowItemViewModel> _rows;
    private readonly Action _onSelectAll;
    private readonly Action _onSelectNone;
    private readonly int _v22Count;
    private readonly int _cadLinkedCount;

    public NotchExportIcGroupViewModel(
        int icIndex,
        IReadOnlyList<NotchExportRowItemViewModel> rows,
        Action onSelectAll,
        Action onSelectNone)
    {
        IcIndex = icIndex;
        _rows = new ObservableCollection<NotchExportRowItemViewModel>(rows ?? throw new ArgumentNullException(nameof(rows)));
        Rows = new ReadOnlyObservableCollection<NotchExportRowItemViewModel>(_rows);
        _v22Count = Rows.Count(item => item.Row.Version == NotchAlgorithmVersion.V22);
        _cadLinkedCount = Rows.Count(item => item.Row.CadPadId.HasValue);
        _onSelectAll = onSelectAll ?? throw new ArgumentNullException(nameof(onSelectAll));
        _onSelectNone = onSelectNone ?? throw new ArgumentNullException(nameof(onSelectNone));

        SelectAllGroupCommand = new RelayCommand(() => _onSelectAll());
        SelectNoneGroupCommand = new RelayCommand(() => _onSelectNone());
        UpdateCounts();
    }

    public int IcIndex { get; }
    public ReadOnlyObservableCollection<NotchExportRowItemViewModel> Rows { get; }
    public IRelayCommand SelectAllGroupCommand { get; }
    public IRelayCommand SelectNoneGroupCommand { get; }

    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private bool _isExpanded = true;

    public int TotalCount => Rows.Count;
    public string HeaderText => $"IC {IcIndex + 1}";
    public string SummaryText => $"{SelectedCount}/{TotalCount}";
    public string GroupDetailText => $"v2.2 {_v22Count} · legacy {TotalCount - _v22Count} · CAD linked {_cadLinkedCount}";

    internal void UpdateCounts()
    {
        SelectedCount = Rows.Count(row => row.IsSelected);
        OnPropertyChanged(nameof(SummaryText));
    }
}

public sealed partial class NotchExportRowItemViewModel : ObservableObject
{
    private const int NullDiffIndex = 65535;
    private const int NoOpCombinePercent = 100;
    private const int NoOpTargetRatioPercent = 0;

    public NotchExportRowItemViewModel(int rowIndex, NotchTableRow row)
    {
        RowIndex = rowIndex;
        Row = row ?? throw new ArgumentNullException(nameof(row));
    }

    public int RowIndex { get; }
    public NotchTableRow Row { get; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private bool _isPreviewSelected;

    public string RowNumberText => $"#{RowIndex + 1}";
    public string VersionText => Row.Version.ToDisplayLabel();
    public string IcText => $"IC {Row.IcIndex + 1}";
    public string DiffText => Row.DiffIndex.ToString(CultureInfo.InvariantCulture);
    public string RegularText => Row.RegularPadIndex.ToString(CultureInfo.InvariantCulture);
    public string CadText => Row.CadPadId?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public bool IsCadLinked => Row.CadPadId.HasValue;
    public string CadLinkStateText => Row.CadPadId.HasValue ? "CAD linked" : "No CAD";
    public string ValuesText => string.Join(", ", Row.Values ?? Array.Empty<int>());
    public string CodePreviewText => $"{{ {ValuesText} }}";
    public string KeyValueDisplayText => CodePreviewText;
    public string CommentText => string.IsNullOrWhiteSpace(Row.Comment) ? "-" : Row.Comment;
    public bool HasComment => !string.IsNullOrWhiteSpace(Row.Comment);
    public string CommentPreviewText => HasComment ? $"Comment: {Row.Comment}" : "Comment: -";
    public string CompactHeaderText => $"{RowNumberText} · {VersionText} · IC {Row.IcIndex + 1} · Diff {DiffText}";
    public string CompactMetaText => Row.CadPadId.HasValue
        ? $"REG {RegularText} -> CAD {CadText}"
        : $"REG {RegularText} -> CAD -";
    public string WorkspaceLinkText => $"{RowNumberText} · IC {Row.IcIndex + 1} · Diff {DiffText}";
    public string WorkspaceLinkDetailText => CompactMetaText;
    public bool IsNoOpV22Node => IsNoOpV22NodePayload(Row);
    public bool IsTransferRow => !IsNoOpV22Node;
    public bool IsStatusNoCad => !IsCadLinked;
    public bool IsStatusLegacy => Row.Version != NotchAlgorithmVersion.V22;
    public bool IsStatusWarning => !IsStatusNoCad && !IsStatusLegacy && IsNoOpV22Node;
    public bool IsStatusLinked => !IsStatusNoCad && !IsStatusLegacy && !IsStatusWarning;
    public string StatusText => BuildStatusText();
    public string MatchSummaryText => BuildMatchSummaryText(Row);
    public string MappingText => $"REG {RegularText} -> CAD {CadText}";
    public string SearchTokenText => BuildSearchTokenText();
    public string PrimaryOutcomeText => BuildPrimaryOutcomeText(Row, IsNoOpV22Node);
    public string PreviewHeaderText => $"{RowNumberText}  |  {VersionText}  |  IC {Row.IcIndex + 1}  |  Diff {DiffText}";
    public string CurrentRowContextText => $"{RowNumberText} · {VersionText} · IC {Row.IcIndex + 1} · Diff {DiffText}";
    public string PreviewPadRelationText => $"REG {RegularText} -> CAD {CadText}";
    public string PreviewPayloadText => BuildPayloadText(Row);
    public string PayloadColumnsText => BuildPayloadColumnsText(Row);
    public string PreviewFlagsText => BuildFlagsText(Row);
    public string PreviewAnalysisText => BuildAnalysisText(Row);
    public string AnalysisRatioSumText => BuildAnalysisRatioSumText(Row);
    public string AnalysisDeltaText => BuildAnalysisDeltaText(Row);
    public string PayloadAnchorText => BuildPayloadAnchorText(Row);
    public string PayloadCombineText => BuildPayloadCombineText(Row);
    public string PayloadTarget1Text => BuildPayloadTargetText(Row, target: 1);
    public string PayloadTarget2Text => BuildPayloadTargetText(Row, target: 2);
    public string MappingStatusText => BuildMappingStatusText();

    private static string BuildPayloadText(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return "Legacy payload: use Values row for detail.";
        }

        return $"Anchor diff {node.AnchorDiffIndex}, combine {node.CombinePercent}%, " +
               $"target1 diff {node.TargetDiffIndex1} ({node.TargetRatioPercent1}%), " +
               $"target2 diff {node.TargetDiffIndex2} ({node.TargetRatioPercent2}%).";
    }

    private static string BuildPrimaryOutcomeText(NotchTableRow row, bool isNoOp)
    {
        if (isNoOp)
        {
            return "No-op: keep anchor diff only (no target switch).";
        }

        if (row.V22Node is null)
        {
            return "Legacy row: interpreted by legacy firmware path.";
        }

        return BuildPayloadSummaryText(row.V22Node);
    }

    private static string BuildFlagsText(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return "n/a";
        }

        return (node.Flags & 1) != 0 ? "CONTINUATION" : "NONE";
    }

    private static string BuildPayloadColumnsText(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return $"Legacy raw columns: [{string.Join(", ", row.Values ?? Array.Empty<int>())}]";
        }

        return $"anchor_diff={node.AnchorDiffIndex}, combine={node.CombinePercent}, " +
               $"target1_diff={FormatDiff(node.TargetDiffIndex1)}, target1_ratio={node.TargetRatioPercent1}, " +
               $"target2_diff={FormatDiff(node.TargetDiffIndex2)}, target2_ratio={node.TargetRatioPercent2}, flags={node.Flags}";
    }

    private static string BuildAnalysisText(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return $"Values count={row.Values.Length}, version={row.Version.ToDisplayLabel()}.";
        }

        var ratioTotal = node.TargetRatioPercent1 + node.TargetRatioPercent2;
        var ratioDelta = node.CombinePercent - ratioTotal;
        return $"Target ratio sum={ratioTotal}% (combine={node.CombinePercent}%, delta={ratioDelta}%).";
    }

    private static string BuildAnalysisRatioSumText(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return "-";
        }

        var ratioTotal = node.TargetRatioPercent1 + node.TargetRatioPercent2;
        return $"{ratioTotal.ToString(CultureInfo.InvariantCulture)}%";
    }

    private static string BuildAnalysisDeltaText(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return "-";
        }

        var ratioTotal = node.TargetRatioPercent1 + node.TargetRatioPercent2;
        var ratioDelta = node.CombinePercent - ratioTotal;
        return $"{ratioDelta.ToString(CultureInfo.InvariantCulture)}%";
    }

    private static string BuildPayloadAnchorText(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return row.DiffIndex.ToString(CultureInfo.InvariantCulture);
        }

        return node.AnchorDiffIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildPayloadCombineText(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return "-";
        }

        return $"{node.CombinePercent.ToString(CultureInfo.InvariantCulture)}%";
    }

    private static string BuildPayloadTargetText(NotchTableRow row, int target)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return "-";
        }

        var diff = target == 1 ? node.TargetDiffIndex1 : node.TargetDiffIndex2;
        var ratio = target == 1 ? node.TargetRatioPercent1 : node.TargetRatioPercent2;
        return $"{FormatDiff(diff)} ({ratio.ToString(CultureInfo.InvariantCulture)}%)";
    }

    private string BuildMappingStatusText()
    {
        if (IsStatusNoCad)
        {
            return "No CAD mapping";
        }

        if (IsStatusWarning)
        {
            return "Linked, but payload is no-op";
        }

        if (IsStatusLegacy)
        {
            return "Legacy mapping";
        }

        return "Linked mapping";
    }

    private static bool IsNoOpV22NodePayload(NotchTableRow row)
    {
        if (row.V22Node is not NotchV22Node node)
        {
            return false;
        }

        return node.CombinePercent == NoOpCombinePercent &&
               node.TargetRatioPercent1 == NoOpTargetRatioPercent &&
               node.TargetRatioPercent2 == NoOpTargetRatioPercent &&
               node.TargetDiffIndex1 == node.TargetDiffIndex2 &&
               node.Flags == 0;
    }

    private string BuildStatusText()
    {
        if (IsStatusNoCad)
        {
            return "No CAD";
        }

        if (IsStatusLegacy)
        {
            return "Legacy";
        }

        return IsStatusWarning
            ? "No-op"
            : "Linked";
    }

    private static string BuildMatchSummaryText(NotchTableRow row)
    {
        if (row.V22Node is NotchV22Node node)
        {
            return $"{node.CombinePercent}%";
        }

        var matchPercent = TryExtractMatchPercentFromComment(row.Comment);
        return matchPercent.HasValue
            ? $"{matchPercent.Value.ToString(CultureInfo.InvariantCulture)}%"
            : "-";
    }

    private static double? TryExtractMatchPercentFromComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        const string token = "R=";
        var start = comment.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += token.Length;
        var end = comment.IndexOf('%', start);
        if (end <= start)
        {
            return null;
        }

        var raw = comment[start..end].Trim();
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string BuildPayloadSummaryText(NotchV22Node node)
    {
        return $"Anchor {node.AnchorDiffIndex}, combine {node.CombinePercent}%, " +
               $"target1 {FormatDiff(node.TargetDiffIndex1)} ({node.TargetRatioPercent1}%), " +
               $"target2 {FormatDiff(node.TargetDiffIndex2)} ({node.TargetRatioPercent2}%).";
    }

    private static string FormatDiff(int diffIndex)
    {
        return diffIndex == NullDiffIndex
            ? "none"
            : diffIndex.ToString(CultureInfo.InvariantCulture);
    }

    public string BuildSearchTokenText()
    {
        return string.Join(
            " ",
            RowNumberText,
            VersionText,
            IcText,
            $"Diff {DiffText}",
            $"REG {RegularText}",
            $"CAD {CadText}",
            StatusText,
            CommentText);
    }
}
