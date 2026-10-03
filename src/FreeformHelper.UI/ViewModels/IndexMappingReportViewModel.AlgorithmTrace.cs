using System.Globalization;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingReportViewModel
{
    private void MarkAlgorithmTraceNotRequested()
    {
        AlgorithmTraceEntries = Array.Empty<IndexMappingAlgorithmTraceEntryViewModel>();
        AlgorithmTraceState = IndexMappingAlgorithmTraceState.NotRequested;
        AlgorithmTraceStatusText = "Trace: not requested.";
        OnPropertyChanged(nameof(HasAlgorithmTraceEntries));
    }

    private async Task EnsureAlgorithmTraceForSelectedDecisionAsync(bool priorityOnly)
    {
        var selected = SelectedDecision;
        if (selected is null)
        {
            MarkAlgorithmTraceNotRequested();
            return;
        }

        if (selected.IsAggregateRow)
        {
            AlgorithmTraceEntries = Array.Empty<IndexMappingAlgorithmTraceEntryViewModel>();
            AlgorithmTraceState = IndexMappingAlgorithmTraceState.NotRequested;
            AlgorithmTraceStatusText = "Trace: aggregate row (select CAD/REG entity).";
            OnPropertyChanged(nameof(HasAlgorithmTraceEntries));
            return;
        }

        var key = BuildTraceCacheKey(selected);
        if (_traceCache.TryGetValue(key, out var cached))
        {
            AlgorithmTraceEntries = cached;
            AlgorithmTraceState = IndexMappingAlgorithmTraceState.Ready;
            AlgorithmTraceStatusText = $"Trace: ready ({cached.Count.ToString(CultureInfo.InvariantCulture)} entries).";
            OnPropertyChanged(nameof(HasAlgorithmTraceEntries));
        }
        else
        {
            await LoadTraceAsync(selected, key);
        }

        if (priorityOnly)
        {
            _ = PrefetchBackgroundTraceAsync();
        }
    }

    private async Task LoadTraceAsync(IndexMappingDecisionRowViewModel decision, string key)
    {
        var requestVersion = Interlocked.Increment(ref _traceRequestVersion);
        AlgorithmTraceState = IndexMappingAlgorithmTraceState.Loading;
        AlgorithmTraceStatusText = "Trace: loading...";

        try
        {
            var trace = await Task.Run(
                () => IndexMappingAlgorithmTraceProjector.Build(decision, TraceSchemaVersion, DateTime.UtcNow));
            if (requestVersion != _traceRequestVersion)
            {
                AlgorithmTraceState = IndexMappingAlgorithmTraceState.Stale;
                AlgorithmTraceStatusText = "Trace: stale (selection changed).";
                return;
            }

            _traceCache[key] = trace;
            if (!ReferenceEquals(SelectedDecision, decision))
            {
                AlgorithmTraceState = IndexMappingAlgorithmTraceState.Stale;
                AlgorithmTraceStatusText = "Trace: stale (selection changed).";
                return;
            }

            AlgorithmTraceEntries = trace;
            AlgorithmTraceState = IndexMappingAlgorithmTraceState.Ready;
            AlgorithmTraceStatusText = $"Trace: ready ({trace.Count.ToString(CultureInfo.InvariantCulture)} entries).";
            OnPropertyChanged(nameof(HasAlgorithmTraceEntries));
        }
        catch (Exception ex)
        {
            AlgorithmTraceState = IndexMappingAlgorithmTraceState.Failed;
            AlgorithmTraceStatusText = $"Trace: failed ({ex.GetType().Name}).";
        }
    }

    private async Task PrefetchBackgroundTraceAsync()
    {
        var visibleRows = DecisionRows.Take(3).ToList();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxConcurrentTraceJobs,
        };

        await Parallel.ForEachAsync(visibleRows, options, (row, _) =>
        {
            var key = BuildTraceCacheKey(row);
            if (!_traceCache.ContainsKey(key))
            {
                var trace = IndexMappingAlgorithmTraceProjector.Build(row, TraceSchemaVersion, DateTime.UtcNow);
                _traceCache[key] = trace;
            }

            return ValueTask.CompletedTask;
        });
    }

}
