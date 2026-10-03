using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Step 5 notch quick validation helpers (regular-centric trace).
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private void UseSelectedRegularForNotchValidation()
    {
        var selectedRegularPadId = GetPrimarySelectedRegularPadIdForNotchValidation();
        if (!selectedRegularPadId.HasValue)
        {
            NotchValidationSummaryText = "Validation: select one REG first.";
            SetStatus("Validation: no Regular selected.");
            return;
        }

        NotchValidationRegularPadId = selectedRegularPadId.Value;
        AnalyzeNotchValidation();
    }

    private void AnalyzeNotchValidation()
    {
        var regularPadId = Math.Clamp((int)Math.Round(NotchValidationRegularPadId), 0, int.MaxValue);
        if (!TryGetNotchValidationReportForRegularPad(
                regularPadId,
                out var report,
                out var nullDiffValue,
                out _,
                out var failureMessage))
        {
            ClearNotchValidationCollections();
            NotchValidationSummaryText = failureMessage;
            SetStatus(failureMessage);
            return;
        }

        var trace = NotchValidationTraceService.BuildTrace(report, RegularPads, nullDiffValue);
        var mergedItems = trace.AllRows.Select(ToDisplayItem).ToList();
        var directItems = trace.DirectRows.Select(ToDisplayItem).ToList();
        var incomingItems = trace.IncomingRows.Select(ToDisplayItem).ToList();
        var outgoingItems = trace.OutgoingRows.Select(ToDisplayItem).ToList();

        NotchValidationItems = new ObservableCollection<NotchValidationDisplayItem>(mergedItems);
        NotchValidationDirectItems = new ObservableCollection<NotchValidationDisplayItem>(directItems);
        NotchValidationIncomingItems = new ObservableCollection<NotchValidationDisplayItem>(incomingItems);
        NotchValidationOutgoingItems = new ObservableCollection<NotchValidationDisplayItem>(outgoingItems);
        NotchValidationDirectCount = directItems.Count;
        NotchValidationIncomingCount = incomingItems.Count;
        NotchValidationOutgoingCount = outgoingItems.Count;

        NotchValidationSummaryText =
            $"REG {report.RegularPadId} | IC{report.IcIndex + 1}/diff{report.DiffIndex} | rows={mergedItems.Count} (direct={directItems.Count}, in={incomingItems.Count}, out={outgoingItems.Count})";

        SetStatus($"Validation updated: REG {report.RegularPadId}, rows={report.DirectRows.Count}, in={report.IncomingRows.Count}, out={report.OutgoingRows.Count}.");
    }

    public bool TryGetNotchValidationReportForRegularPad(
        int regularPadId,
        out NotchValidationReport report,
        out int nullDiffValue,
        out string failureCode,
        out string failureMessage)
    {
        report = default!;
        _projectFile.Settings.ValidateOrThrow();
        nullDiffValue = _projectFile.Settings.Notch.NullValue;
        failureCode = string.Empty;
        failureMessage = string.Empty;

        if (_grid is null)
        {
            failureCode = "NOT_READY";
            failureMessage = "Validation: build grid first.";
            return false;
        }

        var regularPad = _grid.Pads.FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        if (regularPad is null)
        {
            failureCode = "PAD_NOT_FOUND";
            failureMessage = $"Validation: REG {regularPadId} not found.";
            return false;
        }

        if (_lastGeneratedNotchTable is null || _lastGeneratedNotchTable.Rows.Count == 0)
        {
            failureCode = "NOT_READY";
            failureMessage = "Validation: no generated notch table. Run Step 5 Export once first.";
            return false;
        }

        var bucket = EnsureNotchValidationBucket(_lastGeneratedNotchTable, nullDiffValue);
        report = NotchValidationUseCase.BuildRegularReportFromBucket(
            regularPadId,
            regularPad,
            bucket);
        return true;
    }

    private NotchValidationBucket EnsureNotchValidationBucket(
        NotchTable table,
        int nullDiffValue)
    {
        if (_notchValidationBucketCache is not null &&
            ReferenceEquals(_notchValidationBucketTable, table) &&
            _notchValidationBucketNullDiffValue == nullDiffValue)
        {
            return _notchValidationBucketCache;
        }

        var stopwatch = Stopwatch.StartNew();
        _notchValidationBucketCache = NotchValidationUseCase.BuildBucket(
            table,
            nullDiffValue);
        _notchValidationBucketTable = table;
        _notchValidationBucketNullDiffValue = nullDiffValue;
        Logger.Info(CultureInfo.InvariantCulture, "Validation bucket rebuilt: tableRows={0}, null={1}, elapsed={2}ms.",
            table.Rows.Count,
            nullDiffValue,
            stopwatch.ElapsedMilliseconds);
        return _notchValidationBucketCache;
    }

    private void ClearNotchValidation()
    {
        NotchValidationSummaryText = "Validation: select REG, then run Analyze.";
        ClearNotchValidationCollections();
    }

    private void ClearNotchValidationCollections()
    {
        NotchValidationItems = new ObservableCollection<NotchValidationDisplayItem>();
        NotchValidationDirectItems = new ObservableCollection<NotchValidationDisplayItem>();
        NotchValidationIncomingItems = new ObservableCollection<NotchValidationDisplayItem>();
        NotchValidationOutgoingItems = new ObservableCollection<NotchValidationDisplayItem>();
        NotchValidationDirectCount = 0;
        NotchValidationIncomingCount = 0;
        NotchValidationOutgoingCount = 0;
    }

    private void InvalidateNotchValidationCache()
    {
        _notchValidationBucketCache = null;
        _notchValidationBucketTable = null;
        _notchValidationBucketNullDiffValue = -1;
        ClearNotchValidationCollections();
    }

    private void FocusNotchValidationItem(NotchValidationDisplayItem item)
    {
        var cadIds = new List<int>();
        if (item.CadPadId >= 0)
        {
            cadIds.Add(item.CadPadId);
        }

        var regularIds = new List<int>();
        if (item.SourceRegularPadId >= 0)
        {
            regularIds.Add(item.SourceRegularPadId);
        }

        if (item.TargetRegularPadId >= 0 && item.TargetRegularPadId != item.SourceRegularPadId)
        {
            regularIds.Add(item.TargetRegularPadId);
        }

        if (cadIds.Count == 0 && regularIds.Count == 0)
        {
            SetStatus("Validation focus skipped: no target pad available.");
            return;
        }

        TryLocateSelection(
            cadIds,
            regularIds,
            focusSelection: true,
            notVisibleStatus: "Validation focus: target pads are not visible.",
            successStatusBuilder: (cadCount, regularCount) =>
                $"Validation focus: {item.KindText} {item.RowText} (CAD={cadCount}, REG={regularCount}).");
    }

    private int? GetPrimarySelectedRegularPadIdForNotchValidation()
    {
        if (_selectedRegIndices.Count == 0)
        {
            return null;
        }

        foreach (var regularPad in RegularPads)
        {
            if (_selectedRegIndices.Contains(regularPad.Index))
            {
                return regularPad.RegularPadId;
            }
        }

        return null;
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatAreaText(double area)
    {
        if (double.IsNaN(area) || double.IsInfinity(area))
        {
            return "-";
        }

        return area.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static NotchValidationDisplayItem ToDisplayItem(NotchValidationTraceRow row)
    {
        var ratioText = row.RatioPercent.HasValue
            ? $"{FormatPercent(row.RatioPercent.Value)}%"
            : "-";
        var areaText = row.EffectiveArea.HasValue
            ? FormatAreaText(row.EffectiveArea.Value)
            : "-";

        return new NotchValidationDisplayItem(
            KindText: row.KindText,
            RowText: $"#{row.RowNumber}",
            VersionText: row.Version.ToDisplayLabel(),
            IcText: $"IC {row.IcIndex + 1}",
            SourceText: row.SourceText,
            TargetText: row.TargetText,
            RatioText: ratioText,
            AreaText: areaText,
            ValuesText: row.ValuesText,
            CommentText: row.CommentText,
            NoteText: row.NoteText,
            SourceRegularPadId: row.SourceRegularPadId,
            TargetRegularPadId: row.TargetRegularPadId,
            CadPadId: row.CadPadId,
            IcIndex: row.IcIndex,
            SourceDiffIndex: row.SourceDiffIndex,
            TargetDiffIndex: row.TargetDiffIndex,
            IsDirect: row.IsDirect,
            IsIncoming: row.IsIncoming,
            IsOutgoing: row.IsOutgoing);
    }
}

