using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void ResetFreeformAutoDetectStats(string summary = FreeformAutoDetectResult.NotRunSummary)
    {
        FreeformAutoDetectResult = new FreeformAutoDetectResult(
            summary,
            new ObservableCollection<FreeformAutoDetectStatRow>());
    }

    partial void OnFreeformAutoDetectResultChanged(FreeformAutoDetectResult? oldValue, FreeformAutoDetectResult newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        RaiseWhenChanged(oldValue.Summary, newValue.Summary, nameof(FreeformAutoDetectSummary));
        RaiseWhenChanged(oldValue.Rows, newValue.Rows, nameof(FreeformAutoDetectRows));
        RaiseWhenChanged(oldValue.HasRows, newValue.HasRows, nameof(HasFreeformAutoDetectRows));
    }

    private void RefreshFreeformAutoDetectStats(string source)
    {
        if (_grid is null || _grid.Pads.Count == 0)
        {
            ResetFreeformAutoDetectStats("Freeform stats: no regular pads.");
            return;
        }

        FreeformStatistics stats = FreeformStatisticsBuilder.Build(_grid.Pads);
        var rows = new ObservableCollection<FreeformAutoDetectStatRow>
        {
            new("None", stats.NoneCount, stats.TotalCount),
            new("XWay", stats.XWayCount, stats.TotalCount),
            new("YWay", stats.YWayCount, stats.TotalCount),
            new("XYWay", stats.XyWayCount, stats.TotalCount),
        };

        FreeformAutoDetectResult = new FreeformAutoDetectResult(
            $"Freeform stats ({source}): total={stats.TotalCount}, tagged={stats.TaggedCount}, none={stats.NoneCount}.",
            rows);
    }
}
