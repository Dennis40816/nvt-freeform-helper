// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;

namespace FreeformHelper.UI.Views;

public sealed partial class NotchExportFilterBuilderWindow
{
    private string BuildQuery()
    {
        if (!HasInputControls())
        {
            return string.Empty;
        }

        var segments = new List<string>();
        Append(segments, "ic", IcTextBox.Text);
        Append(segments, "diff", DiffTextBox.Text);
        Append(segments, "reg", RegTextBox.Text);
        Append(segments, "cad", CadTextBox.Text);
        Append(segments, "a", AnchorTextBox.Text);

        if (StatusComboBox.SelectedItem is ComboBoxItem item)
        {
            var status = item.Content?.ToString() ?? string.Empty;
            var normalized = status switch
            {
                "Linked" => "linked",
                "No-op" => "noop",
                "No CAD" => "nocad",
                "Legacy" => "legacy",
                _ => string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                segments.Add($"status={normalized}");
            }
        }

        var fuzzy = FuzzyTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(fuzzy))
        {
            var isInitialQuery = string.Equals(fuzzy, _initialQuery.Trim(), StringComparison.Ordinal);
            segments.Add(NeedsQuote(fuzzy) && !isInitialQuery ? $"\"{fuzzy}\"" : fuzzy);
        }

        return string.Join(' ', segments);
    }

    private static void Append(List<string> segments, string key, string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        segments.Add($"{key}={text}");
    }

    private static bool NeedsQuote(string text)
    {
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                return true;
            }
        }

        return false;
    }
}
