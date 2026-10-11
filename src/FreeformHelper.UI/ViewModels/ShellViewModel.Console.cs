using System.Collections.Specialized;
using System.Text;
using FreeformHelper.UI.Logging;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class ShellViewModel
{
    // Tests can schedule a background Add at the snapshot boundary without relying on timing.
    internal Action? ConsoleSnapshotReadForTests { get; set; }

    // Runs on a notification thread that finds the gate held, right before it waits for the gate.
    internal Action? ConsoleGateContendedForTests { get; set; }

    // What a section of the gate changed. The caller raises these after it releases the gate, because a
    // subscriber can write a log line, and that write can wait for another thread that waits for the gate.
    [Flags]
    private enum ConsoleChange
    {
        None = 0,
        Text = 1,
        RenderedLineCount = 2,
        SourceLineCount = 4,
        Summary = 8,
    }

    internal void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The handler decides from the text and the counts, so the decision and the change are one section.
        if (!_consoleTextGate.TryEnter())
        {
            ConsoleGateContendedForTests?.Invoke();
            _consoleTextGate.Enter();
        }

        ConsoleChange change;
        try
        {
            change = HandleLogEntriesChanged(e);
        }
        finally
        {
            _consoleTextGate.Exit();
        }

        RaiseConsoleChange(change);
    }

    /// <summary>
    /// Decides from the text and the counts and changes them. The caller holds the console text gate.
    /// </summary>
    private ConsoleChange HandleLogEntriesChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!IsConsoleExpanded)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) // If collection was cleared.
            {
                return ClearConsoleText();
            }

            return UpdateConsoleSummaryCounts();
        }

        if (IsConsoleDedupEnabled)
        {
            return BuildConsoleText();
        }

        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            // The ring snapshot may already include this notification. Replace until the UI collection catches up.
            if (e.NewStartingIndex < _consoleSourceLineCount)
            {
                return BuildConsoleText();
            }

            return AppendConsoleLines(e.NewItems.Cast<AppLogEntry>()); // Append new log entries.
        }

        if (e.Action == NotifyCollectionChangedAction.Reset) // If collection was cleared.
        {
            return ClearConsoleText();
        }

        return BuildConsoleText(); // For other changes, rebuild the entire text.
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
    /// Appends a collection of <see cref="AppLogEntry"/> objects to the console text.
    /// The caller holds the console text gate.
    /// </summary>
    /// <param name="entries">The log entries to append.</param>
    private ConsoleChange AppendConsoleLines(IEnumerable<AppLogEntry> entries)
    {
        if (entries is null)
        {
            return ConsoleChange.None;
        }

        var bufferedEntries = entries as IReadOnlyCollection<AppLogEntry> ?? entries.ToList();
        if (bufferedEntries.Count == 0)
        {
            return ConsoleChange.None;
        }

        var totalSourceCount = AppLogStore.Instance.GetTotalCount();
        var remainingCapacity = Math.Max(0, ConsoleRenderTailSourceLineLimit - _consoleRenderedLineCount);
        if (_consoleRenderedLineCount >= ConsoleRenderTailSourceLineLimit ||
            bufferedEntries.Count > remainingCapacity ||
            totalSourceCount > ConsoleRenderTailSourceLineLimit)
        {
            return BuildConsoleText();
        }

        var appendedCount = 0;
        foreach (var entry in bufferedEntries)
        {
            AppendConsoleLine(_consoleTextBuffer, AppLogFormatter.FormatLine(entry));
            appendedCount++;
        }

        return SetConsoleState(
            _consoleTextBuffer.ToString(),
            _consoleRenderedLineCount + appendedCount,
            AppLogStore.Instance.Entries.Count) | ConsoleChange.Summary;
    }

    /// <summary>
    /// Rebuilds the entire console text from all current <see cref="AppLogEntry"/> objects.
    /// </summary>
    private void RebuildConsoleText()
    {
        ConsoleChange change;
        lock (_consoleTextGate)
        {
            change = BuildConsoleText();
        }

        RaiseConsoleChange(change);
    }

    /// <summary>
    /// Builds the text from the tail of the log store. The caller holds the console text gate.
    /// </summary>
    private ConsoleChange BuildConsoleText()
    {
        var renderedCount = 0;
        _consoleTextBuffer.Clear();
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
        }
        else
        {
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
        }

        // The notification handler decides from these counts, so they change together with the text.
        return SetConsoleState(_consoleTextBuffer.ToString(), renderedCount, totalSourceCount) | ConsoleChange.Summary;
    }

    private void RefreshConsoleSummaryCounts()
    {
        ConsoleChange change;
        lock (_consoleTextGate)
        {
            change = UpdateConsoleSummaryCounts();
        }

        RaiseConsoleChange(change);
    }

    /// <summary>
    /// Refreshes the counts from the log store. The caller holds the console text gate.
    /// </summary>
    private ConsoleChange UpdateConsoleSummaryCounts()
    {
        var sourceLineCount = AppLogStore.Instance.GetTotalCount();
        var renderedLineCount = IsConsoleDedupEnabled
            ? sourceLineCount
            : Math.Min(sourceLineCount, ConsoleRenderTailSourceLineLimit);
        return SetConsoleState(_consoleText, renderedLineCount, sourceLineCount) | ConsoleChange.Summary;
    }

    /// <summary>
    /// Empties the buffer, the text and the counts. The caller holds the console text gate.
    /// </summary>
    private ConsoleChange ClearConsoleText()
    {
        _consoleTextBuffer.Clear();
        return SetConsoleState(string.Empty, 0, 0) | ConsoleChange.Summary;
    }

    /// <summary>
    /// Stores the text and the counts and reports which of them changed. The caller holds the console text gate.
    /// </summary>
    private ConsoleChange SetConsoleState(string text, int renderedLineCount, int sourceLineCount)
    {
        var change = ConsoleChange.None;
        if (!string.Equals(_consoleText, text, StringComparison.Ordinal))
        {
            _consoleText = text;
            change |= ConsoleChange.Text;
        }

        if (_consoleRenderedLineCount != renderedLineCount)
        {
            _consoleRenderedLineCount = renderedLineCount;
            change |= ConsoleChange.RenderedLineCount;
        }

        if (_consoleSourceLineCount != sourceLineCount)
        {
            _consoleSourceLineCount = sourceLineCount;
            change |= ConsoleChange.SourceLineCount;
        }

        return change;
    }

    // The caller does not hold the console text gate.
    private void RaiseConsoleChange(ConsoleChange change)
    {
        if (change.HasFlag(ConsoleChange.Text))
        {
            OnPropertyChanged(nameof(ConsoleText));
        }

        if (change.HasFlag(ConsoleChange.RenderedLineCount))
        {
            OnPropertyChanged(nameof(ConsoleRenderedLineCount));
        }

        if (change.HasFlag(ConsoleChange.SourceLineCount))
        {
            OnPropertyChanged(nameof(ConsoleSourceLineCount));
        }

        if (change.HasFlag(ConsoleChange.Summary))
        {
            OnPropertyChanged(nameof(ConsoleSummaryText));
        }
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
