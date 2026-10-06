using System.Collections.Specialized;
using System.Text;
using FreeformHelper.UI.Logging;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class ShellViewModel
{
    // Tests can schedule a background Add at the snapshot boundary without relying on timing.
    internal Action? ConsoleSnapshotReadForTests { get; set; }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsConsoleExpanded)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) // If collection was cleared.
            {
                _consoleTextBuffer.Clear();
                ConsoleText = string.Empty;
                ConsoleRenderedLineCount = 0;
                ConsoleSourceLineCount = 0;
                OnPropertyChanged(nameof(ConsoleSummaryText));
                return;
            }

            RefreshConsoleSummaryCounts();
            return;
        }

        if (IsConsoleDedupEnabled)
        {
            RebuildConsoleText();
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            // The ring snapshot may already include this notification. Replace until the UI collection catches up.
            if (e.NewStartingIndex < ConsoleSourceLineCount)
            {
                RebuildConsoleText();
                return;
            }

            AppendConsoleLines(e.NewItems.Cast<AppLogEntry>()); // Append new log entries.
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset) // If collection was cleared.
        {
            _consoleTextBuffer.Clear();
            ConsoleText = string.Empty;
            ConsoleRenderedLineCount = 0;
            ConsoleSourceLineCount = 0;
            OnPropertyChanged(nameof(ConsoleSummaryText));
            return;
        }

        RebuildConsoleText(); // For other changes, rebuild the entire text.
    }

    private static bool ShouldSkipInitialGridBootstrap()
    {
        if (AppContext.TryGetSwitch("FreeformHelper.DisableInitialGridBootstrap", out var disabledBySwitch) &&
            disabledBySwitch)
        {
            return true;
        }

        var processName = Environment.ProcessPath is { Length: > 0 }
            ? Path.GetFileNameWithoutExtension(Environment.ProcessPath)
            : string.Empty;
        return processName.Contains("testhost", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Appends a collection of <see cref="AppLogEntry"/> objects to the <see cref="ConsoleText"/>.
    /// </summary>
    /// <param name="entries">The log entries to append.</param>
    private void AppendConsoleLines(IEnumerable<AppLogEntry> entries)
    {
        if (entries is null)
        {
            return;
        }

        var bufferedEntries = entries as IReadOnlyCollection<AppLogEntry> ?? entries.ToList();
        if (bufferedEntries.Count == 0)
        {
            return;
        }

        var totalSourceCount = AppLogStore.Instance.GetTotalCount();
        var remainingCapacity = Math.Max(0, ConsoleRenderTailSourceLineLimit - ConsoleRenderedLineCount);
        if (ConsoleRenderedLineCount >= ConsoleRenderTailSourceLineLimit ||
            bufferedEntries.Count > remainingCapacity ||
            totalSourceCount > ConsoleRenderTailSourceLineLimit)
        {
            RebuildConsoleText();
            return;
        }

        var appendedCount = 0;
        foreach (var entry in bufferedEntries)
        {
            AppendConsoleLine(_consoleTextBuffer, AppLogFormatter.FormatLine(entry));
            appendedCount++;
        }

        ConsoleText = _consoleTextBuffer.ToString(); // Update the bound property.
        ConsoleRenderedLineCount += appendedCount;
        ConsoleSourceLineCount = AppLogStore.Instance.Entries.Count;
        OnPropertyChanged(nameof(ConsoleSummaryText));
    }

    /// <summary>
    /// Rebuilds the entire <see cref="ConsoleText"/> from all current <see cref="AppLogEntry"/> objects.
    /// </summary>
    private void RebuildConsoleText()
    {
        _consoleTextBuffer.Clear();
        var renderedCount = 0;
        ConsoleSnapshotReadForTests?.Invoke();
        var tailLimit = IsConsoleDedupEnabled ? AppLogStore.Instance.MaxEntries : ConsoleRenderTailSourceLineLimit;
        var tailEntries = AppLogStore.Instance.GetTail(tailLimit, out var totalSourceCount);

        if (!IsConsoleDedupEnabled)
        {
            foreach (var entry in tailEntries)
            {
                AppendConsoleLine(_consoleTextBuffer, AppLogFormatter.FormatLine(entry));
                renderedCount++;
            }

            ConsoleText = _consoleTextBuffer.ToString();
            ConsoleRenderedLineCount = renderedCount;
            ConsoleSourceLineCount = totalSourceCount;
            OnPropertyChanged(nameof(ConsoleSummaryText));
            return;
        }

        string? pendingLine = null;
        var pendingCount = 0;
        foreach (var entry in tailEntries)
        {
            var line = AppLogFormatter.FormatLine(entry);

            if (string.Equals(line, pendingLine, StringComparison.Ordinal))
            {
                pendingCount++;
                continue;
            }

            if (!string.IsNullOrEmpty(pendingLine))
            {
                AppendCollapsedLine(_consoleTextBuffer, pendingLine, pendingCount);
                renderedCount++;
            }

            pendingLine = line;
            pendingCount = 1;
        }

        if (!string.IsNullOrEmpty(pendingLine))
        {
            AppendCollapsedLine(_consoleTextBuffer, pendingLine, pendingCount);
            renderedCount++;
        }

        ConsoleText = _consoleTextBuffer.ToString();
        ConsoleRenderedLineCount = renderedCount;
        ConsoleSourceLineCount = totalSourceCount;
        OnPropertyChanged(nameof(ConsoleSummaryText));
    }

    private void RefreshConsoleSummaryCounts()
    {
        ConsoleSourceLineCount = AppLogStore.Instance.GetTotalCount();
        ConsoleRenderedLineCount = IsConsoleDedupEnabled
            ? ConsoleSourceLineCount
            : Math.Min(ConsoleSourceLineCount, ConsoleRenderTailSourceLineLimit);
        OnPropertyChanged(nameof(ConsoleSummaryText));
    }

    partial void OnIsConsoleExpandedChanged(bool value)
    {
        if (value)
        {
            RebuildConsoleText();
            return;
        }

        RefreshConsoleSummaryCounts();
    }

    partial void OnIsConsoleDedupEnabledChanged(bool value)
    {
        if (IsConsoleExpanded)
        {
            RebuildConsoleText();
            return;
        }

        RefreshConsoleSummaryCounts();
    }

    private static void AppendCollapsedLine(StringBuilder builder, string line, int count)
    {
        var collapsed = line;
        if (count > 1)
        {
            collapsed = $"{line} (x{count})";
        }

        AppendConsoleLine(builder, collapsed);
    }

    private static void AppendConsoleLine(StringBuilder builder, string line)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line);
    }
}
